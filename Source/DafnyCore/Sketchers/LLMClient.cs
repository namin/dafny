using System;
using System.ClientModel;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;
using OpenAI.Chat;
using OmniSharp.Extensions.JsonRpc.Server;
using System.Security.Cryptography.X509Certificates;
using System.IO;

namespace Microsoft.Dafny {
    public class LLMClient: ISketcher {
        private readonly ChatClient _client;
        private readonly string _code_here_comment;
        private readonly string _instructions;
        private readonly ErrorReporter _reporter;
        public LLMClient(ErrorReporter reporter) {
            var base_url = Environment.GetEnvironmentVariable("OPENAI_BASE_URL") ?? "http://localhost";
            var port = Environment.GetEnvironmentVariable("OPENAI_PORT") ?? "11434"; 
            var model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "qwen2.5-coder";
            var apiKey = new ApiKeyCredential(Environment.GetEnvironmentVariable("OPENAI_PASSWORD") ?? "ollama");
            var options = new OpenAI.OpenAIClientOptions {
                Endpoint = new Uri($"{base_url}:{port}/v1")
            };
            _client = new ChatClient(model, apiKey, options);
            _code_here_comment = "// --- YOUR CODE WILL BE ADDED HERE! ---";
            _instructions = "Only output Dafny code without context or explanation. Do not repeat surrounding code. Your code will be added on the line marked: "+_code_here_comment+".\n Edit to satisfy the instructions as follows.";
            _reporter = reporter;
        }
        public async Task<string> GenerateResponse(string prompt) {
            if (string.IsNullOrWhiteSpace(prompt)) {
                throw new ArgumentException("Prompt cannot be null or empty.", nameof(prompt));
            }
            try {
                ChatCompletion completion = await _client.CompleteChatAsync(prompt);
                return filterDafny(completion.Content[0].Text.Trim());
            }
            catch (Exception ex) {
                return $"Error generating response: {ex.Message}";
            }
        }

        public async Task<SketchResponse> GenerateSketch(SketchRequest input) {
            var bundle = _instructions + " " + input.Prompt;
            var prompt = bundle;
            prompt += "\n```dafny\n" + ContextForLLM(input) + "\n```\n";
            prompt += "\n" + bundle;
            return new SketchResponse(await GenerateResponse(prompt));
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
        public string PrettyPrintMethod(Method method) {
            using (var writer = new StringWriter()) {
                var printer = new Printer(writer, _reporter.Options);
                printer.PrintMethod(method, 0, false);
                return writer.ToString();
            }
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
            var codeBlockPattern = @"(?s)```dafny\s+(.*?)```";

            var extractedCode = new List<string>();
            var matches = Regex.Matches(response, codeBlockPattern);

            foreach (Match match in matches) {
                if (match.Success) {
                    var code = match.Groups[1].Value.Trim();
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
                if (trimmedLine == "Copy code" || trimmedLine == "dafny") {
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