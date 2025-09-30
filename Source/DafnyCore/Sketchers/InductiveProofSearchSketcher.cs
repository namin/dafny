using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Microsoft.Dafny.DafnyLogger;
using static Microsoft.Dafny.VerifierCmd;

namespace Microsoft.Dafny {

  public class InductiveProofSearchSketcher : ProofSketcher {
    private readonly ErrorReporter reporter;
    private readonly InductiveProofSketcher inductiveProofSketcher;
    private readonly bool explorer;

    public InductiveProofSearchSketcher(ErrorReporter reporter, bool explorer) : base(reporter) {
      this.reporter = reporter;
      this.explorer = explorer;
      this.inductiveProofSketcher = new InductiveProofSketcher(reporter);
    }

    public override async Task<SketchResponse> GenerateSketch(SketchRequest input) {
      Program program = input.ResolvedProgram;
      Method method = input.Method;
      int? lineNumber = input.LineNumber;
      
      if (method == null) {
        return new SketchResponse("// Error: No method resolved.");
      }
      string programText = PrintProgramToString(program);
      Log("## Program text");
      Log(programText);
      var lineNo = FindInsertionLine(programText, method);
      if (lineNo < 0) {
        return new SketchResponse("// Cannot find method");
      }
      var allCalls = inductiveProofSketcher.AllCalls(method).Select(item => item.Item1).Distinct().ToList();
      var vars = inductiveProofSketcher.FindInductionVariables(method).Distinct().ToList();
      var sketches = new List<(string, Method, List<int>)>();
      foreach (var call in allCalls) {
        await considerSketchMetric(sketches, programText, method.Name, lineNo,
            inductiveProofSketcher.GenerateFunctionBasedInductionProofSketch(method, call));
      }
      foreach (var inductionVar in vars) {
        await considerSketchMetric(sketches, programText, method.Name, lineNo,
            inductiveProofSketcher.BuildProofSketch(method, inductionVar));
      }
      sketches = sketches.Where(s => s.Item2 != null).ToList();
      var sketchesByCount= sketches.OrderBy(x => (x.Item3.Count, x.Item1.Length)).ToList();
      var maxDepth = sketches.Min(s => findDepth(s.Item2.Body));
      var orderedSketches = sketches.OrderBy(x => Metric(maxDepth, x.Item1, x.Item2, x.Item3));
      var exploreMsg = string.Join("\n\n", orderedSketches.Select(x => "// count " + x.Item3.Count + "\n" + x.Item1));
      Log(exploreMsg);
      var bestSketch = orderedSketches.FirstOrDefault();
      return new SketchResponse(explorer ? exploreMsg : bestSketch.Item1);
    }
  
    private int Metric(int maxDepth, string sketch, Method method, List<int> badLines) {
      Log("### Metric for: " + string.Join(", ", badLines));
      var sketchHeader = sketch
        .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
        .Take(3);
      Log(string.Join('\n', sketchHeader));
      var m = 0;
      foreach (var badLine in badLines) {
        var (nesting, found) = findNesting(method.Body, badLine);
        Log("Bad line: " + badLine + ", nesting: " + nesting + ", found: " + found);
        if (nesting < maxDepth) {
          m += 1;
        }
      }
      return m;
    }

    private (int, bool) findNesting(Statement stmt, int line) {
      if (stmt.StartToken.line == line) {
        return (0, true);
      }
      if (stmt.StartToken.line <= line && line <= stmt.EndToken.line) {
        foreach (var sub in stmt.SubStatements) {
          var (subNesting, subFound) = findNesting(sub, line);
          if (subFound) {
            return (subNesting + 1, true);
          }
        }
        return (1, true);
      }
      return (0, false);
    }
  
    private int findDepth(Statement stmt) {
      return stmt.SubStatements
        .Select(sub => findDepth(sub))
        .DefaultIfEmpty(0)
        .Max() + 1;
    }

    private int FindInsertionLine(string programText, Method method) {
      // Find the pattern "method NAME" or "lemma NAME" where NAME matches the method name
      string pattern = $"(method|lemma)[^\n]*{Regex.Escape(method.Name)}";
      Regex methodRegex = new Regex(pattern);

      // Find the match in the program text
      Match match = methodRegex.Match(programText);

      if (!match.Success) {
        Log("### Didn't match");
        // Method/lemma definition not found
        return -1;
      }

      // Calculate the end position of the match
      int endPos = match.Index + match.Length;

      // Find the first opening brace after the method declaration that's not followed by ':'
      int openBracePos = -1;
      int pos = endPos;

      // Iterate through the text to find a '{' not followed by ':'
      while (pos < programText.Length) {
        pos = programText.IndexOf('{', pos);
        if (pos == -1) {
          Log("### No opening brace found");
          // No opening brace found
          return -1;
        }

        // Check if the '{' is followed by ':'
        if (pos + 1 < programText.Length && programText[pos + 1] == ':') {
          // This '{' is followed by ':', continue searching
          pos++;
          continue;
        }

        // Found a '{' not followed by ':'
        openBracePos = pos;
        break;
      }

      // Find the next line after the opening brace
      int newlinePos = programText.IndexOf('\n', openBracePos);
      if (newlinePos == -1) {
        Log("### No newline after opening brace");
        // No newline after opening brace
        return -1;
      }

      // Count the number of newlines from the beginning to the line after opening brace
      int lineCount = 0;
      for (int i = 0; i <= newlinePos; i++) {
        if (programText[i] == '\n') {
          lineCount++;
        }
      }

      return lineCount;
    }
  }
}