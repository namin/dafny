using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Dafny.LanguageServer.Handlers.Custom {
  public class DafnyProofSketchTypeListHandler : IProofSketchTypeListHandler
  {
    public Task<ProofSketchTypeListResponse> Handle(ProofSketchTypeListParams request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new ProofSketchTypeListResponse
        {
            Types = ProofSketcher.Types
        });
    }
  }
}
