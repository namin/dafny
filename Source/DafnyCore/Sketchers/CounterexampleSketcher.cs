using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

            var ins = string.Join(", ", input.Method.Ins.Select(x => x.Name + ": " + x.Type));
            var ens = string.Join(" && ", input.Method.Ens.Select(x => x.E.ToString()));
            var ensFail = "ensures !(" + ens + ")";

            var sb = new StringBuilder();
            for (int i = 0; i < clauses.Count; i++) {
                var clause = clauses[i];
                var name = methodName + "_CounterExample_" + (i+1);
                sb.Append($"lemma {name}({ins})\n");
                sb.Append($"requires {clause}\n");
                sb.Append(ensFail + "\n");
                sb.Append("{}\n");
                sb.Append("\n");
            }
            return new SketchResponse(sb.ToString());
        }
    }
}