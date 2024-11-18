using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Dafny;

namespace Microsoft.Dafny {

  public class InductiveProofSketcher : ProofSketcher {
    private readonly ErrorReporter reporter;

    public InductiveProofSketcher(ErrorReporter reporter) : base(reporter) {
      this.reporter = reporter;
    }

    public override string GenerateProofSketch(Method method, int lineNumber) {
      if (RequiresCallsFunction(method, out var functionCall)) {
        return GenerateFunctionBasedInductionProofSketch(method, functionCall);
      } else {
        return GenerateStandardInductionProofSketch(method);
      }
    }

    private bool RequiresCallsFunction(Method method, out FunctionCallExpr? functionCallExpr) {
      functionCallExpr = null;
      foreach (var req in method.Req) {
        functionCallExpr = FindFunctionCallExpr(req.E);
        if (functionCallExpr != null) {
          return true;
        }
      }
      return false;
    }

    private FunctionCallExpr? FindFunctionCallExpr(Expression expr) {
      if (expr is FunctionCallExpr funcCallExpr) {
        return funcCallExpr;
      }
      foreach (var subExpr in expr.SubExpressions) {
        var result = FindFunctionCallExpr(subExpr);
        if (result != null) {
          return result;
        }
      }
      return null;
    }

    private string GenerateFunctionBasedInductionProofSketch(Method method, FunctionCallExpr functionCallExpr) {
      var sb = new StringBuilder();

      sb.AppendLine($"{Indent(0)}if ({PrintExpression(functionCallExpr.Args[0])} == 0) {{");
      sb.AppendLine($"{Indent(1)}}} else if ({PrintExpression(functionCallExpr.Args[0])} == 1) {{");
      sb.AppendLine($"{Indent(1)}}} else {{");
      sb.AppendLine($"{Indent(2)}{method.Name}({PrintExpression(functionCallExpr.Args[0])} - 2);");
      sb.AppendLine($"{Indent(1)}}}");

      return sb.ToString();
    }

    private class FunctionCase {
      public Expression? Condition { get; set; }
      public bool IsBaseCase { get; set; }
      public FunctionCallExpr? RecursiveCall { get; set; }
    }

    private List<FunctionCase> GetFunctionCases(Function function) {
      var cases = new List<FunctionCase>();
      if (function.Body is ITEExpr iteExpr) {
        AnalyzeITEExpr(iteExpr, function.Name, cases);
      }
      return cases;
    }

    private void AnalyzeITEExpr(ITEExpr iteExpr, string functionName, List<FunctionCase> cases, Expression? accumulatedCondition = null) {
        // Combine accumulated condition with the current "if" condition
        var currentCondition = accumulatedCondition == null
            ? iteExpr.Test
            : new BinaryExpr(iteExpr.Test.tok, BinaryExpr.Opcode.And, accumulatedCondition, iteExpr.Test);

        // Add the "then" case
        var thenCase = new FunctionCase {
            Condition = currentCondition,
            IsBaseCase = !ExprContainsFunctionCall(iteExpr.Thn, functionName),
            RecursiveCall = FindRecursiveCall(iteExpr.Thn, functionName)
        };
        cases.Add(thenCase);

        if (iteExpr.Els != null) {
            if (iteExpr.Els is ITEExpr elseITEExpr) {
                // Use UnaryOpExpr for negating conditions and ensure consistent type
                Expression negatedCondition = new UnaryOpExpr(iteExpr.Test.tok, UnaryOpExpr.Opcode.Not, iteExpr.Test);
                Expression newAccumulatedCondition = accumulatedCondition == null
                    ? negatedCondition
                    : new BinaryExpr(iteExpr.Test.tok, BinaryExpr.Opcode.And, accumulatedCondition, negatedCondition);
                AnalyzeITEExpr(elseITEExpr, functionName, cases, newAccumulatedCondition);
            } else {
                // Handle the final "else" branch with explicit type wrapping
                Expression negatedCondition = new UnaryOpExpr(iteExpr.Test.tok, UnaryOpExpr.Opcode.Not, iteExpr.Test);
                Expression finalCondition = accumulatedCondition == null
                    ? negatedCondition
                    : new BinaryExpr(iteExpr.Test.tok, BinaryExpr.Opcode.And, accumulatedCondition, negatedCondition);

                var elseCase = new FunctionCase {
                    Condition = finalCondition,
                    IsBaseCase = !ExprContainsFunctionCall(iteExpr.Els, functionName),
                    RecursiveCall = FindRecursiveCall(iteExpr.Els, functionName)
                };
                cases.Add(elseCase);
            }
        }
    }

