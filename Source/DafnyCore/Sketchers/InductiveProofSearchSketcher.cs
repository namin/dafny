using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Dafny;

namespace Microsoft.Dafny {

  public class InductiveProofSearchSketcher : ProofSketcher {
    private readonly ErrorReporter reporter;
    private readonly InductiveProofSketcher inductiveProofSketcher;

    public InductiveProofSearchSketcher(ErrorReporter reporter) : base(reporter) {
      this.reporter = reporter;
      this.inductiveProofSketcher = new InductiveProofSketcher(reporter);
    }

    public override string GenerateProofSketch(Program program, Method method, int? lineNumber) {
      if (method == null) {
        return "// Error: No method resolved.";
      }
      var requiresCalls = inductiveProofSketcher.AllRequiresCalls(method).Select(item => item.Item1);
      var vars = inductiveProofSketcher.FindInductionVariables(method);
      var sketches = new List<string>();
      foreach (var requireCall in requiresCalls) {
        sketches.Add(inductiveProofSketcher.GenerateFunctionBasedInductionProofSketch(method, requireCall));
      }
      foreach (var inductionVar in vars) {
        sketches.Add(inductiveProofSketcher.BuildProofSketch(method, inductionVar));
      }
      return string.Join("\n\n", sketches);
    }
  }
}