using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.Dafny.LanguageServer.Handlers.Custom {
  [Parallel]
  [Method(DafnyRequestNames.InductiveProofSketch, Direction.ClientToServer)]
  public interface IInductiveProofSketchHandler : IJsonRpcRequestHandler<ProofSketchParams, ProofSketchResponse> {
  }
}
