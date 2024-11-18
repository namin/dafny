using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Dafny;
using System.Linq;

namespace Microsoft.Dafny {

    public class InductiveProofSketcher : ProofSketcher {
        public InductiveProofSketcher(ErrorReporter reporter) : base(reporter) {
        }

        public override string GenerateProofSketch(Method method, int lineNumber) {
            // Decide whether to use function-based induction based on the requires clause
            if (RequiresCallsFunction(method)) {
                return GenerateFunctionBasedInductionProofSketch(method);
            } else {
                return GenerateStandardInductionProofSketch(method);
            }
        }

        private bool RequiresCallsFunction(Method method) {
            foreach (var req in method.Req) {
                if (ExprContainsFunctionCall(req.E)) {
                    return true;
                }
            }
            return false;
        }

        private bool ExprContainsFunctionCall(Expression expr) {
            if (expr is FunctionCallExpr) {
                return true;
            }
            foreach (var subExpr in expr.SubExpressions) {
                if (ExprContainsFunctionCall(subExpr)) {
                    return true;
                }
            }
            return false;
        }

        private string GenerateFunctionBasedInductionProofSketch(Method method) {
            var sb = new StringBuilder();

            sb.AppendLine("// Function-based induction proof sketch");
            sb.AppendLine($"// Prove {method.Name} using induction on function calls");

            // Get the function called in the requires clause
            var functionCallExpr = FindFunctionCallInRequires(method);
            if (functionCallExpr != null) {
                var functionName = functionCallExpr.Name;

                sb.AppendLine($"Assume {functionName} holds for smaller inputs;");
                sb.AppendLine($"Prove {functionName} holds for the current input based on its definition;");
            } else {
                sb.AppendLine("// Unable to identify the function for induction");
            }

            return sb.ToString();
        }

        private FunctionCallExpr? FindFunctionCallInRequires(Method method) {
            foreach (var req in method.Req) {
                var funcCall = FindFunctionCallExpr(req.E);
                if (funcCall != null) {
                    return funcCall;
                }
            }
            return null;
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

        /// <summary>
        /// Identifies induction variables in the method's input parameters.
        /// </summary>
        /// <param name="method">The method to analyze.</param>
        /// <returns>List of variables suitable for induction.</returns>
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

        // Helper function to extract the variable from the decreases expression (if applicable)
        private IVariable? GetVariableFromExpression(Expression expr) {
            if (expr.Resolved is IdentifierExpr idExpr) {
                return idExpr.Var; // Return the variable bound to the identifier
            }
            // Handle other potential cases for expressions in the decreases clause, if necessary
            return null;
        }

        private bool IsNatType(Type type) {
            var userDefinedType = type.AsNewtype;
            if (userDefinedType != null && userDefinedType.Name == "nat") {
                return true;
            }
            return type.IsBigOrdinalType || type.IsNumericBased(Type.NumericPersuasion.Int);
        }

        /// <summary>
        /// Builds the Dafny proof sketch for base and recursive cases based on induction variables.
        /// </summary>
        /// <param name="method">The method being analyzed.</param>
        /// <param name="inductionVariables">The list of variables for which to generate the proof sketch.</param>
        /// <returns>The proof sketch as a Dafny code string.</returns>
        private string BuildProofSketch(Method method, List<IVariable> inductionVariables) {
            var sb = new StringBuilder();
            var inductionVar = inductionVariables[0];  // Assuming the first variable is the induction variable

            if (inductionVar.Type.IsDatatype) {
                var datatypeDecl = (DatatypeDecl)inductionVar.Type.AsDatatype;
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

                    sb.AppendLine($"  }}");
                }

                sb.AppendLine($"}}");
            } else if (IsNatType(inductionVar.Type)) {
                sb.AppendLine($"if ({inductionVar.Name} == 0) {{");
                sb.AppendLine($"  // Base case for {inductionVar.Name}");
                sb.AppendLine($"}} else {{");
                sb.AppendLine(recursiveMethodCall(method, inductionVar, $"{inductionVar.Name} - 1"));
                sb.AppendLine($"}}");
            } else {
                sb.AppendLine("// Cannot generate induction proof sketch for this type.");
            }

            // Return the generated proof sketch
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