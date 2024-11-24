using MediatR;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.Dafny.LanguageServer.Handlers.Custom {
    [Method(DafnyRequestNames.aiCompletion, Direction.ClientToServer)]
    public class AiCompletionParams : IRequest<AiCompletionResponse> {
        public string Context { get; set; } = string.Empty; // Avoid null warnings
        public string UserPrompt { get; set; } = string.Empty; // Avoid null warnings
    }
}