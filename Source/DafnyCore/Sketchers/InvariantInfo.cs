using System;
using System.Collections.Generic;
using Microsoft.Dafny;

namespace Microsoft.Dafny {

  /// <summary>
  /// Per-method, per-invariant information extracted from the driver invariant P.
  /// </summary>
  public class InvariantInfo {
    public Function Predicate;                // P
    public IVariable AdtFormal;               // P.Ins[0]
    // Maps P's parameter index (>=1) -> the method formal this parameter corresponds to.
    public Dictionary<int, IVariable> PParamIndexToMethodFormal = new Dictionary<int, IVariable>();
    // Keyed by normalized constructor string, e.g., "Node(_,_,_)"
    public Dictionary<string, List<ChildObligation>> CaseObligations = new Dictionary<string, List<ChildObligation>>();
  }

  /// <summary>
  /// Records obligations for a recursive call inside a constructor case of P.
  /// BinderIndex says which case binder (0-based) the recursive call is on (e.g., 0 for 'l', 2 for 'r').
  /// SpecializedArgStrings are [a2, ..., ak] (strings), with placeholders BINDER$<i> for case binders.
  /// </summary>
  public class ChildObligation {
    public int BinderIndex;
    public List<string> SpecializedArgStrings = new List<string>();
    public override string ToString() => $"binder#{BinderIndex} :: [{string.Join(", ", SpecializedArgStrings)}]";
  }
}
