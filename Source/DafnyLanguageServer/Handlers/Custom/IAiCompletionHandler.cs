using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.Dafny.LanguageServer.Handlers.Custom {
    [Parallel]
    [Method(DafnyRequestNames.aiCompletion, Direction.ClientToServer)]
    public interface IAiCompletionHandler : IJsonRpcRequestHandler<AiCompletionParams, AiCompletionResponse> {
    }
}