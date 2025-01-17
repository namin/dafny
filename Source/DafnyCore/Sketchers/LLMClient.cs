using System;
using System.ClientModel;
using System.Threading.Tasks;
using OpenAI.Chat;
using static Microsoft.Dafny.DafnyLogger;

namespace Microsoft.Dafny {
    public class LLMClient {
        private readonly ChatClient _client;
        private readonly string _clientSummary;
        public LLMClient(ErrorReporter reporter) {
            var base_url = Environment.GetEnvironmentVariable("OPENAI_BASE_URL") ?? "http://localhost";
            var port = Environment.GetEnvironmentVariable("OPENAI_PORT") ?? "11434";
            var model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "qwen2.5-coder";
            var apiKey = new ApiKeyCredential(Environment.GetEnvironmentVariable("OPENAI_PASSWORD") ?? "ollama");
            var options = new OpenAI.OpenAIClientOptions {
                Endpoint = new Uri($"{base_url}:{port}/v1")
            };
            _client = new ChatClient(model, apiKey, options);
            if (Environment.GetEnvironmentVariable("OPENAI_PORT") == null) {
                _clientSummary = model;
            } else {
                _clientSummary = ":" + port;
            }
        }
        public async Task<string> GenerateResponse(string prompt) {
            if (string.IsNullOrWhiteSpace(prompt)) {
                throw new ArgumentException("Prompt cannot be null or empty.", nameof(prompt));
            }

            Log("## LLMClient " + _clientSummary);
            Log("### Prompt:");
            Log(prompt);
            try {
                ChatCompletion completion = await _client.CompleteChatAsync(prompt);
                var result = completion.Content[0].Text.Trim();
                Log("### Result:");
                Log(result);
                return result;
            }
            catch (Exception ex) {
                return $"Error generating response: {ex.Message}";
            }
        }
    }
}