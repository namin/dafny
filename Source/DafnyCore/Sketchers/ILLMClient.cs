using System.Threading.Tasks;

namespace Microsoft.Dafny {
  public interface ILLMClient {
    Task<string> GenerateResponseAsync(string prompt);
  }
}