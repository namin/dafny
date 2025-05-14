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

    public InductiveProofSearchSketcher(ErrorReporter reporter) : base(reporter) {
      this.reporter = reporter;
      this.inductiveProofSketcher = new InductiveProofSketcher(reporter);
    }

    public override Task<SketchResponse> GenerateSketch(SketchRequest input) {
      return Task.FromResult(new SketchResponse(GenerateProofSketch(input.ResolvedProgram, input.Method, input.LineNumber)));
    }
    private string GenerateProofSketch(Program program, Method method, int? lineNumber) {
      if (method == null) {
        return "// Error: No method resolved.";
      }
      string programText = PrintProgramToString(program);
      Log("## Program text");
      Log(programText);
      var lineNo = FindInsertionLine(programText, method);
      if (lineNo < 0) {
        return "// Cannot find method";
      }
      var requiresCalls = inductiveProofSketcher.AllRequiresCalls(method).Select(item => item.Item1).Distinct().ToList();
      var vars = inductiveProofSketcher.FindInductionVariables(method).Distinct().ToList();
      var sketches = new List<(string, int)>();
      foreach (var requireCall in requiresCalls) {
        considerSketch(sketches, programText, method.Name, lineNo,
            inductiveProofSketcher.GenerateFunctionBasedInductionProofSketch(method, requireCall));
      }
      foreach (var inductionVar in vars) {
        considerSketch(sketches, programText, method.Name, lineNo,
            inductiveProofSketcher.BuildProofSketch(method, inductionVar));
      }
      var bestFirst = sketches.OrderBy(pair => pair.Item2).ToList();
      Log(string.Join("\n\n", bestFirst.Select(x => "// count " + x.Item2 + "\n" + x.Item1)));
      return bestFirst.FirstOrDefault().Item1;
    }

    private int FindInsertionLine(string programText, Method method)
    {
        // Find the pattern "method NAME" or "lemma NAME" where NAME matches the method name
        string pattern = $"(method|lemma)[^\n]*{Regex.Escape(method.Name)}";
        Regex methodRegex = new Regex(pattern);
        
        // Find the match in the program text
        Match match = methodRegex.Match(programText);
        
        if (!match.Success)
        {
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
        while (pos < programText.Length)
        {
            pos = programText.IndexOf('{', pos);
            if (pos == -1)
            {
                Log("### No opening brace found");
                // No opening brace found
                return -1;
            }
            
            // Check if the '{' is followed by ':'
            if (pos + 1 < programText.Length && programText[pos + 1] == ':')
            {
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
        if (newlinePos == -1)
        {
            Log("### No newline after opening brace");
            // No newline after opening brace
            return -1;
        }
        
        // Count the number of newlines from the beginning to the line after opening brace
        int lineCount = 0;
        for (int i = 0; i <= newlinePos; i++)
        {
            if (programText[i] == '\n')
            {
                lineCount++;
            }
        }
        
        return lineCount;
    }
  }
}