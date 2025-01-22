using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Microsoft.Dafny {
    public class LLMSketcherWhole: ISketcher {
        private readonly LLMClient _client;
        public LLMSketcherWhole(ErrorReporter reporter) {
            _client = new LLMClient();
       }

        public async Task<SketchResponse> GenerateSketch(SketchRequest input) {
            var prompt = input.Prompt;
            var response = await _client.GenerateResponse(prompt);
            return new SketchResponse(filterDafny(response));
        }
        protected string filterDafny(string response) {
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