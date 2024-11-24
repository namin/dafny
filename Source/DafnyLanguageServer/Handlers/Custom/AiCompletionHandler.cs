using System.Threading;
using System.Threading.Tasks;
using System;

namespace Microsoft.Dafny.LanguageServer.Handlers.Custom {
    public class AiCompletionHandler : IAiCompletionHandler {
        private readonly LLMClient _llmClient;

        public AiCompletionHandler(LLMClient llmClient) {
            _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        }

        public async Task<AiCompletionResponse> Handle(AiCompletionParams request, CancellationToken cancellationToken) {
            var prompt = $"{request.Context}\n\nUser Prompt: {request.UserPrompt}";
            
            var response = await _llmClient.GenerateResponseAsync(prompt);
            return new AiCompletionResponse { Completion = response };
        }
    }
}