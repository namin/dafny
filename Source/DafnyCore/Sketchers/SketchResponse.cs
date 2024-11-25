using System.Collections.Generic;

namespace Microsoft.Dafny {
  public class SketchResponse {
    // The actual generated sketch
    public string Sketch { get; set; }

    // Optional metadata (e.g., source, confidence, etc.)
    public Dictionary<string, object> Metadata { get; set; }

    // Constructor for convenience
    public SketchResponse(string sketch, Dictionary<string, object> metadata = null) {
      Sketch = sketch;
      Metadata = metadata ?? new Dictionary<string, object>();
    }
  }
}