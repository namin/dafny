using System.Collections.Generic;

namespace Microsoft.Dafny {
    public class SketchRequest {
        public Program ResolvedProgram { get; set; }
        public string Content { get; set; }
        public Method Method { get; set; } // The Dafny Method object to sketch
        public int? LineNumber { get; set; } // Optional line number for focus
        public string SketchType { get; set; } // The type of sketch (e.g., "induction", "assertions", "ai")
        public string? Prompt { get; set; } // Optional raw prompt for AI sketchers
        public Dictionary<string, object>? AdditionalData { get; set; } // Extensibility for custom data

        public SketchRequest(Program resolvedProgram, string content, Method method, string sketchType, int? lineNumber = null, string? prompt = null, Dictionary<string, object>? additionalData = null) {
            ResolvedProgram = resolvedProgram;
            Content = content;
            Method = method;
            SketchType = sketchType;
            LineNumber = lineNumber;
            Prompt = prompt;
            AdditionalData = additionalData ?? new Dictionary<string, object>();
        }
    }
}