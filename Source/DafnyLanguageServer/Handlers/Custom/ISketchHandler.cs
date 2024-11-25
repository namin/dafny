using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.Dafny.LanguageServer.Handlers.Custom {
  [Parallel]
  [Method(DafnyRequestNames.sketch, Direction.ClientToServer)]
  public interface ISketchHandler : IJsonRpcRequestHandler<SketchParams, SketchResponse> {
  }
}
