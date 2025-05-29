using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Microsoft.Dafny.DafnyLogger;
using static Microsoft.Dafny.VerifierCmd;

namespace Microsoft.Dafny {
  public class ConclusionSketcher : ProofSketcher {
    private readonly ErrorReporter reporter;
    private readonly ConditionAssertionProofSketcher conditionSketcher;
    private readonly InductiveProofSketcher inductiveSketcher;
    public ConclusionSketcher(ErrorReporter reporter) : base(reporter) {
        this.reporter = reporter;
        conditionSketcher = new ConditionAssertionProofSketcher(reporter);
        inductiveSketcher = new InductiveProofSketcher(reporter);
    }

    public override async Task<SketchResponse> GenerateSketch(SketchRequest input) {
        var program = input.ResolvedProgram;
        var (method, lineNumber) = conditionSketcher.ClarifyMethodAndLine(program, input.Method, input.LineNumber);
        var programResolver = new ProgramResolver(program);
        var resolver = new ModuleResolver(programResolver, program.Options);
        resolver.moduleInfo = program.SystemModuleManager.systemNameInfo;
        foreach (var t in program.DefaultModuleDef.TopLevelDecls) {
            if (t is TopLevelDeclWithMembers tm) {
                resolver.AddClassMembers(tm, tm.Members.ToDictionary(m => m.Name, m => m));
            }
        }
        var resolutionContext = new ResolutionContext(method, true);
        var conditions = conditionSketcher.CollectPreGapConditions(program, method, lineNumber);
        var freeVars = conditions.SelectMany(c => FreeVariablesUtil.ComputeFreeVariables(Reporter.Options, c).ToList()).Distinct().ToList();
        var parameters = freeVars.Select(v => (v.Name, v.Type.ToString())).ToList();
        var requires = conditions.Select(c => c.ToString()).ToList();
        var functionCalls = new List<FunctionCallExpr>();
        conditions.ForEach(c => AllFunctionCalls(c, functionCalls));
        functionCalls = functionCalls.Distinct().ToList();
        var inferredConditions = new List<string>();
        foreach (var functionCall in functionCalls) {
            inferredConditions.AddRange(await inferConditionsOfCall(input.Content, resolver, resolutionContext, requires, parameters, functionCall));
        }
        var allRequires = requires.Concat(inferredConditions.Select(c => c.ToString())).Distinct().ToList();
        var s = (input.Indent ?? 8) / 4;
      
        var sb = new StringBuilder();
        sb.AppendLine("");
        foreach (var condition in allRequires) {
            sb.AppendLine($"{Indent(s)}assert {condition};");
        }
        return new SketchResponse(sb.ToString());
    }

    private async Task<List<string>> inferConditionsOfCall(string context, ModuleResolver resolver, ResolutionContext resolutionContext, List<string> requires, List<(string Name, string Type)> parameters, FunctionCallExpr functionCall) {
        var inferredConditions = new List<string>();
        Function followedFunction = functionCall.Function;
        if (followedFunction == null || followedFunction.Body == null) {
            Log("### Funtion body for function " + functionCall + " is null");
            return inferredConditions;
        }
        await inferConditionsOfFunctionInCall(context, resolver, resolutionContext, requires, parameters, functionCall, followedFunction, inferredConditions);
        return inferredConditions;
    }


    private async Task inferConditionsOfFunctionInCall(string context, ModuleResolver resolver, ResolutionContext resolutionContext,  List<string> requires, List<(string Name, string Type)> parameters, FunctionCallExpr functionCallExpr, Function followedFunction, List<string> inferredConditions) {
        var functionBody = followedFunction.Body;

      var map = inductiveSketcher.MapFunctionParametersToArguments(followedFunction, functionCallExpr);
      var env = inductiveSketcher.ReverseMapForVarValues(map);
      var substitutedBody = inductiveSketcher.SubstituteExpression(followedFunction.Body, map);

      await FollowExpr(substitutedBody, resolver, resolutionContext, followedFunction, functionCallExpr, env, parameters, context, requires, new List<string>(), inferredConditions);
    }

