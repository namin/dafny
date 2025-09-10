using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.Dafny {

  /// <summary>
  /// Builds InvariantInfo for a method by locating a driver invariant P(t, ...)
  /// in requires/ensures and harvesting, per constructor case, the specialized
  /// arguments to recursive calls P(child, a2, ..., ak).
  ///
  /// We store the specialized actuals as strings with placeholders BINDER$<i> that
  /// are replaced by the *current* case binder names when emitting the lemma call.
  /// </summary>
  public class InvariantAnalyzer {

    private readonly ErrorReporter reporter;

    public InvariantAnalyzer(ErrorReporter reporter) {
      this.reporter = reporter;
    }

    public InvariantInfo TryBuildDriverInvariant(Method method) {
      if (method == null || method.Ins.Count == 0) {
        return null;
      }

      // Heuristic: pick the first ADT-typed input as the lemma's ADT parameter.
        var lemmaAdtFormal = method.Ins.FirstOrDefault(p => p.Type != null && p.Type.IsDatatype);
      if (lemmaAdtFormal == null) {
        return null;
      }

      // (1) Find a call to a predicate/function P(t, ...)
        var (call, which) = FindInvariantCall(method, lemmaAdtFormal);
      if (call == null || call.Function == null) {
        return null;
      }

      var info = new InvariantInfo {
        Predicate = call.Function,
        AdtFormal = call.Function.Ins.Count > 0 ? call.Function.Ins[0] : null
      };

      // (2) Align P's parameter slots with method formals using the actuals in the call we found.
      for (int i = 1; i < call.Args.Count && i < call.Function.Ins.Count; i++) {
        var arg = call.Args[i];
        var argName = NameOf(arg);
        if (argName != null) {
          var mf = method.Ins.FirstOrDefault(f => f.Name == argName);
          if (mf != null) {
            info.PParamIndexToMethodFormal[i] = mf;
          }
        }
      }

      // (3) Analyze P's body once, if present.
      if (call.Function.Body != null) {
        try {
          AnalyzeBody(info, call.Function);
        } catch (Exception e) {
          reporter.Info(MessageSource.Compiler, method.StartToken, $"[Sketcher] invariant analysis failed: {e.Message}");
        }
      }

      return info;
    }

    private (FunctionCallExpr, string) FindInvariantCall(Method method, IVariable lemmaAdtFormal) {
      // Prefer requires, then ensures.
      foreach (var attrExpr in method.Req) {
        var fce = FirstCallWithFirstArg(attrExpr.E, lemmaAdtFormal);
        if (fce != null) {
          return (fce, "requires");
        }
      }
      foreach (var attrExpr in method.Ens) {
        var fce = FirstCallWithFirstArg(attrExpr.E, lemmaAdtFormal);
        if (fce != null) {
          return (fce, "ensures");
        }
      }
      return (null, null);
    }

    private FunctionCallExpr FirstCallWithFirstArg(Expression e, IVariable v) {
      if (e is FunctionCallExpr f) {
        var first = f.Args.Count > 0 ? f.Args[0] : null;
        if (SameVar(first, v)) {
          return f;
        }
      }
      foreach (var sub in e.SubExpressions) {
        var r = FirstCallWithFirstArg(sub, v);
        if (r != null) {
          return r;
        }
      }
      return null;
    }

    private static bool SameVar(Expression e, IVariable v) {
      if (e is NameSegment ns && ns.Resolved is IdentifierExpr id1) {
        return id1.Var == v;
      }
      if (e?.Resolved is IdentifierExpr id2) {
        return id2.Var == v;
      }
      return false;
    }

    private static string NameOf(Expression e) {
      if (e is NameSegment ns) {
        return ns.Name;
      }
      if (e?.Resolved is IdentifierExpr id && id.Var != null) {
        return id.Var.Name;
      }
      return null;
    }

    private void AnalyzeBody(InvariantInfo info, Function pred) {
      // Expect a NestedMatchExpr on pred.Ins[0]. If absent, bail quietly.
      var body = pred.Body;
      var matches = new List<NestedMatchExpr>();
      CollectMatches(body, matches);
      if (matches.Count == 0) {
        return;
      }
      // Use the *first* match (common for recursive predicates).
        var m = matches[0];
      foreach (var c in m.Cases) {
        var key = PatternUtils.Normalize(c.Pat);
        var binders = PatternUtils.AllPatternBoundVars(c.Pat);
        var binderIndex = new Dictionary<string, int>();
        for (int i = 0; i < binders.Count; i++) {
          if (binders[i] != null && binders[i].Name != null) {
            binderIndex[binders[i].Name] = i;
          }
        }

        var recursiveCalls = new List<FunctionCallExpr>();
        FindCallsTo(recursiveCalls, c.Body, pred);

        foreach (var rc in recursiveCalls) {
          if (rc.Args.Count == 0) {
            continue;
          }
          var childName = NameOf(rc.Args[0]);
          if (childName == null || !binderIndex.ContainsKey(childName)) {
            continue;
          }

          var childIdx = binderIndex[childName];
          var args = new List<string>();

          // Build rename table: P formals (>=1) -> method formals; binders -> placeholders.
          var renameFrom = new List<string>();
          var renameTo = new List<string>();

          for (int j = 1; j < pred.Ins.Count; j++) {
            if (info.PParamIndexToMethodFormal.TryGetValue(j, out var mf)) {
              renameFrom.Add(pred.Ins[j].Name);
              renameTo.Add(mf.Name);
            }
          }
          for (int j = 0; j < binders.Count; j++) {
            if (binders[j] != null && binders[j].Name != null) {
              renameFrom.Add(binders[j].Name);
              renameTo.Add($"BINDER${j}");
            }
          }

          for (int j = 1; j < rc.Args.Count; j++) {
            var s = Printer.ExprToString(reporter.Options, rc.Args[j]);
            // Cheap identifier renaming via regex on word boundaries.
            for (int k = 0; k < renameFrom.Count; k++) {
              s = Regex.Replace(s, $@"\b{Regex.Escape(renameFrom[k])}\b", renameTo[k]);
            }
            args.Add(s);
          }

          if (!info.CaseObligations.TryGetValue(key, out var list)) {
            list = new List<ChildObligation>();
            info.CaseObligations[key] = list;
          }
          list.Add(new ChildObligation { BinderIndex = childIdx, SpecializedArgStrings = args });
        }
      }
    }

    private void CollectMatches(Expression e, List<NestedMatchExpr> res) {
      if (e is NestedMatchExpr me) {
        res.Add(me);
      }
      foreach (var sub in e.SubExpressions) {
        CollectMatches(sub, res);
      }
    }

    private void FindCallsTo(List<FunctionCallExpr> res, Expression e, Function f) {
      if (e is FunctionCallExpr c && c.Function == f) {
        res.Add(c);
      }
      foreach (var sub in e.SubExpressions) {
        FindCallsTo(res, sub, f);
      }
    }
  }
}
