using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.Dafny.LanguageServer.Handlers.Custom
{
    [Method("dafny/proofSketchTypeList", Direction.ClientToServer)]
    public interface IProofSketchTypeListHandler : IJsonRpcRequestHandler<ProofSketchTypeListParams, ProofSketchTypeListResponse> {
        
    }
}
