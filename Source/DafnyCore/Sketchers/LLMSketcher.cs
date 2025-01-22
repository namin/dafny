using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Microsoft.Dafny {
    public class LLMSketcher: LLMSketcherWhole {
        private readonly LLMClient _client;
        private readonly string _code_here_comment;
        private readonly string _instructions;
        public LLMSketcher(ErrorReporter reporter): base(reporter) {
             
            _code_here_comment = "// --- YOUR CODE WILL BE ADDED HERE! ---";
            _instructions = "Only output Dafny code without context or explanation. Do not repeat surrounding code. Your code will be added on the line marked: "+_code_here_comment+".\n Edit to satisfy the instructions as follows.";
        }

        public new async Task<SketchResponse> GenerateSketch(SketchRequest input) {
            var bundle = _instructions + " " + input.Prompt;
            var prompt = bundle;
            prompt += "\n```dafny\n" + ContextForLLM(input) + "\n```\n";
            prompt += "\n" + bundle;
            var response = await _client.GenerateResponse(prompt);
            return new SketchResponse(filterDafny(response));
        }

        private string ContextForLLM(SketchRequest input) {
            string content = input.Content;
            int? lineNumber = input.LineNumber;

            return GetRelevantCode(content, lineNumber);
        }

        private string GetRelevantCode(string content, int? lineNumber) {
            var lines = content.Split('\n');
            if (!lineNumber.HasValue || lineNumber < 1 || lineNumber > lines.Length) {
                return content;
            }

            lines[lineNumber.Value] = (lines[lineNumber.Value]+"\n"+_code_here_comment).TrimStart();

            return string.Join("\n", lines);
        }
    }
}