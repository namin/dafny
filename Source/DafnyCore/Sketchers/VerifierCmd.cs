using System.Diagnostics;
using System.Threading.Tasks;

namespace Microsoft.Dafny {
  class VerifierCmd {
    public async static Task<string> RunVerifier(string filePath, string extra = "") {
        var psi = new ProcessStartInfo("dafny", $"verify {extra} \"{filePath}\"") {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
          };
          using var proc = Process.Start(psi)!;
          var stdout = await proc.StandardOutput.ReadToEndAsync();
          var stderr = await proc.StandardError.ReadToEndAsync();
          proc.WaitForExit();
          var allText = stdout + "\n" + stderr;

          return allText;
    }
  }
}