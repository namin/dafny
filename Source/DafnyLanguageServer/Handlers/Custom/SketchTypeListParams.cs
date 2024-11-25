using MediatR;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.Dafny.LanguageServer.Handlers.Custom {
  [Method(DafnyRequestNames.sketchTypeList, Direction.ClientToServer)]
  public record SketchTypeListParams : IRequest<SketchTypeListResponse> {
  }
}