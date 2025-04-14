using System.Threading.Tasks;

namespace Microsoft.Dafny {
    public class LLMSketcher: LLMSketcherWhole {
        private readonly string _code_here_comment;
        private readonly string _instructions;
        private readonly bool _abbrev;
        public LLMSketcher(bool abbrev, ErrorReporter reporter): base(reporter) {
            _abbrev = abbrev;
            _code_here_comment = "// --- YOUR CODE WILL BE ADDED HERE! ---";
            _instructions = "Only output Dafny code without context or explanation. Do not repeat surrounding code. Your code will be added on the line marked: "+_code_here_comment+".\n Edit to satisfy the instructions as follows.";
        }

        public override async Task<SketchResponse> GenerateSketch(SketchRequest input) {
            var bundle = _instructions + " " + input.Prompt;
            var prompt = bundle;
            var context = ContextForLLM(input);
            if (_abbrev) {
                context = AbbreviateContext(context);
            }
            prompt += "\n```dafny\n" + context + "\n```\n";
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

            lines[lineNumber.Value] = (lines[lineNumber.Value]+"\n"+_code_here_comment);

            var res = string.Join("\n", lines);

            return res;
        }

        // Reduce implementations, within brackets { }, to just have /*omitted*/ as their bodies,
        // except if the _code_here_comment appears.
        private string AbbreviateContext(string content) {
            // Keep track of our current position and the result we're building
            int position = 0;
            string result = "";
            
            while (position < content.Length) {
                // Find the next opening brace
                int openBrace = content.IndexOf('{', position);
                if (openBrace == -1) {
                    // No more opening braces, add the rest of the content and break
                    result += content.Substring(position);
                    break;
                }
                
                // Add everything up to the opening brace
                result += content.Substring(position, openBrace - position + 1);
                
                // Find the matching closing brace, accounting for nested braces
                int closeBrace = FindMatchingBrace(content, openBrace);
                if (closeBrace == -1) {
                    // No matching closing brace, add the rest of the content and break
                    result += content.Substring(openBrace + 1);
                    break;
                }
                
                // Check if the code_here_comment is within this block
                string block = content.Substring(openBrace + 1, closeBrace - openBrace - 1);
                if (block.Contains(_code_here_comment)) {
                    // Keep this block since it contains the comment
                    result += block + "}";
                } else {
                    // Replace the block with /*omitted*/
                    result += "/*omitted*/}";
                }
                
                // Move past the closing brace
                position = closeBrace + 1;
            }
            
            return result;
        }

        // Helper method to find the matching closing brace
        private int FindMatchingBrace(string content, int openBracePos) {
            int braceCount = 1;
            int position = openBracePos + 1;
            
            while (position < content.Length && braceCount > 0) {
                char c = content[position];
                if (c == '{') {
                    braceCount++;
                } else if (c == '}') {
                    braceCount--;
                    if (braceCount == 0) {
                        return position;
                    }
                }
                position++;
            }
            
            // No matching brace found
            return -1;
        }
    }
}