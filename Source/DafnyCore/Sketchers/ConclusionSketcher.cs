using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Boogie;
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
        var conditions = conditionSketcher.CollectPreGapConditions(program, method, lineNumber);
        var freeVars = conditions.SelectMany(c => FreeVariablesUtil.ComputeFreeVariables(Reporter.Options, c).ToList()).Distinct().ToList();
        var parameters = freeVars.Select(v => (v.Name, v.Type.ToString())).ToList();
        var requires = conditions.Select(c => c.ToString()).ToList();
        var functionCalls = new List<FunctionCallExpr>();
        conditions.ForEach(c => AllFunctionCalls(c, functionCalls));
        functionCalls = functionCalls.Distinct().ToList();
        var inferredConditions = new List<Expression>();
        foreach (var functionCall in functionCalls) {
            inferredConditions.AddRange(await inferConditionsOfCall(input.Content, requires, parameters, functionCall));
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

    private async Task<List<Expression>> inferConditionsOfCall(string context, List<string> requires, List<(string Name, string Type)> parameters, FunctionCallExpr functionCall) {
        var inferredConditions = new List<Expression>();
        Function followedFunction = functionCall.Function;
        if (followedFunction == null || followedFunction.Body == null) {
            Log("### Funtion body for function " + functionCall + " is null");
            return inferredConditions;
        }
        await inferConditionsOfFunctionInCall(context, requires, parameters, functionCall, followedFunction, inferredConditions);
        return inferredConditions;
    }


    private async Task inferConditionsOfFunctionInCall(string context, List<string> requires, List<(string Name, string Type)> parameters, FunctionCallExpr functionCallExpr, Function followedFunction, List<Expression> inferredConditions) {
        var functionBody = followedFunction.Body;

      var map = inductiveSketcher.MapFunctionParametersToArguments(followedFunction, functionCallExpr);
      var env = inductiveSketcher.ReverseMapForVarValues(map);
      var substitutedBody = inductiveSketcher.SubstituteExpression(followedFunction.Body, map);

      await FollowExpr(substitutedBody, followedFunction, functionCallExpr, env, parameters, context, requires, new List<Expression>(), inferredConditions);
    }

    private async Task FollowExpr(Expression expr, Function followedFunction, FunctionCallExpr functionCallExpr, Dictionary<string, IVariable> env, List<(string Name, string Type)> parameters, string context, List<string> requires, List<Expression> path, List<Expression> inferredConditions) {
        if (expr is ITEExpr iteExpr) {
            var test = iteExpr.Test;
            await FollowExpr(iteExpr.Thn, followedFunction, functionCallExpr, env, parameters, context, requires, path.Concat(new List<Expression> { test }).ToList(), inferredConditions);
            await FollowExpr(iteExpr.Els, followedFunction, functionCallExpr, env, parameters, context, requires, path.Concat(new List<Expression> { UnaryOpExpr.CreateNot(test.Origin, test) }).ToList(), inferredConditions);
        } else if (expr is NestedMatchExpr nestedMatchExpr) {
            Log("## NestedMatchExpr (ignoring): " + nestedMatchExpr);
            /*
            var source = nestedMatchExpr.Source;
            foreach (var caseStmt in nestedMatchExpr.Cases) {
                var pattern = caseStmt.Pat;
                if (pattern is IdPattern idPattern && idPattern.Ctor != null) {
                    var variables = inductiveSketcher.ExtractVariables(caseStmt);
                    var extendedEnv = inductiveSketcher.ExtendEnvironment(env, variables);
                    var arguments = idPattern.Ctor.Formals.Select(p => (Expression)new MemberSelectExpr(p.Origin, source, p.NameNode));
                    var map = variables.Zip(arguments).ToDictionary();
                    var substBody = inductiveSketcher.SubstituteExpression(caseStmt.Body, map);
                    await FollowExpr(substBody, followedFunction, functionCallExpr, extendedEnv, parameters, context, requires, path, inferredConditions);
                } else {
                    Log("### Not IdPattern " + pattern);
                }
             }
             */
        } else if (expr is LetExpr letExpr) {
            Log("## LetExpr (ignoring): " + letExpr);
            // This could work but slows down the process a lot.
            /*
            var variableMap = inductiveSketcher.ExtractVariables(letExpr);
            var extendedEnv = new Dictionary<string, IVariable>(env);
            foreach (var kvp in variableMap) {
                extendedEnv[kvp.Key.Name] = kvp.Key;
            }
            var substitutedBody = inductiveSketcher.SubstituteExpression(letExpr.Body, variableMap);
            await FollowExpr(substitutedBody, followedFunction, functionCallExpr, extendedEnv, parameters, context, requires, path, inferredConditions);
            */
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