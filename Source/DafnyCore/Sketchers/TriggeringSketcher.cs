using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Boogie.SMTLib;
using Microsoft.Dafny;
using RAST;

namespace Microsoft.Dafny {
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

      var sb = new StringBuilder();
      foreach (var quantifier in quantifiers) {
        var suggestedTriggers = GenerateTriggerSuggestions(quantifier, program);
        if (suggestedTriggers.Count > 0) {
          sb.AppendLine($"// Suggested triggers for quantifier");
          foreach (var trigger in suggestedTriggers) {
            sb.AppendLine($"{trigger}");
          }
        }
      }
      return sb.ToString();
    }

    private List<Expression> FindRelevantQuantifiers(Method method, int? lineNumber) {
      var quantifiers = new List<Expression>();
      // Start with quantifiers in requires and ensures clauses
      foreach (var req in method.Req) {
        CollectQuantifiers(req.E, quantifiers, lineNumber);
      }
      foreach (var ens in method.Ens) {
        CollectQuantifiers(ens.E, quantifiers, lineNumber);
      }
      // TODO: Add loop invariants and assertions from method body
      return quantifiers;
    }

    private void CollectQuantifiers(Expression expr, List<Expression> quantifiers, int? lineNumber) {
      if (expr is QuantifierExpr quantifier) {
        // If no specific line number is given, collect all quantifiers
        // Or if line number is given but we can't get token info, collect all
        quantifiers.Add(quantifier);
      }
      // Recursively check subexpressions
      foreach (var subExpr in expr.SubExpressions) {
        CollectQuantifiers(subExpr, quantifiers, lineNumber);
      }
    }

    private List<string> GenerateTriggerSuggestions(Expression quantifier, Program program) {
      var suggestions = new List<string>();

      // 1. Static Analysis Phase
      var potentialTriggers = AnalyzePotentialTriggers(quantifier);

      // 2. Pattern Matching Phase
      var patternBasedTriggers = FindPatternBasedTriggers(quantifier, program);

      // 3. Context Analysis Phase
      var contextualTriggers = AnalyzeSuccessfulTriggersInContext(quantifier, program);

      // 4. Filter and Rank Phase
      suggestions.AddRange(RankAndFilterTriggers(
          potentialTriggers
          .Concat(patternBasedTriggers)
          .Concat(contextualTriggers)
      ));

      return suggestions;
    }

    private List<Expression> AnalyzePotentialTriggers(Expression expr) {
      var candidates = new List<Expression>();
      if (expr is QuantifierExpr quantifier) {
        // Get all bound variables
        var boundVars = new HashSet<IVariable>(quantifier.BoundVars);
        
        // Look for subexpressions that could be triggers
        CollectTriggerCandidates(quantifier.Term, candidates, boundVars);
      }
      return candidates;
    }

    private void CollectTriggerCandidates(Expression expr, List<Expression> candidates, HashSet<IVariable> boundVars) {
      // Check if this expression could be a trigger
      if (IsValidTrigger(expr, boundVars)) {
        candidates.Add(expr);
      }

      // Recursively check subexpressions
      foreach (var subExpr in expr.SubExpressions) {
        CollectTriggerCandidates(subExpr, candidates, boundVars);
      }
    }

    private List<Expression> FindPatternBasedTriggers(Expression quantifier, Program program) {
      var patterns = new List<Expression>();
      // Common patterns to look for:
      if (quantifier is QuantifierExpr qExpr) {
        // Look for function applications
        var functionApps = CollectFunctionApplications(qExpr.Term);
        patterns.AddRange(functionApps);
        
        // Look for field accesses
        var fieldAccesses = CollectFieldAccesses(qExpr.Term);
        patterns.AddRange(fieldAccesses);
      }
      return patterns;
    }

    private List<Expression> CollectFunctionApplications(Expression expr) {
      var apps = new List<Expression>();
      if (expr is FunctionCallExpr) {
        apps.Add(expr);
      }
      foreach (var subExpr in expr.SubExpressions) {
        apps.AddRange(CollectFunctionApplications(subExpr));
      }
      return apps;
    }

    private List<Expression> CollectFieldAccesses(Expression expr) {
      var accesses = new List<Expression>();
      // TODO: Implement field access collection
      return accesses;
    }

    private List<Expression> AnalyzeSuccessfulTriggersInContext(Expression quantifier, Program program) {
      var contextTriggers = new List<Expression>();
      // TODO: Look at similar quantifiers in the program
      return contextTriggers;
    }

    private List<string> RankAndFilterTriggers(IEnumerable<Expression> candidates) {
      // For now, just convert to strings
      return candidates.Select(c => $"{{:trigger {PrintExpression(c)}}}").ToList();
    }

    private bool IsValidTrigger(Expression expr, HashSet<IVariable> boundVars) {
      // Basic trigger validity rules
      if (expr is QuantifierExpr) {
        return false; // Cannot contain nested quantifiers
      }
      if (expr is BinaryExpr binary && binary.Op == BinaryExpr.Opcode.Eq) {
        return false; // Cannot contain equality
      }
      if (expr is LiteralExpr) {
        return false; // Literals alone are not useful triggers
      }
      
      // Must mention at least one bound variable
      var mentionedVars = CollectMentionedVariables(expr);
      return mentionedVars.Intersect(boundVars).Any();
    }

    private HashSet<IVariable> CollectMentionedVariables(Expression expr) {
      var vars = new HashSet<IVariable>();
      if (expr is IdentifierExpr idExpr) {
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