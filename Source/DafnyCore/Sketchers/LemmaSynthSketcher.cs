using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.Dafny {
  /// <summary>
  /// Fills in empty lemma bodies by asking an LLM, then inserting the result.
  /// Uses the lemma synthesis prompt format from formal-disco.
  /// </summary>
  public class LemmaSynthSketcher : ISketcher {
    private readonly VLLMChatClient _client;

    private const string SystemPrompt =
      "You are an expert Dafny programmer. You will be given a Dafny program " +
      "containing a lemma with an empty body. Your job is to fill in the lemma body " +
      "so that the program verifies.\n\n" +
      "Output ONLY the lemma body contents (the code that goes between the { and }). " +
      "Do not include the lemma signature, the braces themselves, or any explanation.\n\n" +
      "Wrap your code in a ```dafny code block.";

    public LemmaSynthSketcher(ErrorReporter reporter) {
      _client = new VLLMChatClient();
    }

    public async Task<SketchResponse> GenerateSketch(SketchRequest input) {
      var program = input.Content;
      var lineNumber = input.LineNumber;

      // Find the target lemma
      var lemmaName = FindTargetLemma(program, lineNumber);
      if (lemmaName == null) {
        return new SketchResponse("// No empty lemma found at cursor position or in file.");
      }

      // Get verification notes from prompt or additional data
      var notes = input.Prompt ?? "";
      if (string.IsNullOrWhiteSpace(notes) &&
          input.AdditionalData != null &&
          input.AdditionalData.TryGetValue("notes", out var notesObj)) {
        notes = notesObj?.ToString() ?? "";
      }

      var userPrompt = BuildUserPrompt(program, lemmaName, notes);

      var response = await _client.ChatComplete(SystemPrompt, userPrompt);

      // Extract body from response
      var body = ExtractBody(response);

      // Insert body into the program
      var filled = InsertLemmaBody(program, lemmaName, body);
      var sketch = TextDiff.ExtractMinimalSketch(program, filled);

      return new SketchResponse(sketch, new Dictionary<string, object> {
        { "lemma_name", lemmaName },
        { "generated_body", body },
        { "raw_response", response },
        { "full_program", filled }
      });
    }

    private string BuildUserPrompt(string program, string lemmaName, string notes) {
      return
        $"Program:\n{program}\n\n" +
        $"The lemma `{lemmaName}` has an empty body. " +
        "Fill in the body so the program verifies.\n\n" +
        $"Dafny verification output on the current program:\n{notes}\n\n" +
        "Output ONLY the body contents (without the surrounding braces).";
    }

    /// <summary>
    /// Find the target lemma name. If lineNumber is provided, find the lemma at that line.
    /// Otherwise, find the first lemma with an empty body.
    /// </summary>
    private string FindTargetLemma(string program, int? lineNumber) {
      var lines = program.Split('\n');

      // If we have a line number, find the lemma at/near that line
      if (lineNumber.HasValue && lineNumber.Value >= 0 && lineNumber.Value < lines.Length) {
        // Search backwards from cursor to find enclosing lemma
        for (int i = lineNumber.Value; i >= 0; i--) {
          var match = Regex.Match(lines[i], @"^\s*lemma\s+(\w+)");
          if (match.Success) {
            return match.Groups[1].Value;
          }
        }
      }

      // Fallback: find first lemma with empty body
      for (int i = 0; i < lines.Length; i++) {
        var match = Regex.Match(lines[i], @"^\s*lemma\s+(\w+)");
        if (match.Success) {
          var name = match.Groups[1].Value;
          if (HasEmptyBody(lines, i)) {
            return name;
          }
        }
      }

      // Last resort: return any lemma name
      for (int i = 0; i < lines.Length; i++) {
        var match = Regex.Match(lines[i], @"^\s*lemma\s+(\w+)");
        if (match.Success) {
          return match.Groups[1].Value;
        }
      }

      return null;
    }

    /// <summary>
    /// Check if a lemma starting at the given line has an empty body.
    /// </summary>
    private bool HasEmptyBody(string[] lines, int startLine) {
      int bodyStart = -1;
      for (int j = startLine; j < lines.Length; j++) {
        if (lines[j].Contains("{")) {
          bodyStart = j;
          break;
        }
      }
      if (bodyStart < 0) return false;

      // Check if the body is empty (only whitespace/comments between { and })
      int depth = 0;
      var bodyContent = "";
      for (int j = bodyStart; j < lines.Length; j++) {
        foreach (char c in lines[j]) {
          if (c == '{') depth++;
          else if (c == '}') {
            depth--;
            if (depth == 0) {
              // Strip comments from body content
              var stripped = Regex.Replace(bodyContent, @"//.*", "");
              stripped = Regex.Replace(stripped, @"/\*.*?\*/", "", RegexOptions.Singleline);
              return string.IsNullOrWhiteSpace(stripped);
            }
          }
        }
        if (j > bodyStart || (j == bodyStart && lines[j].IndexOf('{') < lines[j].Length - 1)) {
          var afterBrace = j == bodyStart
            ? lines[j].Substring(lines[j].IndexOf('{') + 1)
            : lines[j];
          bodyContent += afterBrace + "\n";
        }
      }
      return false;
    }

    /// <summary>
    /// Extract lemma body from LLM response, stripping markdown fences.
    /// </summary>
    private string ExtractBody(string response) {
      // Try to extract from code block
      var codeBlockPattern = @"(?s)```(?:[Dd]afny)?\s+(.*?)```";
      var match = Regex.Match(response, codeBlockPattern);
      string text = match.Success ? match.Groups[1].Value.Trim() : response.Trim();

      // Strip surrounding braces if present
      if (text.StartsWith("{") && text.EndsWith("}")) {
        text = text.Substring(1, text.Length - 2);
      }
      return text.Trim();
    }

    /// <summary>
    /// Insert a body into the named lemma. Port of formal-disco/tasks/lemma_synth.py insert_lemma_body().
    /// </summary>
    private string InsertLemmaBody(string program, string lemmaName, string body) {
      var lines = program.Split('\n');
      var result = new List<string>();
      int i = 0;

      while (i < lines.Length) {
        if (Regex.IsMatch(lines[i], $@"\s*lemma\s+{Regex.Escape(lemmaName)}\b")) {
          // Found the lemma — copy lines until we hit the opening brace
          while (i < lines.Length) {
            result.Add(lines[i]);
            if (lines[i].Contains("{")) {
              break;
            }
            i++;
          }
          i++;

          // Skip existing body content (find matching closing brace)
          int depth = 1;
          while (i < lines.Length) {
            foreach (char c in lines[i]) {
              if (c == '{') depth++;
              else if (c == '}') depth--;
            }
            if (depth <= 0) {
              // Insert body before closing brace
              if (!string.IsNullOrWhiteSpace(body)) {
                result.Add(body);
              }
              result.Add(lines[i]);
              i++;
              break;
            }
            i++;
          }
        } else {
          result.Add(lines[i]);
          i++;
        }
      }

      return string.Join("\n", result);
    }
  }
}
