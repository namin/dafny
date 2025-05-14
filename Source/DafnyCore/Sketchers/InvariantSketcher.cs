using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Microsoft.Dafny.DafnyLogger;
using static Microsoft.Dafny.VerifierCmd;

namespace Microsoft.Dafny {

  public class InvariantSketcher : ISketcher {
    private readonly ErrorReporter reporter;
    private readonly LLMSketcher sketcher;

    public InvariantSketcher(ErrorReporter reporter) {
      this.reporter = reporter;
      this.sketcher = new LLMSketcher(false/*abbrev*/, reporter);
    }

    // TODO: extend the interface of ProofSketcher so this can be included?
    // TODO: if we had character position too, we could indent the resulting sketch properly.
    public virtual async Task<SketchResponse> GenerateSketch(SketchRequest input) {
        var content = input.Content;
        var response = await sketcher.GenerateSketch(input.withPrompt("Find the program invariants. List all potential invariants in a Dafny code block, one per line."));
        var suggestions = response.Sketch;
        var newContent = InsertSketchAtLine(content, suggestions, input.LineNumber.Value);
        var output = await VerifyDafnyProgram(newContent, input.Method.Name);
        var badLines = FindBadLines(output); // TODO: we might want to also consider each invariant separately?
        var keptSuggestions = suggestions.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        for (int i=0; i<keptSuggestions.Length; i++) {
            if (badLines.Contains(1 + i + input.LineNumber.Value)) {
                Log("### Bad lines contain " + keptSuggestions[i]);
                keptSuggestions[i] = "// " + keptSuggestions[i];
            }
        }
        var sketch = string.Join("\n", keptSuggestions);;
        return new SketchResponse(sketch);
    }
  }
}