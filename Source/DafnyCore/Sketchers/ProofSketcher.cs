using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Dafny;
public abstract class ProofSketcher : InductionRewriter, ISketcher {
  public ProofSketcher(ErrorReporter reporter) : base(reporter) {
  }
  public abstract Task<SketchResponse> GenerateSketch(SketchRequest input);
  public static readonly List<string> Types = new List<string> { "induction", "induction_search", "inductive_explorer", "shallow_induction", "shallow_induction_search", "counterexamples", "invariants", "conclusions", "assertions", "call_lemma" };

  public static ProofSketcher? Create(string sketchType, ErrorReporter reporter)
  {
    switch (sketchType.ToLower()) {
      case "induction":
        return new InductiveProofSketcher(reporter);
      case "induction_search":
        return new InductiveProofSearchSketcher(reporter, false/*explorer*/, false/*shallow*/);
      case "inductive_explorer":
        return new InductiveProofSearchSketcher(reporter, true/*explorer*/, false/*shallow*/);
      case "shallow_induction":
        return new InductiveProofSketcher(reporter, true/*shallow*/);
      case "shallow_induction_search":
        return new InductiveProofSearchSketcher(reporter, false/*explorer*/, true/*shallow*/);
      case "invariants":
        return new InvariantSketcher(reporter);
      case "conclusions":
        return new ConclusionSketcher(reporter);
      case "assertions":
        return new ConditionAssertionProofSketcher(reporter);
      case "counterexamples":
        return new CounterexampleSketcher(reporter);
      case "call_lemma":
        return new LemmaCallSketcher(reporter);
      default:
        return null;
    }
  }
  protected string Indent(int level) => new string(' ', (level + 1) * 4);
}