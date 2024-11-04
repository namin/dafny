using System.Collections.Generic;

namespace Microsoft.Dafny;
public abstract class ProofSketcher : InductionRewriter {
  public ProofSketcher(ErrorReporter reporter) : base(reporter) {
  }
  public abstract string GenerateProofSketch(Method method, int lineNumber);
  public static readonly List<string> Types = new List<string> { "induction", "assertions" };

  public static ProofSketcher? Create(string sketchType, ErrorReporter reporter)
  {
    switch (sketchType.ToLower()) {
      case "induction":
        return new InductiveProofSketcher(reporter);
      case "assertions":
        return new ConditionAssertionProofSketcher(reporter);
      default:
        return null;
    }
  }
  protected string Indent(int level) => new string(' ', (level + 1) * 4);
}