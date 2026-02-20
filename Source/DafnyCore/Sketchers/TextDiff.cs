using System;
using System.Collections.Generic;

namespace Microsoft.Dafny {
  /// <summary>
  /// Applies a simple line-based diff to text.
  ///
  /// Directives (one per line in the diff):
  ///   - Lines starting and ending with "@@" are anchors: search forward for that line.
  ///   - Lines starting with '=' are keep: search forward for that line and advance.
  ///   - Lines starting with '-' are delete: search forward for that line and remove it.
  ///   - Lines starting with '+' are add: insert that line at the current cursor.
  ///
  /// </summary>
  public static class TextDiff {
    public static string ApplyTextDiff(string text, string diff) {
      var lines = new List<string>(text.Split('\n'));
      int cursor = 0;

      foreach (var raw in diff.Split('\n')) {
        if (string.IsNullOrEmpty(raw)) {
          continue;
        }

        // Anchor lines
        if (raw.StartsWith("@@") && raw.EndsWith("@@")) {
          var anchor = raw.Substring(2, raw.Length - 4);
          if (!string.IsNullOrEmpty(anchor)) {
            var j = FindForward(lines, anchor, cursor);
            if (j.HasValue) {
              cursor = j.Value + 1;
            }
          }
          continue;
        }

        char op = raw[0];
        string payload = raw.Substring(1);
        if (payload.StartsWith(" ")) {
          payload = payload.Substring(1);
        }

        switch (op) {
          case '=': {
            var j = FindForward(lines, payload, cursor);
            if (j.HasValue) {
              cursor = j.Value + 1;
            }
            break;
          }
          case '-': {
            var j = FindForward(lines, payload, cursor);
            if (j.HasValue) {
              lines.RemoveAt(j.Value);
              cursor = j.Value;
            }
            break;
          }
          case '+': {
            lines.Insert(cursor, payload);
            cursor += 1;
            break;
          }
          default:
            // Ignore unrecognized lines
            break;
        }
      }

      return string.Join("\n", lines);
    }

    /// <summary>
    /// Compare original and patched text. If the only changes are a single
    /// contiguous block of added lines (no deletions or edits elsewhere),
    /// return just those lines. Otherwise return the full patched text.
    /// </summary>
    public static string ExtractMinimalSketch(string original, string patched) {
      var origLines = original.Split('\n');
      var patchLines = patched.Split('\n');

      // Find first differing line from the top
      int top = 0;
      while (top < origLines.Length && top < patchLines.Length
             && origLines[top] == patchLines[top]) {
        top++;
      }

      // Find first differing line from the bottom
      int origBot = origLines.Length - 1;
      int patchBot = patchLines.Length - 1;
      while (origBot >= top && patchBot >= top
             && origLines[origBot] == patchLines[patchBot]) {
        origBot--;
        patchBot--;
      }

      // Check if this is effectively a contiguous addition.
      // Either no original lines were touched (origBot < top), or
      // the only original lines in the diff region are blank/whitespace
      // (e.g. an empty lemma body being filled in).
      bool isPureAddition = origBot < top;
      if (!isPureAddition && patchBot >= top) {
        isPureAddition = true;
        for (int i = top; i <= origBot; i++) {
          if (!string.IsNullOrWhiteSpace(origLines[i])) {
            isPureAddition = false;
            break;
          }
        }
      }

      if (isPureAddition && patchBot >= top) {
        var added = new List<string>();
        for (int i = top; i <= patchBot; i++) {
          added.Add(patchLines[i]);
        }
        return string.Join("\n", added);
      }

      // Otherwise the change involves deletions or replacements — return whole file
      return patched;
    }

    private static int? FindForward(List<string> lines, string target, int start) {
      for (int i = start; i < lines.Count; i++) {
        if (lines[i].TrimEnd('\r') == target) {
          return i;
        }
      }
      return null;
    }
  }
}
