using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using static Microsoft.Dafny.DafnyLogger;
using System.Linq;

namespace Microsoft.Dafny {
  class VerifierCmd {
    public async static Task<string> RunVerifier(string filePath, string extra = "") {
        var psi = new ProcessStartInfo("dafny", $"verify {extra} \"{filePath}\"") {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
          };
          using var proc = Process.Start(psi)!;
          var stdout = await proc.StandardOutput.ReadToEndAsync();
          var stderr = await proc.StandardError.ReadToEndAsync();
          proc.WaitForExit();
          var allText = stdout + "\n" + stderr;

          return allText;
    }

    public static async Task<int> considerSketch(List<(string, int)> sketches, string programText, string methodName, int lineNumber, string sketch) {
        var sketchedProgram = InsertSketchAtLine(programText, sketch, lineNumber);
        var output = await VerifyDafnyProgram(sketchedProgram, methodName);
        var count = ParseErrorCount(output) ?? -1;
        sketches.Add((sketch, count));
        return count;
    }

    public static string InsertSketchAtLine(string program, string sketch, int lineNumber)
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

    public static string PrintProgramToString(Program program)
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

    public static async Task<string> VerifyDafnyProgram(string programText, string methodName)
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
            return text;
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

    public static List<int> FindBadLines(string input)
    {
        string pattern = @"\((\d+),\d+\): Error:";

        MatchCollection matches = Regex.Matches(input, pattern);
        List<int> lineNumbers = new List<int>();

        foreach (Match match in matches)
        {
            if (int.TryParse(match.Groups[1].Value, out int number))
            {
                lineNumbers.Add(number);
            }
        }

        Log("### Bad lines: " + string.Join(", ", lineNumbers));
        return lineNumbers.Distinct().ToList();
    }
  }
}