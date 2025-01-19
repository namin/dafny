using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Microsoft.Dafny {
    public class LLMSketcher: ISketcher {
        private readonly LLMClient _client;
        private readonly string _code_here_comment;
        private readonly string _instructions;
        public LLMSketcher(ErrorReporter reporter) {
            _client = new LLMClient();
            _code_here_comment = "// --- YOUR CODE WILL BE ADDED HERE! ---";
            _instructions = "Only output Dafny code without context or explanation. Do not repeat surrounding code. Your code will be added on the line marked: "+_code_here_comment+".\n Edit to satisfy the instructions as follows.";
        }

        public async Task<SketchResponse> GenerateSketch(SketchRequest input) {
            var bundle = _instructions + " " + input.Prompt;
            var prompt = bundle;
            prompt += "\n```dafny\n" + ContextForLLM(input) + "\n```\n";
            prompt += "\n" + bundle;
            var response = await _client.GenerateResponse(prompt);
            return new SketchResponse(filterDafny(response));
        }

        private string ContextForLLM(SketchRequest input) {
            string content = input.Content;
            int? lineNumber = input.LineNumber;

            return GetRelevantCode(content, lineNumber);
        }

        private string GetRelevantCode(string content, int? lineNumber) {
            var lines = content.Split('\n');
            if (!lineNumber.HasValue || lineNumber < 1 || lineNumber > lines.Length) {
                return content;
            }

            lines[lineNumber.Value] = (lines[lineNumber.Value]+"\n"+_code_here_comment).TrimStart();

            return string.Join("\n", lines);
        }
        private string filterDafny(string response) {
            if (string.IsNullOrWhiteSpace(response)) {
                throw new ArgumentException("Response cannot be null or empty.", nameof(response));
            }
            string quotedCode = filterQuotedDafny(response);
            if (!string.IsNullOrWhiteSpace(quotedCode)) {
                return quotedCode;
            }
            return filterAssumeCode(response);
        }
        private string filterQuotedDafny(string response) {
            var codeBlockPattern = @"(?s)```(dafny)?\s+(.*?)```";

            var extractedCode = new List<string>();
            var matches = Regex.Matches(response, codeBlockPattern);

            foreach (Match match in matches) {
                if (match.Success) {
                    var code = match.Groups[2].Value.Trim();
                    extractedCode.Add(code);
                }
            }

            return string.Join("\n", extractedCode).Trim();
        }

        private string filterAssumeCode(string response) {
            var lines = response.Split('\n');
            var cleanedLines = new List<string>();

            foreach (var line in lines) {
                var trimmedLine = line.Trim();

                // Skip lines that are only noise keywords
                if (trimmedLine.StartsWith("Copy") || trimmedLine == "dafny") {
                    continue;
                }

                // Otherwise, add the line as is
                cleanedLines.Add(trimmedLine);
            }

            // Combine and return the cleaned lines
            return string.Join("\n", cleanedLines).Trim();
        }
    }
}