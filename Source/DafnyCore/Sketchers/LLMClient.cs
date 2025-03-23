using System;
using System.ClientModel;
using System.Threading.Tasks;
using Microsoft.Boogie;
using OpenAI.Chat;
using static Microsoft.Dafny.DafnyLogger;

namespace Microsoft.Dafny {
    public class LLMClient {
        private readonly IClient _client;
        public LLMClient() {
            if (Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") != null) {
                _client = new AnthropicClient();
            } else if (Environment.GetEnvironmentVariable("GEMINI_API_KEY") != null) {
                _client = new GeminiClient();
            } else {
                _client = new OpenAIClient();
            }
        }
        public async Task<string> GenerateResponse(string prompt) {
            if (string.IsNullOrWhiteSpace(prompt)) {
                throw new ArgumentException("Prompt cannot be null or empty.", nameof(prompt));
            }

            Log("## LLMClient " + _client.ClientSummary);
            Log("### Prompt:");
            Log(prompt);
            try {
                var result = await _client.Complete(prompt);
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