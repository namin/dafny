using System;
using System.ClientModel;
using System.Threading.Tasks;
using OpenAI.Chat;

namespace Microsoft.Dafny {
    public class OpenAIClient : IClient {
        private readonly ChatClient _client;
        private readonly string _clientSummary;
        public OpenAIClient() {
            var base_url = Environment.GetEnvironmentVariable("OPENAI_BASE_URL") ?? "http://localhost";
            var port = Environment.GetEnvironmentVariable("OPENAI_PORT") ?? "11434";
            var model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "qwen2.5-coder";
            var apiKey = new ApiKeyCredential(Environment.GetEnvironmentVariable("OPENAI_PASSWORD") ?? "ollama");
            var options = new OpenAI.OpenAIClientOptions {
                Endpoint = new Uri($"{base_url}:{port}/v1")
            };
            _client = new ChatClient(model, apiKey, options);
            _clientSummary = $"{model} (:{port})";
        }
        public string ClientSummary => _clientSummary;

        public async Task<string> Complete(string prompt) {
            try {
                ChatCompletion completion = await _client.CompleteChatAsync(prompt);
                var result = completion.Content[0].Text.Trim();
                return result;
            }
            catch (Exception ex) {
                return $"Error generating response: {ex.Message}";
            }
        }
    }
}