    private async Task FollowExpr(Expression expr, ModuleResolver resolver, ResolutionContext resolutionContext, Function followedFunction, FunctionCallExpr functionCallExpr, Dictionary<string, IVariable> env, List<(string Name, string Type)> parameters, string context, List<string> requires, List<string> path, List<string> inferredConditions) {
            var allParameters = parameters.Concat(env.Select(kvp => (kvp.Key, kvp.Value.Type.ToString()))).Distinct().ToList();
            if (expr is ITEExpr iteExpr) {
                var test = iteExpr.Test.ToString();
                var disjointConditions = new List<(string, List<string>)>();
                var notTest = "!(" + test + ")";
                var oks = await RunVerifierConditions(context, allParameters, requires.Concat(path).ToList(), new List<string> { test, notTest });
                foreach (var ok in oks) {
                    var subConditions = new List<string>();
                    var next = (ok.Item1 == 0) ? iteExpr.Thn : iteExpr.Els;
                    await FollowExpr(next, resolver, resolutionContext, followedFunction, functionCallExpr, env, parameters, context, requires, path.Concat(new List<string> { ok.Item2 }).ToList(), subConditions);
                    if (subConditions.Count > 0) {
                        disjointConditions.Add((ok.Item2, subConditions));
                    }
                }
                addDisjointConditions(disjointConditions, inferredConditions);
            } else if (expr is NestedMatchExpr nestedMatchExpr) {
                Log("## NestedMatchExpr: " + nestedMatchExpr);
                var source = nestedMatchExpr.Source;
                var disjointConditions = new List<(string, List<string>)>();
                var cases = new List<string>();
                var substs = new List<Expression>();
                var extendedEnvs = new List<Dictionary<string, IVariable>>();
                foreach (var caseStmt in nestedMatchExpr.Cases) {
                    var pattern = caseStmt.Pat;
                    if (pattern is IdPattern idPattern && idPattern.Ctor != null) {
                        var variables = idPattern.Arguments.Select(p => p as IdPattern).Where(p => p != null).Select(p => p.BoundVar).ToList();
                        Log("### idPattern: " + idPattern);
                        Log("### variables: " + string.Join(", ", variables.Select(v => v.Name)));
                        Log("### number of arguments: " + idPattern.Arguments.Count);
                        var extendedEnv = inductiveSketcher.ExtendEnvironment(env, variables);
                        var predicate = "(" + source.ToString() + ")." + idPattern.Ctor.Name + "?";
                        var extendedPath = path.Concat(new List<string> { predicate }).ToList();
                        var arguments = idPattern.Ctor.Formals.Select(p => {
                            var e = new MemberSelectExpr(source.Origin, source, p.NameNode);
                            resolver.ResolveExpression(e, resolutionContext);
                            return e;
                        });
                        Log("### arguments: " + string.Join(", ", arguments.Select(a => a.ToString())));
                        var map = idPattern.Arguments.Zip(arguments).Select(p => {
                            if (p.Item1 is IdPattern vid) {
                                return (vid.BoundVar, (Expression)p.Item2);
                            } else {
                                return (null, null);
                            }
                        }).Where(p => p.Item1 != null).ToDictionary();
                        var substBody = inductiveSketcher.SubstituteExpression(caseStmt.Body, map);
                        cases.Add(predicate);
                        substs.Add(substBody);
                        extendedEnvs.Add(extendedEnv);
                    } else {
                        Log("### Not IdPattern " + pattern);
                    }
                }
                var oks = await RunVerifierConditions(context, allParameters, requires.Concat(path).ToList(), cases);
                foreach (var ok in oks) {
                    var subConditions = new List<string>();
                    await FollowExpr(substs[ok.Item1], resolver, resolutionContext, followedFunction, functionCallExpr, extendedEnvs[ok.Item1], parameters, context, requires, path.Concat(new List<string> { ok.Item2 }).ToList(), subConditions);
                    if (subConditions.Count > 0) {
                        disjointConditions.Add((ok.Item2, subConditions));
                    }
                }
                addDisjointConditions(disjointConditions, inferredConditions);
            } else if (expr is LetExpr letExpr) {
                Log("## LetExpr: " + letExpr);
                await FollowExpr(BoogieGenerator.InlineLet(letExpr), resolver, resolutionContext, followedFunction, functionCallExpr, env, parameters, context, requires, path, inferredConditions);
            } else if (expr.Type.ToString() == functionCallExpr.Type.ToString()) {
                var eq = "(" + functionCallExpr.ToString() + ")==(" + expr.ToString() + ")";
                inferredConditions.Add(eq);
            } else {
                Log("### Unhandled expression: " + expr.Type + " vs " + functionCallExpr.Type + " for " + expr);
            }
    }

    private void addDisjointConditions(List<(string, List<string>)> disjointConditions, List<string> inferredConditions) {
        var n = disjointConditions.Count;
        if (n == 1) {
            inferredConditions.AddRange(disjointConditions[0].Item2);
        } else if (n > 1) {
            inferredConditions.Add(string.Join(" || ", disjointConditions.Select(dc => "("+dc.Item1+")")));
            foreach (var x in disjointConditions) {
                inferredConditions.Add("( " + x.Item1 + " ) ==> (" + string.Join(" && ", x.Item2.Select(c => "(" + c + ")")) + ")");
            }
        }
    }

    private void AllFunctionCalls(Expression expr, List<FunctionCallExpr> functionCalls) {
        if (expr is FunctionCallExpr functionCall) {
            functionCalls.Add(functionCall);
        }
        foreach (var subExpr in expr.SubExpressions) {
            AllFunctionCalls(subExpr, functionCalls);
        }
    }
  }
}