using System.Threading;
using System.Threading.Tasks;
using Microsoft.Dafny.LanguageServer.Workspace;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

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
      var projectManager = await projects.GetProjectManager(request.TextDocument);
      var errorMsg = "";
      if (projectManager == null) {
        errorMsg += "\n // Couldn't find error manager for requested document: " + request.TextDocument.Uri;
      } else {
        // Get the latest resolved state
        var state = await projectManager.GetStateAfterResolutionAsync();

        if (state != null && state.ResolvedProgram is Program resolvedProgram) {
          var reporter = new ConsoleErrorReporter(projectManager.Compilation.Options);
          var method = GetMethodFromPosition(resolvedProgram, request.Position);
          if (method != null) {
            var sketcher = ISketcher.Create(request.SketchType, reporter);
            if (sketcher != null) {
              return await sketcher.GenerateSketch(new SketchRequest(
                resolvedProgram, request.Content, method, request.SketchType, request.Position.Line, request.Prompt));
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
  }
}
