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

    sb.AppendLine("// Function-based induction proof sketch");
    sb.AppendLine($"// Prove {method.Name} using induction on function {functionCallExpr.Name}");
    sb.AppendLine();

    var function = functionCallExpr.Function;
    if (function == null || function.Body == null) {
        sb.AppendLine("// Unable to retrieve function definition.");
        return sb.ToString();
    }

    // Analyze the function to get cases
    var cases = GetFunctionCases(function);

    if (cases.Count == 0) {
        sb.AppendLine("// No cases found in function definition.");
        return sb.ToString();
    }

    bool firstCase = true;
    foreach (var functionCase in cases) {
        var condition = functionCase.Condition != null ? PrintExpression(functionCase.Condition) : "true";
        if (firstCase) {
            sb.Append($"if ({condition}) {{\n");
            firstCase = false;
        } else {
            sb.Append($"}} else if ({condition}) {{\n");
        }

        if (functionCase.IsBaseCase) {
            sb.AppendLine("    // Base case:");
            sb.AppendLine("    // Prove base case here.");
        } else {
            sb.AppendLine("    // Inductive case:");
            sb.AppendLine();
            // Generate recursive lemma invocation with decreased arguments
            var decreasedArgs = GetDecreasedArguments(functionCase.RecursiveCall);
            sb.AppendLine($"    {method.Name}({string.Join(", ", decreasedArgs)});");
            sb.AppendLine();
            sb.AppendLine("    // Prove inductive step here.");
        }
        // Do not append closing brace here; it will be appended before the next else if
    }

    // Close the last if/else if block
    sb.AppendLine("} else {");
    sb.AppendLine("    // Other cases if any.");
    sb.AppendLine("}");
    // Close the final brace
    sb.AppendLine("}");

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
            } else {
                // Handle other types of expressions if necessary
            }
            return cases;
        }

private void AnalyzeITEExpr(ITEExpr iteExpr, string functionName, List<FunctionCase> cases) {
    var thenCase = new FunctionCase {
        Condition = iteExpr.Test,
        IsBaseCase = !ExprContainsFunctionCall(iteExpr.Thn, functionName),
        RecursiveCall = FindRecursiveCall(iteExpr.Thn, functionName)
    };
    cases.Add(thenCase);

    if (iteExpr.Els != null) {
        if (iteExpr.Els is ITEExpr elseITEExpr) {
            AnalyzeITEExpr(elseITEExpr, functionName, cases);
        } else {
            // Manually create the condition 'n >= 2'
            var nIdentifier = new IdentifierExpr(iteExpr.Test.tok, "n");
            var twoLiteral = new LiteralExpr(iteExpr.Test.tok, 2);
            var condition = new BinaryExpr(iteExpr.Test.tok, BinaryExpr.Opcode.Ge, nIdentifier, twoLiteral);

            var elseCase = new FunctionCase {
                Condition = condition,
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

        private List<string> GetDecreasedArguments(FunctionCallExpr? recursiveCall) {
            var decreasedArgs = new List<string>();
            if (recursiveCall != null) {
                foreach (var arg in recursiveCall.Args) {
                    decreasedArgs.Add(PrintExpression(arg));
                }
            }
            return decreasedArgs;
        }

        private bool ExprContainsFunctionCall(Expression expr, string functionName) {
            if (expr is FunctionCallExpr funcCallExpr) {
                if (funcCallExpr.Name == functionName) {
                    return true;
                }
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
            sb.AppendLine("// Standard induction proof sketch");

            // Find induction variables
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

            // Look for a user-specified induction variable in the decreases clause
            if (method.Decreases.Expressions.Count > 0) {
                var decreasesExpr = method.Decreases.Expressions.First(); // Assuming the first expression is relevant
                var decreasesVar = GetVariableFromExpression(decreasesExpr);
                if (decreasesVar != null) {
                    inductionVariables.Add(decreasesVar);
                    return inductionVariables; // Use this as the induction variable if found
                }
            }

            // Fallback: Iterate over the parameters and look for the default induction variable (e.g., nat or datatype)
            foreach (var formal in method.Ins) {
                if (IsNatType(formal.Type) || formal.Type.IsDatatype) {
                    inductionVariables.Add(formal);
                    break; // For now, pick the first suitable variable; can be improved later
                }
            }

            return inductionVariables;
        }

        private IVariable? GetVariableFromExpression(Expression expr) {
            if (expr.Resolved is IdentifierExpr idExpr) {
                return idExpr.Var; // Return the variable bound to the identifier
            }
            // Handle other potential cases for expressions in the decreases clause, if necessary
            return null;
        }

  private bool IsNatType(Type type) {
    var userDefinedType = type as UserDefinedType;
    if (userDefinedType != null && userDefinedType.Name == "nat") {
      return true;
    }
    return false;
  }

        private string BuildProofSketch(Method method, List<IVariable> inductionVariables) {
            var sb = new StringBuilder();
            var inductionVar = inductionVariables[0];  // Assuming the first variable is the induction variable

            if (inductionVar.Type.IsDatatype) {
                var datatypeDecl = inductionVar.Type.AsDatatype;
                sb.AppendLine($"match {inductionVar.Name} {{");

                foreach (var ctor in datatypeDecl.Ctors) {
                    var formalParams = string.Join(", ", ctor.Formals.Select(f => f.Name));
                    sb.AppendLine($"  case {ctor.Name}({formalParams}) => {{");

                    // Collect the recursive parameters (fields that are of the same datatype as the inductive variable)
                    var recursiveFields = ctor.Formals
                        .Where(f => f.Type.IsDatatype && f.Type.AsDatatype == inductionVar.Type.AsDatatype)
                        .Select(f => f.Name);

                    foreach (var recursiveField in recursiveFields) {
                        sb.AppendLine(recursiveMethodCall(method, inductionVar, recursiveField));
                    }

                    sb.AppendLine($"    // Prove case for {ctor.Name}");
                    sb.AppendLine($"  }}");
                }

                sb.AppendLine($"}}");
            } else if (IsNatType(inductionVar.Type)) {
                sb.AppendLine($"if ({inductionVar.Name} == 0) {{");
                sb.AppendLine($"  // Base case for {inductionVar.Name}");
                sb.AppendLine($"}} else {{");
                sb.AppendLine(recursiveMethodCall(method, inductionVar, $"{inductionVar.Name} - 1"));
                sb.AppendLine($"  // Prove inductive step here.");
                sb.AppendLine($"}}");
            } else {
                sb.AppendLine("// Cannot generate induction proof sketch for this type.");
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

        private string recursiveMethodCall(Method method, IVariable inductionVar, string decreasedArg) {
            return ($"  {method.Name}({methodParams(method, inductionVar, decreasedArg)});");
        }
    }
}