using System.Threading.Tasks;
using System.Collections.Generic;

namespace Microsoft.Dafny {
  public interface ISketcher {
      public Task<SketchResponse> GenerateSketch(SketchRequest input);
      public static readonly List<string> Types =
        new List<string>(ProofSketcher.Types.Concat(new List<string> { "ai", "ai_abbrev", "ai_whole", "trigger", "fixer", "lemma_synth" }));
      public static ISketcher? Create(string sketchType, ErrorReporter reporter) {
        var proofSketcher = ProofSketcher.Create(sketchType, reporter);
        if (proofSketcher != null) {
          return proofSketcher;
        }
        if (sketchType == "ai") {
          return new LLMSketcher(false, reporter);
        }
        if (sketchType == "ai_abbrev") {
          return new LLMSketcher(true, reporter);
        }
        if (sketchType == "ai_whole") {
          return new LLMSketcherWhole(reporter);
        }
        if (sketchType == "trigger") {
          return new TriggeringSketcher(reporter);
        }
        if (sketchType == "fixer") {
          return new FixerSketcher(reporter);
        }
        if (sketchType == "lemma_synth") {
          return new LemmaSynthSketcher(reporter);
        }
        return null;
      }
  }
}