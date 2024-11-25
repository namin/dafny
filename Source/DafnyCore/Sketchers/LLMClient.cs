using System;
using System.ClientModel;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using OpenAI.Chat;

namespace Microsoft.Dafny {
    public class LLMClient: ILLMClient {
        private readonly ChatClient _client;
        private readonly string _instructions;
        public LLMClient() {
            var base_url = Environment.GetEnvironmentVariable("OPENAI_BASE_URL") ?? "http://localhost";
            var port = Environment.GetEnvironmentVariable("OPENAI_PORT") ?? "11434"; 
            var model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "qwen2.5-coder";
            var apiKey = new ApiKeyCredential(Environment.GetEnvironmentVariable("OPENAI_PASSWORD") ?? "ollama");
            var options = new OpenAI.OpenAIClientOptions {
                Endpoint = new Uri($"{base_url}:{port}/v1")
            };
            _client = new ChatClient(model, apiKey, options);
            _instructions = "Only output Dafny code without context or explanation.";
        }
        public async Task<string> GenerateResponseAsync(string prompt) {
            if (string.IsNullOrWhiteSpace(prompt)) {
                throw new ArgumentException("Prompt cannot be null or empty.", nameof(prompt));
            }
            try {
                ChatCompletion completion = await _client.CompleteChatAsync(prompt+"\n"+_instructions);
                return filterDafny(completion.Content[0].Text.Trim());
            }
            catch (Exception ex) {
                return $"Error generating response: {ex.Message}";
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