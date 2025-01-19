using System.Threading.Tasks;

namespace Microsoft.Dafny {
    public interface IClient
    {
        Task<string> Complete(string prompt);
        string ClientSummary { get; }
    }
}