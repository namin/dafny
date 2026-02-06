using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Dafny;
public abstract class ProofSketcher : InductionRewriter, ISketcher {
  public ProofSketcher(ErrorReporter reporter) : base(reporter) {
  }
  public abstract Task<SketchResponse> GenerateSketch(SketchRequest input);
  public static readonly List<string> Types = new List<string> {"counterexamples", "induction", "induction_search", "inductive_explorer", "shallow_induction", "shallow_induction_search",  "case", "case_search", "case_explorer", "shallow_case", "shallow_case_search", "invariants", "conclusions", "assertions", "call_lemma" };

  public static ProofSketcher? Create(string sketchType, ErrorReporter reporter)
  {
    switch (sketchType.ToLower()) {
      case "induction":
        return new InductiveProofSketcher(reporter);
      case "induction_search":
        return new InductiveProofSearchSketcher(reporter, false/*explorer*/, true/*induction*/, false/*shallow*/);
      case "inductive_explorer":
        return new InductiveProofSearchSketcher(reporter, true/*explorer*/, true/*induction*/, false/*shallow*/);
      case "shallow_induction":
        return new InductiveProofSketcher(reporter, true/*induction*/, true/*shallow*/);
      case "shallow_induction_search":
        return new InductiveProofSearchSketcher(reporter, false/*explorer*/, true/*induction*/, true/*shallow*/);
      case "case":
        return new InductiveProofSketcher(reporter, false/*induction*/, false/*shallow*/);
      case "case_search":
        return new InductiveProofSearchSketcher(reporter, false/*explorer*/, false/*induction*/, false/*shallow*/);
      case "case_explorer":
        return new InductiveProofSearchSketcher(reporter, true/*explorer*/, false/*induction*/, false/*shallow*/);
      case "shallow_case":
        return new InductiveProofSketcher(reporter, false/*induction*/, true/*shallow*/);
      case "case_induction_search":
        return new InductiveProofSearchSketcher(reporter, false/*explorer*/, false/*induction*/, true/*shallow*/);
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