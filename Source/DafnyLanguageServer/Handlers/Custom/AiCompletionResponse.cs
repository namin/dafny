namespace Microsoft.Dafny.LanguageServer.Handlers.Custom {
    public class AiCompletionResponse {
        public string Completion { get; set; } = string.Empty; // Avoid null warnings
    }
}