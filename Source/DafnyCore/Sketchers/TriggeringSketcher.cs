using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Boogie.SMTLib;
using Microsoft.Dafny;
using RAST;

namespace Microsoft.Dafny {
  /// <summary>
  /// TriggeringSketcher helps with trigger selection for quantifiers.
  /// It analyzes quantified expressions and suggests potential triggers ranked by effectiveness.
  /// 
  /// Suggested triggers are ranked as follows:
  /// 1. Sequence/array accesses (score 100) - e.g., a[i], s[i]
  /// 2. Function applications (score 80) - e.g., f(i)
  /// 3. Field accesses (score 60) - e.g., x.f
  /// 4. Range expressions (score 40) - e.g., from <= i, i < to
  /// 
  /// The sketcher avoids suggesting invalid triggers such as:
  /// - Implications (==>) and logical equivalence (<==>)
  /// - Expressions without bound variables
  /// - Nested quantifiers
  /// - Literals
  /// 
  /// Example usage:
  /// For a quantifier like: forall i :: from <= i < to ==> s[i] != v
  /// The sketcher suggests:
  /// {:trigger s[i]}        // Primary trigger using sequence access
  /// {:trigger from <= i}   // Alternative using lower bound
  /// {:trigger i < to}      // Alternative using upper bound
  /// </summary>
  public class TriggeringSketcher : ProofSketcher {
    private readonly ErrorReporter reporter;

    public TriggeringSketcher(ErrorReporter reporter) : base(reporter) {
      this.reporter = reporter;
    }

    public override string GenerateProofSketch(Program program, Method method, int? lineNumber) {
      // Find relevant quantifiers near the verification failure
      var quantifiers = FindRelevantQuantifiers(method, lineNumber);
      if (quantifiers.Count == 0) {
        return "// No relevant quantifiers found near this point";
      }

      // Collect all unique triggers from all quantifiers
      var sb = new StringBuilder();
      var allTriggers = new HashSet<string>();

      foreach (var quantifier in quantifiers) {
        var suggestedTriggers = GenerateTriggerSuggestions(quantifier, program);
        allTriggers.UnionWith(suggestedTriggers);
      }

      if (allTriggers.Count > 0) {
        sb.AppendLine("// Suggested triggers, ranked by effectiveness:");
        foreach (var trigger in allTriggers) {
          sb.AppendLine($"{trigger}");
        }
      }
      return sb.ToString();
    }

    private List<Expression> FindRelevantQuantifiers(Method method, int? lineNumber) {
      var quantifiers = new HashSet<Expression>();
      // Start with quantifiers in requires and ensures clauses
      foreach (var req in method.Req) {
        CollectQuantifiers(req.E, quantifiers);
      }
      foreach (var ens in method.Ens) {
        CollectQuantifiers(ens.E, quantifiers);
      }
      // Process method body if available
      if (method.Body != null) {
        CollectQuantifiersFromBody(method.Body, quantifiers);
      }
      return quantifiers.ToList();
    }

    private void CollectQuantifiersFromBody(BlockStmt body, HashSet<Expression> quantifiers) {
      foreach (var stmt in body.Body) {
        CollectQuantifiersFromStatement(stmt, quantifiers);
      }
    }

    private void CollectQuantifiersFromStatement(Statement stmt, HashSet<Expression> quantifiers) {
      if (stmt is AssertStmt assertStmt) {
        CollectQuantifiers(assertStmt.Expr, quantifiers);
      }
      else if (stmt is WhileStmt whileStmt) {
        foreach (var inv in whileStmt.Invariants) {
          CollectQuantifiers(inv.E, quantifiers);
        }
        if (whileStmt.Body != null) {
          CollectQuantifiersFromBody(whileStmt.Body, quantifiers);
        }
      }
      else if (stmt is BlockStmt blockStmt) {
        CollectQuantifiersFromBody(blockStmt, quantifiers);
      }
      // Add other statement types as needed
    }

    private void CollectQuantifiers(Expression expr, HashSet<Expression> quantifiers) {
      if (expr is QuantifierExpr quantifier) {
        quantifiers.Add(quantifier);
      }
      foreach (var subExpr in expr.SubExpressions) {
        CollectQuantifiers(subExpr, quantifiers);
      }
    }

    private List<string> GenerateTriggerSuggestions(Expression quantifier, Program program) {
      if (!(quantifier is QuantifierExpr qExpr)) {
        return new List<string>();
      }

      // Get bound variables
      var boundVars = new HashSet<IVariable>(qExpr.BoundVars);

      // Collect all potential triggers
      var candidates = new List<(Expression expr, int score)>();
      
      // 1. Find sequence/array accesses - highest priority
      var arrayAccesses = CollectArrayAccesses(qExpr.Term, boundVars);
      candidates.AddRange(arrayAccesses.Select(e => (e, 100)));

      // 2. Find function applications - second priority
      var functionApps = CollectFunctionApplications(qExpr.Term, boundVars);
      candidates.AddRange(functionApps.Select(e => (e, 80)));

      // 3. Find field accesses - third priority
      var fieldAccesses = CollectFieldAccesses(qExpr.Term, boundVars);
      candidates.AddRange(fieldAccesses.Select(e => (e, 60)));

      // 4. Find range expressions and comparisons
      var rangeExprs = CollectRangeExpressions(qExpr.Term, boundVars);
      candidates.AddRange(rangeExprs.Select(e => (e, 40)));

      // Filter, rank and return top suggestions
      return RankAndFilterTriggers(candidates)
        .Take(3)  // Limit to top 3 suggestions
        .Select(e => $"{{:trigger {PrintExpression(e)}}}").Distinct()
        .ToList();
    }

