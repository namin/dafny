using System.Threading;
using System.Threading.Tasks;
using Microsoft.Dafny.LanguageServer.Workspace;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using System.Linq;

namespace Microsoft.Dafny.LanguageServer.Handlers.Custom {
  public class DafnySketchHandler : ISketchHandler {
    private readonly ILogger<DafnySketchHandler> logger;
    private readonly IProjectDatabase projects;
    private readonly ILanguageServerFacade _languageServer;

    public DafnySketchHandler(ILogger<DafnySketchHandler> logger, IProjectDatabase projects, ILanguageServerFacade languageServer) {
      this.logger = logger;
      this.projects = projects;
      this._languageServer = languageServer;
    }

    public async Task<SketchResponse> Handle(SketchParams request, CancellationToken cancellationToken) {
      var uri = request.TextDocument.Uri.ToUri();
      var projectManager = await projects.GetProjectManager(request.TextDocument);
      var errorMsg = "";
      if (projectManager == null) {
        errorMsg += "\n // Couldn't find error manager for requested document: " + uri;
      } else {
        var state = await projectManager.GetStateAfterResolutionAsync();

        if (state != null && state.ResolvedProgram is Program resolvedProgram) {
          var reporter = projectManager.Compilation.Reporter;
          var method = GetMethodFromPosition(resolvedProgram, request.Position);
          if (method != null) {
            var sketcher = ISketcher.Create(request.SketchType, reporter);
            if (sketcher != null) {
              return await sketcher.GenerateSketch(new SketchRequest(
                resolvedProgram, request.Content, method, request.SketchType, request.Position.Line, request.Prompt));
            } else if (request.SketchType == "error") {
              var diagnostics = state.GetAllDiagnostics();
              var methodDiagnostics = diagnostics
                  .Where(diagnostic => IsDiagnosticForMethod(diagnostic, method)); // Filter diagnostics for the method
              if (methodDiagnostics.Any()) {
                  errorMsg += string.Join("\n", methodDiagnostics.Select(d => PrettyDiagnostic(d)));
                  return new SketchResponse("// Errors found:\n" + errorMsg);
              } else {
                  return new SketchResponse("// OK: No errors in the method.");
              }
            } else {
              errorMsg += $"\n// No sketcher found";
            }
          } else {
            errorMsg += $"\n// No method found at position {request.Position} in {request.TextDocument.Uri}";
          }
        }
      }
      return new SketchResponse("\n// Error: no proof sketch generated" + errorMsg + "\n"); 
    }
    private Method GetMethodFromPosition(Program resolvedProgram, Position position) {
      // Accessing the DefaultModuleDefinition from the resolvedProgram
      if (resolvedProgram.DefaultModuleDef is DefaultModuleDefinition defaultModule) {
        foreach (var topLevelDecl in defaultModule.TopLevelDecls) {
          if (topLevelDecl is TopLevelDeclWithMembers classDecl) {
            foreach (var member in classDecl.Members) {
              if (member is Method method && IsPositionInRange(method.tok, method.EndToken, position)) {
                return method;
              }
            }
          }
        }
      }
      return null;
    }

    // Adjust to check if position is between start and end tokens
    private bool IsPositionInRange(IToken startToken, IToken endToken, Position position) {
      return position.Line >= startToken.line && position.Line <= endToken.line &&
            (position.Line != startToken.line || position.Character >= startToken.col) &&
            (position.Line != endToken.line || position.Character <= endToken.col);
    }

    private bool IsDiagnosticForMethod(FileDiagnostic fileDiagnostic, Method method) {
      var diagnostic = fileDiagnostic.Diagnostic;
      // Extract the diagnostic range
      var diagnosticStartLine = diagnostic.Range.Start.Line;
      var diagnosticStartChar = diagnostic.Range.Start.Character;
      var diagnosticEndLine = diagnostic.Range.End.Line;
      var diagnosticEndChar = diagnostic.Range.End.Character;

      // Extract the method's start and end range
      var methodStartLine = method.tok.line;
      var methodStartChar = method.tok.col;
      var methodEndLine = method.EndToken.line;
      var methodEndChar = method.EndToken.col;

      // Check if the diagnostic falls within the method's token range
      return (diagnosticStartLine > methodStartLine || 
              (diagnosticStartLine == methodStartLine && diagnosticStartChar >= methodStartChar)) &&
            (diagnosticEndLine < methodEndLine || 
              (diagnosticEndLine == methodEndLine && diagnosticEndChar <= methodEndChar));
    }

    private string PrettyDiagnostic(FileDiagnostic fileDiagnostic) {
      var diagnostic = fileDiagnostic.Diagnostic;
      return "// " + diagnostic.Message;
    }
  }
}
