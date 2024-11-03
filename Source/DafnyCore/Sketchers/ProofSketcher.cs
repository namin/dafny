namespace Microsoft.Dafny;
public abstract class ProofSketcher : InductionRewriter {
  public ProofSketcher(ErrorReporter reporter) : base(reporter) {
  }
  public abstract string GenerateProofSketch(Method method, int lineNumber);
  protected string Indent(int level) => new string(' ', (level + 1) * 4);
}