    private List<Expression> CollectArrayAccesses(Expression expr, HashSet<IVariable> boundVars) {
      var accesses = new List<Expression>();
      
      if (expr is SeqSelectExpr seqSelect && IsValidTriggerExpr(seqSelect, boundVars)) {
        accesses.Add(seqSelect);
      }
      
      foreach (var subExpr in expr.SubExpressions) {
        accesses.AddRange(CollectArrayAccesses(subExpr, boundVars));
      }
      
      return accesses;
    }

    private List<Expression> CollectFunctionApplications(Expression expr, HashSet<IVariable> boundVars) {
      var apps = new List<Expression>();
      
      if (expr is FunctionCallExpr funcCall && IsValidTriggerExpr(funcCall, boundVars)) {
        apps.Add(funcCall);
      }
      
      foreach (var subExpr in expr.SubExpressions) {
        apps.AddRange(CollectFunctionApplications(subExpr, boundVars));
      }
      
      return apps;
    }

    private List<Expression> CollectFieldAccesses(Expression expr, HashSet<IVariable> boundVars) {
      var accesses = new List<Expression>();
      
      if (expr is MemberSelectExpr memberSelect && IsValidTriggerExpr(memberSelect, boundVars)) {
        accesses.Add(memberSelect);
      }
      
      foreach (var subExpr in expr.SubExpressions) {
        accesses.AddRange(CollectFieldAccesses(subExpr, boundVars));
      }
      
      return accesses;
    }

    private List<Expression> CollectRangeExpressions(Expression expr, HashSet<IVariable> boundVars) {
      var ranges = new List<Expression>();
      
      if (expr is BinaryExpr binary && IsValidTriggerExpr(binary, boundVars)) {
        // Add individual comparisons
        if (binary.Op == BinaryExpr.Opcode.Lt || binary.Op == BinaryExpr.Opcode.Le ||
            binary.Op == BinaryExpr.Opcode.Gt || binary.Op == BinaryExpr.Opcode.Ge) {
          ranges.Add(binary);
        }
        // Look for range patterns like from <= i < to
        if (binary.Op == BinaryExpr.Opcode.Lt || binary.Op == BinaryExpr.Opcode.Le) {
          if (binary.E0 is BinaryExpr leftBinary && 
              (leftBinary.Op == BinaryExpr.Opcode.Le || leftBinary.Op == BinaryExpr.Opcode.Lt)) {
            ranges.Add(binary);
          }
        }
      }
      
      foreach (var subExpr in expr.SubExpressions) {
        ranges.AddRange(CollectRangeExpressions(subExpr, boundVars));
      }
      
      return ranges;
    }

    private List<Expression> RankAndFilterTriggers(List<(Expression expr, int score)> candidates) {
      return candidates
        .Where(c => IsValidTriggerExpr(c.expr, null))  // Final validity check
        .OrderByDescending(c => c.score)
        .Select(c => c.expr)
        .ToList();
    }

    private bool IsValidTriggerExpr(Expression expr, HashSet<IVariable> boundVars) {
      // Basic trigger validity rules
      if (expr is null) return false;
      if (expr is QuantifierExpr) return false;  // No nested quantifiers
      if (expr is LiteralExpr) return false;     // No literals
      if (expr is BinaryExpr binary) {
        switch (binary.Op) {
          case BinaryExpr.Opcode.Imp:     // Only exclude implications
          case BinaryExpr.Opcode.Iff:     // and logical equivalence
            return false;
          default:
            break;
        }
      }

      // If we're checking bound variables
      if (boundVars != null) {
        // Must mention at least one bound variable
        var mentionedVars = CollectMentionedVariables(expr);
        return mentionedVars.Intersect(boundVars).Any();
      }

      return true;
    }

    private HashSet<IVariable> CollectMentionedVariables(Expression expr) {
      var vars = new HashSet<IVariable>();
      
      if (expr is IdentifierExpr idExpr && idExpr.Var != null) {
        vars.Add(idExpr.Var);
      }
      
      foreach (var subExpr in expr.SubExpressions) {
        vars.UnionWith(CollectMentionedVariables(subExpr));
      }
      
      return vars;
    }

    private string PrintExpression(Expression expr) {
      return Printer.ExprToString(reporter.Options, expr);
    }
  }
}