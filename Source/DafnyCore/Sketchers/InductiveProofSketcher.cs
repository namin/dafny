using System.Collections.Generic;
using Microsoft.Dafny;
using System.Text;
using System.Linq;

namespace Microsoft.Dafny;

public class InductiveProofSketcher : ProofSketcher {
  public InductiveProofSketcher(ErrorReporter reporter) : base(reporter) {
  }
  override public string GenerateProofSketch(Method method, int lineNumber) {
    return GenerateProofSketch(method);
  }
  /// <summary>
  /// Generates a Dafny code proof sketch for a given method using induction.
  /// </summary>
  /// <param name="method">The method to generate the proof sketch for.</param>
  /// <returns>A string containing the generated Dafny code for the proof sketch.</returns>
  public string GenerateProofSketch(Method method) {
    // Now we can access protected methods from InductionRewriter
    ProcessMethodExpressions(method); // Access inherited method

    // Find induction variables
    var inductionVariables = FindInductionVariables(method);

    if (inductionVariables.Count == 0) {
      return "No induction variables found for the given method.";
    }

    // Build and return the Dafny code proof sketch
    var proofSketch = BuildProofSketch(method, inductionVariables);
    return proofSketch;
  }

  private bool isNatType(Type type) {
    var userDefinedType = type as UserDefinedType;
    if (userDefinedType != null && userDefinedType.Name == "nat") {
      return true;
    }
    return false;
  }
  /// <summary>
  /// Identifies induction variables in the method's input parameters.
  /// </summary>
  /// <param name="method">The method to analyze.</param>
  /// <returns>List of variables suitable for induction.</returns>
  public List<IVariable> FindInductionVariables(Method method) {
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
          if (isNatType(formal.Type) || formal.Type.IsDatatype) {
              inductionVariables.Add(formal);
              break; // For now, pick the first suitable variable; can be improved later
          }
      }

      return inductionVariables;
  }

  // Helper function to extract the variable from the decreases expression (if applicable)
  private IVariable GetVariableFromExpression(Expression expr) {
      if (expr.Resolved is IdentifierExpr idExpr) {
          return idExpr.Var; // Return the variable bound to the identifier
      }
      // Handle other potential cases for expressions in the decreases clause, if necessary
      return null;
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
          sb.AppendLine($"{Indent(0)}match {inductionVar.Name} {{");

          foreach (var ctor in datatypeDecl.Ctors) {
              var formalParams = string.Join(", ", ctor.Formals.Select(f => f.Name));
              sb.AppendLine($"{Indent(1)}case {ctor.Name}({formalParams}) => {{");

              // Collect the recursive parameters (fields that are of the same datatype as the inductive variable)
              var recursiveFields = ctor.Formals
                  .Where(f => f.Type.IsDatatype && f.Type.AsDatatype == inductionVar.Type.AsDatatype)
                  .Select(f => f.Name);

              foreach (var recursiveField in recursiveFields) {
                sb.AppendLine(recursiveMethodCall(method, inductionVar, recursiveField));
              }

              sb.AppendLine($"{Indent(1)}}}");
          }

          sb.AppendLine($"{Indent(0)}}}");
      } else if (isNatType(inductionVar.Type)) {
          sb.AppendLine($"{Indent(0)}if ({inductionVar.Name} == 0) {{");
          sb.AppendLine($"{Indent(1)}// Base case for {inductionVar.Name}");
          sb.AppendLine($"{Indent(0)}}} else {{");
          sb.AppendLine(recursiveMethodCall(method, inductionVar, $"{inductionVar.Name} - 1"));
          sb.AppendLine($"{Indent(0)}}}");
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
    return ($"{Indent(2)}{method.Name}({methodParams(method, inductionVar, decreasedArg)});");
  }
}

