using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Dafny.LanguageServer.Handlers.Custom {
  public class DafnySketchTypeListHandler : ISketchTypeListHandler
  {
    public static readonly List<string> Types = new List<string>(ISketcher.Types.Concat(new List<string> { "error", "log_file_path" } ));
    public Task<SketchTypeListResponse> Handle(SketchTypeListParams request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new SketchTypeListResponse
        {
            Types = Types
        });
    }
  }
}
