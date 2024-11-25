using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.Dafny.LanguageServer.Handlers.Custom {
  [Method(DafnyRequestNames.sketch, Direction.ClientToServer)]
  public record SketchParams : TextDocumentPositionParams, IRequest<SketchResponse> {
    public string SketchType { get; init; }
    public string Content { get; init; }
    public string? Prompt { get; init; }
  }
}
