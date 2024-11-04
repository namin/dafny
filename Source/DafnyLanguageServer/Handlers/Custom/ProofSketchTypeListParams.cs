using MediatR;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.Dafny.LanguageServer.Handlers.Custom {
  [Method(DafnyRequestNames.proofSketchTypeList, Direction.ClientToServer)]
  public record ProofSketchTypeListParams : IRequest<ProofSketchTypeListResponse> {
  }
}