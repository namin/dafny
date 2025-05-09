using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using static Microsoft.Dafny.DafnyLogger;

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
      var lineNo = lineNumber ?? method.EndToken.line;
      string programText = PrintProgramToString(program);
      Log("## Program text");
      Log(programText);
      var requiresCalls = inductiveProofSketcher.AllRequiresCalls(method).Select(item => item.Item1).Distinct().ToList();
      var vars = inductiveProofSketcher.FindInductionVariables(method).Distinct().ToList();
      var sketches = new List<(string, int)>();
      foreach (var requireCall in requiresCalls) {
        considerSketch(sketches, programText, lineNo,
            inductiveProofSketcher.GenerateFunctionBasedInductionProofSketch(method, requireCall));
      }
      foreach (var inductionVar in vars) {
        considerSketch(sketches, programText, lineNo,
            inductiveProofSketcher.BuildProofSketch(method, inductionVar));
      }
      return string.Join("\n\n", sketches.Select(x => "// count " + x.Item2 + "\n" + x.Item1));
    }

    private void considerSketch(List<(string, int)> sketches, string programText, int lineNumber, string sketch) {
        var count = VerifyCountErrors(programText, sketch, lineNumber);
        sketches.Add((sketch, count));
    }

    private int VerifyCountErrors(String program, string sketch, int lineNumber) {
        var sketchedProgram = InsertSketchAtLine(program, sketch, lineNumber);
        return VerifyDafnyProgramSync(sketchedProgram);
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

    public int VerifyDafnyProgramSync(string programText)
    {
        // This will block the current thread until the task completes
        // WARNING: Can cause deadlocks if the task depends on the current thread
        Task<int> task = VerifyDafnyProgram(programText);
        return task.Result;
    }

    static async Task<int> VerifyDafnyProgram(string programText)
    {
        // Create DafnyOptions with default settings
        var options = DafnyOptions.Default;
        options.VerifySnapshots = 1; // Basic verification

        // Create a temporary file with the sketch content
        string tempFilePath = Path.GetTempFileName() + ".dfy";
        File.WriteAllText(tempFilePath, programText);

        try
        {
            // Create a DafnyFile from the temporary file
            var uri = new Uri(tempFilePath);
            var dafnyFile = DafnyFile.HandleDafnyFile(OnDiskFileSystem.Instance,
                new ErrorReporterSink(options), options, uri, Token.NoToken);
            var files = new List<DafnyFile> { dafnyFile };
            
            // Parse and check the program
            var (program, error) = await DafnyMain.ParseCheck(
                new StringReader(""), 
                files, 
                "SketchVerification", 
                options);
            
            if (error != null)
            {
                // If there's an error in parsing/resolving, extract the count
                string[] parts = error.Split(' ');
                if (int.TryParse(parts[0], out int count))
                {
                    return count;
                }
                // If we can't parse the count, assume at least one error
                return 1;
            }
            
            // If verification is needed, proceed with it
            if (program != null && program.Reporter != null)
            {
                // You can run verification here using the Boogie pipeline
                // For a simple count, we can use the error count from the reporter
                return program.Reporter.Count(Microsoft.Dafny.ErrorLevel.Error);
            }
            
            return 0; // No errors found
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
  }
}