using System.Threading.Tasks;
using System.Collections.Generic;

namespace Microsoft.Dafny {
  public interface ISketcher {
      public Task<SketchResponse> GenerateSketch(SketchRequest input);
      public static readonly List<string> Types =
        new List<string>(ProofSketcher.Types.Concat(new List<string> { "ai" }));      
      public static ISketcher? Create(string sketchType, ErrorReporter reporter) {
        var proofSketcher = ProofSketcher.Create(sketchType, reporter);
        if (proofSketcher != null) {
          return proofSketcher;
        }
        if (sketchType == "ai") {
          return new LLMClient(reporter);
        }
        return null;
      }
  }
}