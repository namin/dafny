using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Microsoft.Dafny.DafnyLogger;
using static Microsoft.Dafny.VerifierCmd;

namespace Microsoft.Dafny {
    public class CounterexampleSketcher : ProofSketcher {
        private readonly ErrorReporter reporter;
        public CounterexampleSketcher(ErrorReporter reporter) : base(reporter) {
            this.reporter = reporter;
        }
        public override async Task<SketchResponse> GenerateSketch(SketchRequest input) {
            var programText = input.Content;
            var methodName = input.Method.Name;
            var clauses = await ExtractCounterexamples(programText, methodName);
            var sb = new StringBuilder();
            foreach (var clause in clauses) {
                sb.Append($"requires {clause};\n");
            }
            return new SketchResponse(sb.ToString());
        }
    }
}