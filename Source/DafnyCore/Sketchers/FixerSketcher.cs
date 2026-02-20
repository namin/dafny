using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.Dafny {
  /// <summary>
  /// Repairs broken Dafny programs by asking an LLM for a diff and applying it.
  /// Uses the repair prompt format from formal-disco.
  /// </summary>
  public class FixerSketcher : ISketcher {
    private readonly VLLMChatClient _client;

    // Example texts for the diff format (from formal-disco/patch.py)
    private const string ExampleBefore = "hello\nworld\na\nline2\nline3\n";
    private const string ExampleDiff = "@@\n= a\n+ inserted-after-a\n@@\n- line2\n= line3\n+ after3";
    private const string ExampleAfter = "hello\nworld\na\ninserted-after-a\nline3\nafter3\n";

    public FixerSketcher(ErrorReporter reporter) {
      _client = new VLLMChatClient();
    }

    public async Task<SketchResponse> GenerateSketch(SketchRequest input) {
      var program = input.Content;

      // Get verification errors from prompt or additional data
      var notes = input.Prompt ?? "";
      if (string.IsNullOrWhiteSpace(notes) &&
          input.AdditionalData != null &&
          input.AdditionalData.TryGetValue("notes", out var notesObj)) {
        notes = notesObj?.ToString() ?? "";
      }

      var systemPrompt = BuildSystemPrompt();
      var userPrompt = BuildUserPrompt(program, notes);

      var response = await _client.ChatComplete(systemPrompt, userPrompt);

      // Extract diff from response (strip markdown fences if present)
      var diff = ExtractDiff(response);

      // Apply diff to get repaired program
      try {
        var repaired = TextDiff.ApplyTextDiff(program, diff);
        var sketch = TextDiff.ExtractMinimalSketch(program, repaired);
        return new SketchResponse(sketch, new Dictionary<string, object> {
          { "diff", diff },
          { "raw_response", response },
          { "full_program", repaired }
        });
      } catch (Exception ex) {
        // If diff application fails, return the raw diff so user can see what the model produced
        return new SketchResponse(
          $"// Failed to apply diff: {ex.Message}\n// Raw model output:\n{response}");
      }
    }

    private string BuildSystemPrompt() {
      return
        "You are an expert Dafny developer. You will be given a Dafny program that has errors " +
        "pointed out by Dafny. These errors can be syntactic, or failures to verify the program " +
        "(i.e., prove post-conditions or verify current assertions/invariants).\n" +
        "Your job is to repair these errors by emitting a DIFF in a simple, line-based format.\n\n" +
        "Diff format:\n" +
        "- Lines starting with '@@' are anchors (search-forward markers). These don't modify the program, " +
        "but just start a new 'block' of changes in your patch.\n" +
        "- Lines starting with '=' keep that exact line: find it forward and advance the cursor. " +
        "You typically only need a few of these after your @@ line to position the cursor for the " +
        "actual changes: you don't need to copy much of the original file.\n" +
        "- Lines starting with '-' delete that exact line found forward.\n" +
        "- Lines starting with '+' add a new line at the current cursor.\n\n" +
        "- All diff lines should start with one of the special characters above and a space following them. " +
        "Other lines will be completely ignored\n" +
        "Here is an example of a diff:\n\n" +
        $"Text before:\n{ExampleBefore}\n\n" +
        $"Example of model output (diff in the format you must follow):\n{ExampleDiff}\n\n" +
        $"Text after:\n{ExampleAfter}";
    }

    private string BuildUserPrompt(string program, string notes) {
      return
        $"Program:\n{program}\n\n" +
        $"Notes (verification output):\n{notes}\n\n" +
        "Your goal is to fix the errors shown above by Dafny. Note that fixing these errors might " +
        "require various kinds of changes, such as fixing the syntax, fixing the implementation of a " +
        "method or function, adding new logical annotations (e.g., assertions, invariants, " +
        "decreases/increases clauses, etc), introducing new lemmas that help prove existing " +
        "assertions, or other changes.\n";
    }

    private string ExtractDiff(string response) {
      // Try to extract from ```dafny or ``` code block
      var codeBlockPattern = @"(?s)```(?:[Dd]afny)?\s+(.*?)```";
      var match = Regex.Match(response, codeBlockPattern);
      if (match.Success) {
        return match.Groups[1].Value.Trim();
      }

      // If no code block, return the whole response (it might be raw diff)
      return response.Trim();
    }
  }
}
