using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.Dafny.LanguageServer.Handlers.Custom {
  [Parallel]
  [Method(DafnyRequestNames.proofSketch, Direction.ClientToServer)]
  public interface IProofSketchHandler : IJsonRpcRequestHandler<ProofSketchParams, ProofSketchResponse> {
  }
}
