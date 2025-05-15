using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Boogie;
using Microsoft.Dafny.Triggers;
using static Microsoft.Dafny.DafnyLogger;
using static Microsoft.Dafny.VerifierCmd;

namespace Microsoft.Dafny {
  public class ConclusionSketcher : ProofSketcher {
    private readonly ConditionAssertionProofSketcher conditionSketcher;
    private readonly InductiveProofSketcher inductiveSketcher;
    public ConclusionSketcher(ErrorReporter reporter) : base(reporter) {
      conditionSketcher = new ConditionAssertionProofSketcher(reporter);
      inductiveSketcher = new InductiveProofSketcher(reporter);
    }

    public override async Task<SketchResponse> GenerateSketch(SketchRequest input) {
        var program = input.ResolvedProgram;
        var (method, lineNumber) = conditionSketcher.ClarifyMethodAndLine(program, input.Method, input.LineNumber);
        var resolver = new ModuleResolver(new ProgramResolver(program), Reporter.Options);
        var resolutionContext = ResolutionContext.FromCodeContext(method);
        var conditions = conditionSketcher.CollectPreGapConditions(program, method, lineNumber);
        var freeVars = conditions.SelectMany(c => FreeVariablesUtil.ComputeFreeVariables(Reporter.Options, c).ToList()).Distinct().ToList();
        var parameters = freeVars.Select(v => (v.Name, v.Type.ToString())).ToList();
        var requires = conditions.Select(c => c.ToString()).ToList();
        var functionCalls = new List<FunctionCallExpr>();
        conditions.ForEach(c => AllFunctionCalls(c, functionCalls));
        functionCalls = functionCalls.Distinct().ToList();
        var inferredConditions = new List<Expression>();
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

    private async Task<List<Expression>> inferConditionsOfCall(string context, ModuleResolver resolver, ResolutionContext resolutionContext, List<string> requires, List<(string Name, string Type)> parameters, FunctionCallExpr functionCall) {
        var inferredConditions = new List<Expression>();
        Function followedFunction = functionCall.Function;
        if (followedFunction == null || followedFunction.Body == null) {
            Log("### Funtion body for function " + functionCall + " is null");
            return inferredConditions;
        }
        await inferConditionsOfFunctionInCall(context, resolver, resolutionContext, requires, parameters, functionCall, followedFunction, inferredConditions);
        return inferredConditions;
    }


    private async Task inferConditionsOfFunctionInCall(string context, ModuleResolver resolver, ResolutionContext resolutionContext,  List<string> requires, List<(string Name, string Type)> parameters, FunctionCallExpr functionCallExpr, Function followedFunction, List<Expression> inferredConditions) {
        var functionBody = followedFunction.Body;

      var map = inductiveSketcher.MapFunctionParametersToArguments(followedFunction, functionCallExpr);
      var env = inductiveSketcher.ReverseMapForVarValues(map);
      var substitutedBody = inductiveSketcher.SubstituteExpression(followedFunction.Body, map);

      await FollowExpr(substitutedBody, resolver, resolutionContext, followedFunction, functionCallExpr, env, parameters, context, requires, new List<Expression>(), inferredConditions);
    }

    private async Task FollowExpr(Expression expr, ModuleResolver resolver, ResolutionContext resolutionContext, Function followedFunction, FunctionCallExpr functionCallExpr, Dictionary<string, IVariable> env, List<(string Name, string Type)> parameters, string context, List<string> requires, List<Expression> path, List<Expression> inferredConditions) {
        if (expr is ITEExpr iteExpr) {
            var test = iteExpr.Test;
            await FollowExpr(iteExpr.Thn, resolver, resolutionContext, followedFunction, functionCallExpr, env, parameters, context, requires, path.Concat(new List<Expression> { test }).ToList(), inferredConditions);
            await FollowExpr(iteExpr.Els, resolver, resolutionContext, followedFunction, functionCallExpr, env, parameters, context, requires, path.Concat(new List<Expression> { UnaryOpExpr.CreateNot(test.Origin, test) }).ToList(), inferredConditions);
        } else if (expr is NestedMatchExpr nestedMatchExpr) {
            Log("## NestedMatchExpr: " + nestedMatchExpr);
            var source = nestedMatchExpr.Source;
            foreach (var caseStmt in nestedMatchExpr.Cases) {
                var pattern = caseStmt.Pat;
                if (pattern is IdPattern idPattern && idPattern.Ctor != null) {
                    var variables = idPattern.Arguments.Select(p => p as IdPattern).Where(p => p != null).Select(p => p.BoundVar).ToList();
                    Log("### idPattern: " + idPattern);
                    Log("### variables: " + string.Join(", ", variables.Select(v => v.Name)));
                    Log("### number of arguments: " + idPattern.Arguments.Count);
                    var extendedEnv = inductiveSketcher.ExtendEnvironment(env, variables);
                    var ctorPredicate = new Name(idPattern.Ctor.Name+"?");
                    var predicate = new ExprDotName(source.Origin, source, ctorPredicate, null) {
                        Type = new BoolType()
                    };
                    resolver.ResolveExpression(predicate, resolutionContext);
                    var extendedPath = path.Concat(new List<Expression> { predicate }).ToList();
                    var arguments = idPattern.Ctor.Formals.Select(p => {
                        var e = new ExprDotName(source.Origin, source, p.NameNode, null) {
                            Type = p.Type
                        };
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
                    await FollowExpr(substBody, resolver, resolutionContext, followedFunction, functionCallExpr, extendedEnv, parameters, context, requires, extendedPath, inferredConditions);
                } else {
                    Log("### Not IdPattern " + pattern);
                }
             }
        } else if (expr is LetExpr letExpr) {
            Log("## LetExpr: " + letExpr);
            await FollowExpr(BoogieGenerator.InlineLet(letExpr), resolver, resolutionContext, followedFunction, functionCallExpr, env, parameters, context, requires, path, inferredConditions);
        } else if (expr.Type.ToString() == functionCallExpr.Type.ToString()) {
            var eqExpr = BinaryExpr.CreateEq(functionCallExpr, expr, functionCallExpr.Type);
            var pathRequires = path.Select(e => e.ToString()).ToList();
            var allParameters = parameters.Concat(env.Select(kvp => (kvp.Key, kvp.Value.Type.ToString()))).Distinct().ToList();
            var check = await RunVerifierImplication(context, allParameters, requires.Concat(pathRequires).ToList(), new List<string> { eqExpr.ToString()});
            if (check == 0) {
                inferredConditions.Add(eqExpr);
            }
        } else {
            Log("### Unhandled expression: " + expr.Type + " vs " + functionCallExpr.Type + " for " + expr);
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