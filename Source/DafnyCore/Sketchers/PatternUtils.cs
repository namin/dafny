using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Dafny;

namespace Microsoft.Dafny {

  internal static class PatternUtils {

    /// <summary>
    /// Return all bound variables introduced by an ExtendedPattern, in source order.
    /// </summary>
    public static List<IVariable> AllPatternBoundVars(ExtendedPattern pat) {
      var res = new List<IVariable>();
      if (pat == null) {
        return res;
      }

      foreach (var node in pat.DescendantsAndSelf) {
        if (node is IdPattern id && id.BoundVar != null) {
          res.Add(id.BoundVar);
        }
      }
      return res;
    }

    // Pretty string for printing the case: KEEP binder names, but collapse throwaway
    // names that start with '_' to just "_", and normalize whitespace.
    public static string Pretty(ExtendedPattern pat) {
      var s = pat.ToString();
      s = Regex.Replace(s, @"\s+", " ");
      s = Regex.Replace(s, @"\b_[A-Za-z0-9_]*\b", "_");
      return s;
    }

    /// <summary>
    /// Normalize a pattern by replacing all binder names with "_" while preserving constructor name/arity.
    /// Example: "Node(l, v, r)" -> "Node(_, _, _)"
    /// </summary>
    public static string Normalize(ExtendedPattern pat) {
      if (pat == null) {
        return "_";
      }
      var s = pat.ToString();
       foreach (var node in pat.DescendantsAndSelf) {
        if (node is IdPattern id && id.BoundVar != null) {
          s = Regex.Replace(s, $@"\b{Regex.Escape(id.BoundVar.Name)}\b", "_");
        }
      }
      // Compress spaces to be stable-ish across versions
      s = Regex.Replace(s, @"\s+", " ");
      return s;
    }
  }
}
