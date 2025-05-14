using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Microsoft.Dafny.DafnyLogger;


namespace Microsoft.Dafny {
  public class ConclusionSketcher : ProofSketcher {
    private readonly ConditionAssertionProofSketcher conditionSketcher;
    public ConclusionSketcher(ErrorReporter reporter) : base(reporter) {
      conditionSketcher = new ConditionAssertionProofSketcher(reporter);
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
            inferredConditions.AddRange(inferConditionsOfCall(requires, functionCall));
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

    private List<Expression> inferConditionsOfCall(List<string> requires, FunctionCallExpr functionCall) {
        var inferredConditions = new List<Expression>();
        // TODO
        inferredConditions.Add(BinaryExpr.CreateEq(functionCall, functionCall, functionCall.Type));
        return inferredConditions;
    }

    private void AllFunctionCalls(Expression expr, List<FunctionCallExpr> functionCalls) {
        if (expr is FunctionCallExpr functionCall) {;
            functionCalls.Add(functionCall);
        }
        foreach (var subExpr in expr.SubExpressions) {
            AllFunctionCalls(subExpr, functionCalls);
        }
    }
  }
}