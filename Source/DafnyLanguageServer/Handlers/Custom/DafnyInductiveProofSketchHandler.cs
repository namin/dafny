using System.Threading;
using System.Threading.Tasks;
using Microsoft.Dafny.LanguageServer.Workspace;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.Dafny.LanguageServer.Handlers.Custom {
  public class DafnyInductiveProofSketchHandler : IInductiveProofSketchHandler {
    private readonly ILogger<DafnyInductiveProofSketchHandler> logger;
    private readonly IProjectDatabase projects;

    public DafnyInductiveProofSketchHandler(ILogger<DafnyInductiveProofSketchHandler> logger, IProjectDatabase projects) {
      this.logger = logger;
      this.projects = projects;
    }

    public async Task<ProofSketchResponse> Handle(ProofSketchParams request, CancellationToken cancellationToken) {
      var projectManager = await projects.GetProjectManager(request.TextDocument);
      var errorMsg = "";
      if (projectManager == null) {
        errorMsg += "\n // Couldn't find error manager for requested document: " + request.TextDocument.Uri;
      } else {
        // Use GetStateAfterResolutionAsync to get the latest resolved state
        var state = await projectManager.GetStateAfterResolutionAsync();

        if (state != null && state.ResolvedProgram is Program resolvedProgram) {
          // Create an ErrorReporter for the InductiveProofSketcher
          var reporter = new ConsoleErrorReporter(projectManager.Compilation.Options);

          // Find the method at the given position
          var method = GetMethodFromPosition(resolvedProgram, request.Position);

          if (method != null) {
            // Create the InductiveProofSketcher with the ErrorReporter
            var sketcher = new InductiveProofSketcher(reporter);
            // Generate the proof sketch using InductiveProofSketcher
            var sketch = sketcher.GenerateProofSketch(method);
           
            return new ProofSketchResponse { Sketch = sketch };    
          } else {
            errorMsg += $"\n// No method found at position {request.Position} in {request.TextDocument.Uri}";
          }
        }
      }
      return new ProofSketchResponse { Sketch = "\n// Error: no proof sketch generated" + errorMsg + "\n"}; 
    }

    private Method GetMethodFromPosition(Program resolvedProgram, Position position) {
      // Accessing the DefaultModuleDefinition from the resolvedProgram
      if (resolvedProgram.DefaultModuleDef is DefaultModuleDefinition defaultModule) {
        // Traversing the top-level declarations in the Dafny module
        foreach (var topLevelDecl in defaultModule.TopLevelDecls) {
          if (topLevelDecl is TopLevelDeclWithMembers classDecl) {
            Method previousMethod = null;
            foreach (var member in classDecl.Members) {
              if (member is Method method) {
                if (previousMethod != null && IsPositionAfter(previousMethod.tok, position)) {
                  return previousMethod;
                }
                previousMethod = method;
              }
            }
            // Check if the position is in the last method
            if (previousMethod != null && IsPositionAfter(previousMethod.tok, position)) {
              return previousMethod;
            }
          }
        }
      }
      return null;
    }

    // Helper method to check if the position is after the method's start token
    private bool IsPositionAfter(IToken methodToken, Position position) {
      var startLine = methodToken.line - 1;  // Convert to zero-based index
      var startCharacter = methodToken.col - 1;

      // Check if the position is after the start of the method
      return position.Line > startLine || (position.Line == startLine && position.Character >= startCharacter);
    }


  }
}
