using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.Dafny.LanguageServer.Handlers.Custom {
  public enum SketchType {
      Inductive,
      Assertions
  }
  [Method(DafnyRequestNames.proofSketch, Direction.ClientToServer)]
  public record ProofSketchParams : TextDocumentPositionParams, IRequest<ProofSketchResponse> {
    public SketchType SketchType { get; init; }
  }
}
