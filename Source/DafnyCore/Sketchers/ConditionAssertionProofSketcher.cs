using System;
using System.Collections.Generic;
using Microsoft.Dafny;
using System.Text;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Dafny {
  public class ConditionAssertionProofSketcher : ProofSketcher {
    public ConditionAssertionProofSketcher(ErrorReporter reporter) : base(reporter) { }

    /// <summary>
    /// Generates explicit assertions for implicit conditions at a given gap in the method.
    /// </summary>
    /// <param name="method">The method containing the gap.</param>
    /// <param name="lineNumber">The line number of the gap.</param>
    /// <returns>A string containing assertions for implicit conditions.</returns>
    override public string GenerateProofSketch(Program program, Method? maybeMethod, int? maybeLineNumber) {
      var method = maybeMethod;
      var lineNumber = maybeLineNumber;
      if (maybeMethod is null) {
        if (lineNumber is null) {
          return "// Missing position info";
        }
        method = maybeMethod ?? FindMethod(program, lineNumber.Value);
      }
      if (method is null) {
        return "// Cannot find method";
      }
      if (lineNumber is null) {
        lineNumber = method.StartToken.line + 1;
      }
      var sb = new StringBuilder();
      sb.AppendLine("");

      var s = 2; // TODO: infer from gap location
      
      sb.AppendLine($"{Indent(s)}// Pre-gap conditions");
      foreach (var condition in CollectPreGapConditions(method, lineNumber)) {
        sb.AppendLine($"{Indent(s)}assert {condition};");
      }

      sb.AppendLine($"{Indent(s)}// gap");

      sb.AppendLine($"{Indent(s)}// Post-gap conditions");
      foreach (var goal in CollectPostGapGoals(method, lineNumber)) {
        sb.AppendLine($"{Indent(s)}assert {goal};");
      }

      return sb.ToString();
    }

    private Method FindMethod(Program program, int lineNumber) {
      //Log("# Getting Method");
      if (program.DefaultModuleDef is DefaultModuleDefinition defaultModule) {
        foreach (var topLevelDecl in defaultModule.TopLevelDecls) {
          if (topLevelDecl is TopLevelDeclWithMembers classDecl) {
            foreach (var member in classDecl.Members) {
              var method = member as Method;
              if (method != null) {
                //var methodDetails = $"lines {method.Tok.line}-{method.EndToken.line}";
                if (method.StartToken.line <= lineNumber && lineNumber <= method.EndToken.line) {
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
  
    /// <summary>
    /// Collects pre-gap conditions based on control flow up to a specified line.
    /// </summary>
    private List<string> CollectPreGapConditions(Method method, int? lineNumber) {
      var conditions = new List<string>();

      // Step 1: Add method preconditions
      foreach (var precond in method.Req) {
        conditions.Add(precond.E.ToString());
      }

      // Step 2: Traverse statements leading up to the gap, adding conditions from invariants, branches, and assignments
      foreach (var stmt in GetStatementsUpToLine(method.Body, lineNumber)) {
        if (stmt is WhileStmt whileStmt) {
          // Add loop invariant as a pre-gap condition if within the loop
          foreach (var inv in whileStmt.Invariants) {
            conditions.Add(inv.E.ToString());
          }
        } else if (stmt is IfStmt ifStmt) {
          // Add conditions based on the true or false branch we are in
          conditions.Add(ifStmt.Guard.ToString());
        }
        // Additional handling can go here for other control flow constructs
      }

      return conditions;
    }

    /// <summary>
    /// Collects post-gap goals based on following assertions and method postconditions.
    /// </summary>
    private List<string> CollectPostGapGoals(Method method, int? lineNumber) {
      var goals = new List<string>();

      // Step 1: Add method postconditions if near the end of the method
      foreach (var postcond in method.Ens) {
        goals.Add(postcond.E.ToString());
      }

      // Step 2: If gap is in a loop, add the invariant as a goal
      var followingStmt = GetStatementAfterLine(method.Body, lineNumber);
      if (followingStmt is WhileStmt whileStmt) {
        foreach (var inv in whileStmt.Invariants) {
          goals.Add(inv.E.ToString());
        }
      }

      // Additional handling for assertions or conditions following the gap can be added here

      return goals;
    }

    /// <summary>
    /// Traverses method body up to the specified line number to collect statements.
    /// </summary>
    private IEnumerable<Statement> GetStatementsUpToLine(BlockStmt body, int? lineNumber) {
      // Example pseudo-code to collect all statements up to the specified line number
      var statements = new List<Statement>();
      foreach (var stmt in body.Body) {
        if (stmt.StartToken.line >= lineNumber) { 
          break;
        }
        statements.Add(stmt);
      }
      return statements;
    }

    /// <summary>
    /// Gets the statement immediately after a specified line, if any.
    /// </summary>
    private Statement GetStatementAfterLine(BlockStmt body, int? lineNumber) {
      foreach (var stmt in body.Body) {
        if (stmt.StartToken.line > lineNumber) {
          return stmt;
        }
      }
      return null;
    }
  }
}
