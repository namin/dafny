using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.Dafny.LanguageServer.Handlers.Custom {
  [Method(DafnyRequestNames.proofSketch, Direction.ClientToServer)]
  public record ProofSketchParams : TextDocumentPositionParams, IRequest<ProofSketchResponse> {
    public string SketchType { get; init; }
  }
}
