using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Dafny.LanguageServer.Handlers.Custom {
  public class DafnySketchTypeListHandler : ISketchTypeListHandler
  {
    public Task<SketchTypeListResponse> Handle(SketchTypeListParams request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new SketchTypeListResponse
        {
            Types = ISketcher.Types
        });
    }
  }
}
