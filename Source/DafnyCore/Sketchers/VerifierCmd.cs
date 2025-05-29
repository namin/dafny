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
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var proc = Process.Start(psi)!;
            var stdout = await proc.StandardOutput.ReadToEndAsync();
            var stderr = await proc.StandardError.ReadToEndAsync();
            proc.WaitForExit();
            var allText = stdout + "\n" + stderr;

            return allText;
        }

        public static async Task<List<(int,string)>> RunVerifierConditions(string context, List<(string, string)> parameters, List<string> requires, List<string> conditions) {
            var name = "scratchpad";
            var lemma = "lemma " + name + "(" + string.Join(", ", parameters.Select(p => p.Item1 + ": " + p.Item2)) + ")" +
                string.Join('\n', requires.Select(x => "\nrequires " + x));
            var body = string.Join("\nelse ", conditions.Select((x, i) => $"if ({x}) {{\nassert false; //{i}\n}}"));
            var full_lemma = lemma + "\n{\n" + body + "\n}";
            Log("### Verifier Lemma");
            Log(full_lemma);

            var contextLineCount = context.Count(c => c == '\n');
            var sketchedProgram = context + "\n" + full_lemma;
            var output = await VerifyDafnyProgram(sketchedProgram, name);
            var indices = FindFalseFailures(output);
            return indices.Select(i => (i,conditions[i])).ToList();
        }
    

        public static async Task<int> RunVerifierImplication(string context, List<(string, string)> parameters, List<string> requires, List<string> ensures) {
            var name = "scratchpad";
            var lemma = "lemma " + name + "(" + string.Join(", ", parameters.Select(p => p.Item1 + ": " + p.Item2)) + ")" +
                string.Join('\n', requires.Select(x => "\nrequires " + x)) +
                string.Join('\n', ensures.Select(x => "\nensures " + x));
            Log("### Verifier Lemma");
            Log(lemma);
            var contraCount = await RunVerifierScratchpad(context, lemma + "\n{assert false;}", name);
            if (contraCount > 0) {
                Log("#### No contradiction found");
                return await RunVerifierScratchpad(context, lemma + "\n{}", name);
            } else {
                Log("#### Contradiction found");
                return -1;
            }
        }

        public static async Task<int> RunVerifierScratchpad(string context, string pad, string methodName = null) {
            var contextLineCount = context.Count(c => c == '\n');
            var sketchedProgram = context + "\n" + pad;
            var output = await VerifyDafnyProgram(sketchedProgram, methodName);
            var badLines = FindBadLines(output);
            var count = badLines.FindAll(x => x > contextLineCount).Distinct().Count();
            return count;
        }

        public static async Task<int> considerSketch(List<(string, int)> sketches, string programText, string methodName, int lineNumber, string sketch) {
            var sketchedProgram = InsertSketchAtLine(programText, sketch, lineNumber);
            var output = await VerifyDafnyProgram(sketchedProgram, methodName);
            var count = ParseErrorCount(output) ?? -1;
            sketches.Add((sketch, count));
            return count;
        }

        public static string InsertSketchAtLine(string program, string sketch, int lineNumber) {
            // Split the program into lines
            string[] lines = program.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            // Check if lineNumber is valid
            if (lineNumber < 0 || lineNumber > lines.Length) {
                throw new ArgumentOutOfRangeException(nameof(lineNumber), "Line number " + lineNumber + " is out of range for " + lines.Length);
            }

            // Create a list from the array to make insertion easier
            List<string> linesList = new List<string>(lines);

            // Insert the sketch as a single item at the specified position
            linesList.Insert(lineNumber, sketch);

            // Join the lines back together
            return string.Join(Environment.NewLine, linesList);
        }

        public static string PrintProgramToString(Program program) {
            string tempFilePath = Path.GetTempFileName();
            try {
                DafnyMain.MaybePrintProgram(program, tempFilePath, false);
                string result = File.ReadAllText(tempFilePath);
                return result;
            }
            finally {
                if (File.Exists(tempFilePath)) {
                    File.Delete(tempFilePath);
                }
            }
        }

        public static async Task<string> VerifyDafnyProgram(string programText, string methodName) {
            //Log("## Program to verify");
            //Log(programText);
            // Create a temporary file with the sketch content
            string tempFilePath = Path.GetTempFileName() + ".dfy";
            File.WriteAllText(tempFilePath, programText);
            try {
                var text = await VerifierCmd.RunVerifier(tempFilePath, "--filter-symbol " + methodName);
                Log("### Verifier output");
                Log(text);
                return text;
            }
            finally {
                // Clean up the temporary file
                if (File.Exists(tempFilePath)) {
                    File.Delete(tempFilePath);
                }
            }
        }

        public static int? ParseErrorCount(string output) {
            // Regular expression to match the entire Dafny output format
            Regex regex = new Regex(@"Dafny program verifier finished with \d+ verified, (\d+) errors?");

            // Extract the match
            Match match = regex.Match(output);

            // If we have a match, return the error count, otherwise return null
            if (match.Success) {
                return int.Parse(match.Groups[1].Value);
            } else {
                return null;
            }
        }

        public static List<int> FindBadLines(string input) {
            string pattern = @"\((\d+),\d+\): Error:";

            MatchCollection matches = Regex.Matches(input, pattern);
            List<int> lineNumbers = new List<int>();

            foreach (Match match in matches) {
                if (int.TryParse(match.Groups[1].Value, out int number)) {
                    lineNumbers.Add(number);
                }
            }

            if (lineNumbers.Count > 0) {
                Log("### Bad lines: " + string.Join(", ", lineNumbers));
            }
            return lineNumbers.Distinct().ToList();
        }

        public static List<int> FindFalseFailures(string input) {
            var lines = input.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                 .Select(line => line.TrimEnd()) // optional: remove trailing whitespace
                 .ToList();

            var pattern = @"assert\s+false;\s*//\s*(\d+)";
            var regex = new Regex(pattern);

            var result = lines
                .Select(line => regex.Match(line))
                .Where(m => m.Success)
                .Select(m => int.Parse(m.Groups[1].Value))
                .ToList();

            return result.Distinct().ToList();
        }
    }
}