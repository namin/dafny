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
    public class LLMClient {
        private readonly ChatClient _client;
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
        }
        public async Task<string> GenerateResponse(string prompt) {
            if (string.IsNullOrWhiteSpace(prompt)) {
                throw new ArgumentException("Prompt cannot be null or empty.", nameof(prompt));
            }
            try {
                ChatCompletion completion = await _client.CompleteChatAsync(prompt);
                return completion.Content[0].Text.Trim();
            }
            catch (Exception ex) {
                return $"Error generating response: {ex.Message}";
            }
        }
        public string PrettyPrintMethod(Method method) {
            using (var writer = new StringWriter()) {
                var printer = new Printer(writer, _reporter.Options);
                printer.PrintMethod(method, 0, false);
                return writer.ToString();
            }
        }
    }
}