    private FunctionCallExpr? FindRecursiveCall(Expression expr, string functionName) {
      if (expr is FunctionCallExpr funcCallExpr && funcCallExpr.Name == functionName) {
        return funcCallExpr;
      }
      foreach (var subExpr in expr.SubExpressions) {
        var result = FindRecursiveCall(subExpr, functionName);
        if (result != null) {
          return result;
        }
      }
      return null;
    }

    private bool ExprContainsFunctionCall(Expression expr, string functionName) {
      if (expr is FunctionCallExpr funcCallExpr) {
        return funcCallExpr.Name == functionName;
      }
      foreach (var subExpr in expr.SubExpressions) {
        if (ExprContainsFunctionCall(subExpr, functionName)) {
          return true;
        }
      }
      return false;
    }

    private string PrintExpression(Expression expr) {
      return Printer.ExprToString(reporter.Options, expr);
    }

    private string GenerateStandardInductionProofSketch(Method method) {
      var sb = new StringBuilder();

      var inductionVariables = FindInductionVariables(method);
      if (inductionVariables.Count > 0) {
        var proofSketch = BuildProofSketch(method, inductionVariables);
        sb.Append(proofSketch);
      } else {
        sb.AppendLine("// No suitable induction variable found.");
      }

      return sb.ToString();
    }

    private List<IVariable> FindInductionVariables(Method method) {
      var inductionVariables = new List<IVariable>();

      if (method.Decreases.Expressions.Count > 0) {
        var decreasesExpr = method.Decreases.Expressions.First();
        var decreasesVar = GetVariableFromExpression(decreasesExpr);
        if (decreasesVar != null) {
          inductionVariables.Add(decreasesVar);
          return inductionVariables;
        }
      }

      foreach (var formal in method.Ins) {
        if (IsNatType(formal.Type) || formal.Type.IsDatatype) {
          inductionVariables.Add(formal);
          break;
        }
      }

      return inductionVariables;
    }

    private IVariable? GetVariableFromExpression(Expression expr) {
      if (expr.Resolved is IdentifierExpr idExpr) {
        return idExpr.Var;
      }
      return null;
    }

    private bool IsNatType(Type type) {
      var userDefinedType = type as UserDefinedType;
      return userDefinedType != null && userDefinedType.Name == "nat";
    }

    private string BuildProofSketch(Method method, List<IVariable> inductionVariables) {
      var sb = new StringBuilder();
      var inductionVar = inductionVariables[0];  // Assuming the first variable is the induction variable

      if (inductionVar.Type.IsDatatype) {
        sb.AppendLine($"{Indent(0)}// Structural induction on {inductionVar.Name}");
        sb.AppendLine(Indent(0));
        var datatypeDecl = inductionVar.Type.AsDatatype;
        sb.AppendLine($"{Indent(0)}match {inductionVar.Name} {{");

        foreach (var ctor in datatypeDecl.Ctors) {
          var formalParams = string.Join(", ", ctor.Formals.Select(f => f.Name));
          sb.AppendLine($"{Indent(1)}case {ctor.Name}({formalParams}) => {{");

          // Collect the recursive parameters (fields that are of the same datatype as the inductive variable)
          var recursiveFields = ctor.Formals
              .Where(f => f.Type.IsDatatype && f.Type.AsDatatype == inductionVar.Type.AsDatatype)
              .Select(f => f.Name);

          foreach (var recursiveField in recursiveFields) {
            sb.AppendLine(recursiveMethodCall(2, method, inductionVar, recursiveField));
          }

          sb.AppendLine($"{Indent(1)}}}");
        }

        sb.AppendLine($"{Indent(0)}}}");
      } else if (IsNatType(inductionVar.Type)) {
        sb.AppendLine($"{Indent(0)}// Natural induction on {inductionVar.Name}");
        sb.AppendLine();
        sb.AppendLine($"{Indent(0)}if ({inductionVar.Name} == 0) {{");
        sb.AppendLine($"{Indent(0)}}} else {{");
        sb.AppendLine(recursiveMethodCall(1, method, inductionVar, $"{inductionVar.Name} - 1"));
        sb.AppendLine($"{Indent(0)}}}");
      } else {
        sb.AppendLine(Indent(0)+"// Cannot generate induction proof sketch for this type.");
      }

      return sb.ToString();
    }

    private string methodParams(Method method, IVariable inductionVar, string decreasedArg) {
      return string.Join(", ", method.Ins.Select(param => {
        if (param == inductionVar) {
          return decreasedArg;
        }
        return param.Name;
      }));
    }

    private string recursiveMethodCall(int i, Method method, IVariable inductionVar, string decreasedArg) {
      return ($"{Indent(i)}{method.Name}({methodParams(method, inductionVar, decreasedArg)});");
    }
  }
}