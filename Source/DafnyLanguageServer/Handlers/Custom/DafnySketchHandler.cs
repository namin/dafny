using System.Threading;
using System.Threading.Tasks;
using Microsoft.Dafny.LanguageServer.Workspace;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using System.Linq;
using System.Collections.Generic;
//using static Microsoft.Dafny.DafnyLogger;

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
          //if (method != null) {
            var sketcher = ISketcher.Create(request.SketchType, reporter);
            if (sketcher != null) {
              return await sketcher.GenerateSketch(new SketchRequest(
                resolvedProgram, request.Content, method, request.SketchType, request.Position.Line, request.Position.Character, request.Prompt));
            } else if (request.SketchType.StartsWith("error")) {
              var diagnostics = state.GetAllDiagnostics();
              var methodDiagnostics = diagnostics
                  .Where(diagnostic => IsDiagnosticForMethod(diagnostic, method));
              var inline = request.SketchType == "error_inline";
              var in_method = inline ? "" : " in "+method.ToString();
              var prefix = inline ? "// " : "";
              if (methodDiagnostics.Any()) {
                  var text = System.IO.File.ReadAllText(uri.LocalPath);
                  var annotated = GenerateErrors(text, method, methodDiagnostics, inline);
                  return new SketchResponse(prefix+"Errors found"+in_method+":\n" + annotated.Trim());
              } else {
                  return new SketchResponse(prefix+"OK: No errors"+in_method);
              }
            } else if (request.SketchType == "log_file_path") {
              return new SketchResponse("// " + DafnyLogger.logFilePath);
            } else {
              errorMsg += $"\n// No sketcher found";
            }
          //} else {
          //  errorMsg += $"\n// No method found at position {request.Position} in {request.TextDocument.Uri}";
          //}
        }
      }
      return new SketchResponse("\n// Error: no proof sketch generated" + errorMsg + "\n"); 
    }
    private Method GetMethodFromPosition(Program resolvedProgram, Position position) {
      //Log("# Getting Method");
      if (resolvedProgram.DefaultModuleDef is DefaultModuleDefinition defaultModule) {
        foreach (var topLevelDecl in defaultModule.TopLevelDecls) {
          if (topLevelDecl is TopLevelDeclWithMembers classDecl) {
            foreach (var member in classDecl.Members) {
              var method = member as Method;
              if (method != null) {
                //var methodDetails = $"lines {method.Tok.line}-{method.EndToken.line}";
                if (IsPositionInRange(method.StartToken, method.EndToken, position)) {
                  //Log("## Found method: " + methodDetails);
                  return method;
                } else {
                  //Log("## Method out of range: " + methodDetails);
                }
              }
            }
          }
        }
      }
      return null;
    }

    // Adjust to check if position is between start and end tokens
    private bool IsPositionInRange(IOrigin startToken, IOrigin endToken, Position position) {
      return position.Line >= startToken.line && position.Line <= endToken.line &&
            (position.Line != startToken.line || position.Character >= startToken.col) &&
            (position.Line != endToken.line || position.Character <= endToken.col);
    }

    private bool IsDiagnosticForMethod(FileDiagnostic fileDiagnostic, Method method) {
      var diagnostic = fileDiagnostic.Diagnostic;

      if (diagnostic.Severity != DiagnosticSeverity.Error || diagnostic.Message.StartsWith("Selected triggers: ")) {
        return false;
      }

      // Extract the diagnostic range
      var diagnosticStartLine = diagnostic.Range.Start.Line;
      var diagnosticStartChar = diagnostic.Range.Start.Character;
      var diagnosticEndLine = diagnostic.Range.End.Line;
      var diagnosticEndChar = diagnostic.Range.End.Character;

      // Extract the method's start and end range
      var methodStartLine = method.StartToken.line;
      var methodStartChar = method.StartToken.col;
      var methodEndLine = method.EndToken.line;
      var methodEndChar = method.EndToken.col;

      // Check if the diagnostic falls within the method's token range
      return (diagnosticStartLine > methodStartLine || 
              (diagnosticStartLine == methodStartLine && diagnosticStartChar >= methodStartChar)) &&
            (diagnosticEndLine < methodEndLine || 
              (diagnosticEndLine == methodEndLine && diagnosticEndChar <= methodEndChar));
    }

    private string GenerateErrors(string text, Method method, IEnumerable<FileDiagnostic> diagnostics, bool inline) {
        var prefix = inline ? "// " : "";
        var lines = text.Split('\n');
        int startLine = method.BodyStartTok.line - 1;
        int endLine = method.Body.EndToken.line - 1;
        var methodLines = lines.Skip(startLine).Take(endLine - startLine + 1).ToArray();
        var diagnosticsByLine = new Dictionary<int, HashSet<string>>();

        foreach (var diagnostic in diagnostics) {
            int diagLine = diagnostic.Diagnostic.Range.Start.Line;
            if (diagLine >= startLine && diagLine <= endLine) {
                if (!diagnosticsByLine.ContainsKey(diagLine - startLine)) {
                    diagnosticsByLine[diagLine - startLine] = new HashSet<string>();
                }
                var lineinfo = inline ? "" : $" Ln {diagLine+1}:";
                diagnosticsByLine[diagLine - startLine].Add(
                  $"{prefix}{lineinfo} ERROR: {diagnostic.Diagnostic.Message}");
            }
        }

        var annotatedLines = new List<string>();
        for (int i = 0; i < methodLines.Length; i++) { 
            if (diagnosticsByLine.ContainsKey(i)) {
                var line = methodLines[i];
                annotatedLines.AddRange(diagnosticsByLine[i]);
                annotatedLines.Add(line);
            } else if (inline) {
                annotatedLines.Add(methodLines[i]);
            }
        }

        return string.Join("\n", annotatedLines);
    }
  }
}
