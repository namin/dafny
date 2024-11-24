using System;
using System.Threading.Tasks;

namespace Microsoft.Dafny
{
    class LLMProgram
    {
        static async Task Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: LLMCProgram <prompt>");
                return;
            }

            string prompt = args[0];

            var client = new LLMClient();

            string response = await client.GenerateResponseAsync(prompt);
            Console.WriteLine(response);
        }
    }
}