using System;
using System.Collections.Generic;
using Microsoft.Dafny;
using System.Text;
using System.Linq;

namespace Microsoft.Dafny;

public class InductiveProofSketcher : InductionRewriter {
  public InductiveProofSketcher(ErrorReporter reporter) : base(reporter) {
    // Inherits from InductionRewriter, can access protected methods
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

    // Iterate over the parameters
    foreach (var formal in method.Ins) {
        Console.WriteLine($"Checking if {formal.Name} is a candidate for induction...");

        // Check if the parameter is a nat (non-negative integer)
        if (isNatType(formal.Type)) {
            Console.WriteLine($"{formal.Name} is a candidate for induction (Type: nat)");
            inductionVariables.Add(formal);
        }

        // If the parameter is a datatype, it's also a candidate for induction
        else if (formal.Type.IsDatatype) {
            Console.WriteLine($"{formal.Name} is a candidate for induction (Type: {formal.Type})");
            inductionVariables.Add(formal);
        }
    }

    // Iterate over the method's preconditions (Req) to find potential induction variables
    foreach (var precondition in method.Req) {
        foreach (var inputVar in method.Ins) {
            // Check if the input variable occurs in a recursive function call
            if (InductionHeuristic.VarOccursInArgumentToRecursiveFunction(Reporter.Options, precondition.E, inputVar)) {
                Console.WriteLine($"{inputVar.Name} occurs in a recursive function in the precondition.");
                inductionVariables.Add(inputVar);
            }
        }
    }

    if (inductionVariables.Count == 0) {
        Console.WriteLine("No induction variables found for the given method.");
    }

    return inductionVariables;
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

    // Generate the lemma header
    sb.AppendLine($"lemma {method.Name}_Induction(");
    sb.AppendLine($"  {inductionVar.Name}: {inductionVar.Type.ToString()}");
    sb.AppendLine(")");
    
    // Add the ensures clause based on the method's postconditions
    sb.AppendLine("  ensures");
    foreach (var postcondition in method.Ens) {
      sb.AppendLine($"    {postcondition.E.ToString()},");
    }
    sb.AppendLine("{");

    // Check if the induction variable is a datatype
    if (inductionVar.Type.IsDatatype) {
        // Handle each constructor of the datatype
        var datatypeDecl = (DatatypeDecl)inductionVar.Type.AsDatatype;  // Cast to DatatypeDecl to access constructors
        sb.AppendLine($"  match {inductionVar.Name} {{");
        
        foreach (var ctor in datatypeDecl.Ctors) {
            sb.AppendLine($"    case {ctor.Name}({string.Join(", ", ctor.Formals.Select(f => f.Name))}) => {{");
            sb.AppendLine($"      // Handle case for {ctor.Name}");
            sb.AppendLine($"      assert TestMethod({inductionVar.Name});  // Prove for case {ctor.Name}");
            sb.AppendLine("    }");
        }

        sb.AppendLine("  }");
    } else {
        // Add the base case for non-datatype (e.g., nat)
        sb.AppendLine($"  if {inductionVar.Name} == 0 {{");
        sb.AppendLine($"    assert {method.Name}({inductionVar.Name});  // Base case");
        sb.AppendLine("  } else {");
        sb.AppendLine($"    {method.Name}_Induction({inductionVar.Name} - 1);  // Inductive hypothesis");
        sb.AppendLine($"    assert {method.Name}({inductionVar.Name});  // Prove for {inductionVar.Name}");
        sb.AppendLine("  }");
    }

    sb.AppendLine("}");

    // Return the generated proof sketch
    return sb.ToString();
  }
}

