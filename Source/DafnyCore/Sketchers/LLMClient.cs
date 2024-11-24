using System;
using System.ClientModel;
using System.Threading.Tasks;
using OpenAI.Chat;

namespace Microsoft.Dafny {
    public class LLMClient: ILLMClient {
        private readonly ChatClient _client;
        public LLMClient() {
            var model = "qwen2.5-coder";
            var apiKey = new ApiKeyCredential("ollama");
            var options = new OpenAI.OpenAIClientOptions {
                Endpoint = new Uri("http://localhost:11434/v1")
            };
            _client = new ChatClient(model, apiKey, options);
        }
        public async Task<string> GenerateResponseAsync(string prompt) {
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
    }
}