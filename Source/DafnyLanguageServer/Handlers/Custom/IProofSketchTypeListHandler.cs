using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.Dafny.LanguageServer.Handlers.Custom
{
    [Method(DafnyRequestNames.proofSketchTypeList, Direction.ClientToServer)]
    public interface IProofSketchTypeListHandler : IJsonRpcRequestHandler<ProofSketchTypeListParams, ProofSketchTypeListResponse> {

    }
}
