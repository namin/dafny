using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Microsoft.Dafny.DafnyLogger;
using Microsoft.Boogie;

namespace Microsoft.Dafny {

  public class InductiveProofSearchSketcher : ProofSketcher {
    private readonly ErrorReporter reporter;
    private readonly InductiveProofSketcher inductiveProofSketcher;

    public InductiveProofSearchSketcher(ErrorReporter reporter) : base(reporter) {
      this.reporter = reporter;
      this.inductiveProofSketcher = new InductiveProofSketcher(reporter);
    }

    public override string GenerateProofSketch(Program program, Method method, int? lineNumber) {
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
      return string.Join("\n\n", sketches.OrderBy(pair => pair.Item2).Select(x => "// count " + x.Item2 + "\n" + x.Item1));
    }

    private int FindInsertionLine(string programText, Method method)
    {
        // Find the pattern "method NAME" or "lemma NAME" where NAME matches the method name
        string pattern = $"(method|lemma)[^\n]*{Regex.Escape(method.Name)}";
        Log("### Pattern: " + pattern);
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

    private void considerSketch(List<(string, int)> sketches, string programText, string methodName, int lineNumber, string sketch) {
        var count = VerifyCountErrors(programText, sketch, methodName, lineNumber);
        sketches.Add((sketch, count));
    }

    private int VerifyCountErrors(String program, string sketch, string methodName, int lineNumber) {
        var sketchedProgram = InsertSketchAtLine(program, sketch, lineNumber);
        return VerifyDafnyProgramSync(sketchedProgram, methodName);
    }

    public string InsertSketchAtLine(string program, string sketch, int lineNumber)
    {
        // Split the program into lines
        string[] lines = program.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        
        // Check if lineNumber is valid
        if (lineNumber < 0 || lineNumber > lines.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(lineNumber), "Line number " + lineNumber + " is out of range for " + lines.Length);
        }
        
        // Create a list from the array to make insertion easier
        List<string> linesList = new List<string>(lines);
        
        // Insert the sketch as a single item at the specified position
        linesList.Insert(lineNumber, sketch);
        
        // Join the lines back together
        return string.Join(Environment.NewLine, linesList);
    }

    public string PrintProgramToString(Program program)
    {
        string tempFilePath = Path.GetTempFileName();
        try
        {
            DafnyMain.MaybePrintProgram(program, tempFilePath, false);
            string result = File.ReadAllText(tempFilePath);
            return result;
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    private int VerifyDafnyProgramSync(string programText, string methodName)
    {
        // This will block the current thread until the task completes
        // WARNING: Can cause deadlocks if the task depends on the current thread
        Task<int> task = VerifyDafnyProgram(programText, methodName);
        return task.Result;
    }

    private static async Task<int> VerifyDafnyProgram(string programText, string methodName)
    {
        Log("## Program to verify");
        Log(programText);
        // Create a temporary file with the sketch content
        string tempFilePath = Path.GetTempFileName() + ".dfy";
        File.WriteAllText(tempFilePath, programText);
        try
        {
            var text = await VerifierCmd.RunVerifier(tempFilePath, "--filter-symbol " + methodName);
            Log("### Verifier output");
            Log(text);
            return ParseErrorCount(text) ?? -1;
        }
        finally
        {
            // Clean up the temporary file
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    public static int? ParseErrorCount(string output)
    {
        // Regular expression to match the entire Dafny output format
        Regex regex = new Regex(@"Dafny program verifier finished with \d+ verified, (\d+) errors?");
        
        // Extract the match
        Match match = regex.Match(output);
        
        // If we have a match, return the error count, otherwise return null
        if (match.Success)
        {
            return int.Parse(match.Groups[1].Value);
        }
        else
        {
            return null;
        }
    }
  }
}