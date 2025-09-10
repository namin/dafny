using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Dafny;

namespace Microsoft.Dafny {

  public class InductiveProofSketcher : ProofSketcher {
    private readonly ErrorReporter reporter;
    private InvariantInfo driverInvariant; // computed per-method
    private readonly InvariantAnalyzer analyzer;

    public InductiveProofSketcher(ErrorReporter reporter) : base(reporter) {
      this.reporter = reporter;
      this.analyzer = new InvariantAnalyzer(reporter);
    }

    public override Task<SketchResponse> GenerateSketch(SketchRequest input) {
      return Task.FromResult(new SketchResponse(GenerateProofSketch(input.ResolvedProgram, input.Method, input.LineNumber)));
    }

    private string GenerateProofSketch(Program program, Method method, int? lineNumber) {
      if (method == null) {
        return "// Error: No method resolved.";
      }
      // Determine if function-based induction should be applied
      if (method.Req.Count != 0 || method.Ens.Any(exp => exp.Attributes != null && exp.Attributes.Name == "induction_target")) {
        var functionCallExpr = CallsFunction(method);
        if (functionCallExpr != null) {
          return GenerateFunctionBasedInductionProofSketch(method, functionCallExpr);
        }
      }
      // Fallback to structural induction
      return GenerateStandardInductionProofSketch(method);
    }

    public List<(FunctionCallExpr, bool)> AllCalls(Method method) {
      var allCalls = new List<(FunctionCallExpr, bool)>();
      var reqAndEnsCalls = method.Req.Select(x => (x, true)).ToList().Concat(method.Ens.Select(x => (x, false))).ToList();
      foreach (var x in reqAndEnsCalls) {
        var exp = x.Item1;
        var isReq = x.Item2;
        var expCalls = new List<FunctionCallExpr>();
        FindFunctionCallExprs(exp.E, expCalls);
        if (!isReq) {
          expCalls.Reverse();
        }
        bool target = exp.Attributes != null && exp.Attributes.Name == "induction_target";
        foreach (var call in expCalls) {
          allCalls.Add((call, target));
        }
      }
      return allCalls;
    }

    private FunctionCallExpr? CallsFunction(Method method) {
      var list = AllCalls(method);
      FunctionCallExpr? result =
          list.FirstOrDefault(item => item.Item2).Item1
          ?? list.FirstOrDefault().Item1;
      return result;
    }

    private FunctionCallExpr? FindFunctionCallExpr(Expression expr) {
      if (expr is FunctionCallExpr funcCallExpr) {
        return funcCallExpr;
      }
      foreach (var subExpr in expr.SubExpressions) {
        var result = FindFunctionCallExpr(subExpr);
        if (result != null) {
          return result;
        }
      }
      return null;
    }

    private void FindFunctionCallExprs(Expression expr, List<FunctionCallExpr> res) {
      if (expr is FunctionCallExpr funcCallExpr) {
        res.Add(funcCallExpr);
      }
      foreach (var subExpr in expr.SubExpressions) {
        FindFunctionCallExprs(subExpr, res);
      }
    }

    public Expression SubstituteExpression(Expression expr, Dictionary<IVariable, Expression> map) {
      var substituter = new Substituter(null, map, new Dictionary<TypeParameter, Type>());
      return substituter.Substitute(expr);
    }

    public string GenerateFunctionBasedInductionProofSketch(Method method, FunctionCallExpr functionCallExpr) {
      driverInvariant = analyzer.TryBuildDriverInvariant(method);

      var sb = new StringBuilder();
      sb.AppendLine($"{Indent(0)}// Inductive proof using rule induction");
      sb.AppendLine($"{Indent(0)}// following function: {functionCallExpr.Function.Name}");

      var followedFunction = functionCallExpr.Function;
      if (followedFunction == null || followedFunction.Body == null) {
        sb.AppendLine($"{Indent(0)}// Cannot analyze the function body; no body defined.");
        return sb.ToString();
      }

      var map = MapFunctionParametersToArguments(followedFunction, functionCallExpr);

      var env = ReverseMapForVarValues(map);

      var substitutedBody = SubstituteExpression(followedFunction.Body, map);

      // Thread current constructor case + path conditions while walking.
      FollowExpr(sb, 0, substitutedBody, method, followedFunction, env, caseCtx: null, pathConds: new List<string>());

      return sb.ToString();
    }

    public Dictionary<string, IVariable> ReverseMapForVarValues(Dictionary<IVariable, Expression> map) {
      var env = new Dictionary<string, IVariable>();
      foreach (var kvp in map) {
        if (kvp.Value is NameSegment) {
          env[(kvp.Value as NameSegment).Name] = kvp.Key;
        } else if (kvp.Value?.Resolved is IdentifierExpr id && id.Var != null) {
          env[id.Var.Name] = kvp.Key;
        }
      }
      return env;
    }

    public Dictionary<IVariable, Expression> MapFunctionParametersToArguments(Function function, FunctionCallExpr functionCallExpr) {
      var map = new Dictionary<IVariable, Expression>();

      var parameters = function.Ins;
      if (parameters == null || functionCallExpr.Args.Count != parameters.Count) {
        throw new InvalidOperationException("Mismatch between number of function parameters and arguments.");
      }

      for (int i = 0; i < parameters.Count; i++) {
        var parameter = parameters[i];
        var argument = functionCallExpr.Args[i];
        map[parameter] = argument;
      }

      return map;
    }

    private string substitutePattern(ExtendedPattern p) {
      // Keep constructor name, normalize binder names to underscores
      return PatternUtils.Normalize(p);
    }

    private class CaseContext {
      public string CaseKey;            // "Node(_,_,_)"
      public List<IVariable> Binders;   // in-order
      public CaseContext(string key, List<IVariable> binders) { CaseKey = key; Binders = binders; }
    }

    private void FollowExpr(StringBuilder sb, int indent, Expression expr, Method method, Function function, Dictionary<string, IVariable> env,
                            CaseContext caseCtx = null, List<string> pathConds = null, bool noIndent = false) {
      if (ExprIsRecursiveCall(expr, function.Name)) {
        var functionCallExpr = (FunctionCallExpr)expr;
        var recursiveEnv = MapFunctionParametersToArguments(function, functionCallExpr);
        HandleRecursiveCall(sb, indent, method, env, recursiveEnv, caseCtx);
      } else if (expr is NestedMatchExpr nestedMatchExpr) {
        sb.AppendLine($"{Indent(indent)}match {PrintExpression(nestedMatchExpr.Source)} {{");
        foreach (var caseStmt in nestedMatchExpr.Cases) {
          var pattern = caseStmt.Pat;
          var binders = PatternUtils.AllPatternBoundVars(pattern);
          var extendedEnv = ExtendEnvironment(env, binders);

          var caseKey = PatternUtils.Normalize(pattern); // for invariant lookups
          var casePrint = PatternUtils.Pretty(pattern);       // for printing

          sb.AppendLine($"{Indent(indent + 1)}case {casePrint} => {{");

          if (driverInvariant != null && NeedsReveal(driverInvariant.Predicate)) {
            sb.AppendLine($"{Indent(indent)}reveal {driverInvariant.Predicate.Name}();");
          }

          var newCaseCtx = new CaseContext(caseKey, binders);
          FollowExpr(sb, indent + 2, caseStmt.Body, method, function, extendedEnv, newCaseCtx, pathConds);
          sb.AppendLine($"{Indent(indent + 1)}}}");
        }
        sb.AppendLine($"{Indent(indent)}}}");
      } else if (expr is LetExpr letExpr) {
        var variableMap = ExtractVariables(letExpr);
        var extendedEnv = new Dictionary<string, IVariable>(env);
        foreach (var kvp in variableMap) {
          extendedEnv[kvp.Key.Name] = kvp.Key;
        }
        foreach (var kvp in variableMap) {
          sb.AppendLine($"{Indent(indent)}var {kvp.Key.Name} := {PrintExpression(kvp.Value)};");
          FollowExpr(sb, indent, kvp.Value, method, function, extendedEnv, caseCtx, pathConds);
        }
        FollowExpr(sb, indent, letExpr.Body, method, function, extendedEnv, caseCtx, pathConds);
      } else if (expr is ITEExpr iteExpr) {
        var firstIndent = noIndent ? "" : Indent(indent);
        var cond = PrintExpression(iteExpr.Test);
        sb.AppendLine($"{firstIndent}if ({cond}) {{");
        var thnConds = new List<string>(pathConds ?? new List<string>()) { cond };
        FollowExpr(sb, indent + 1, iteExpr.Thn, method, function, env, caseCtx, thnConds);
        sb.Append($"{Indent(indent)}}}");
        if (iteExpr.Els is ITEExpr nestedIteExpr) {
          sb.Append(" else ");
          FollowExpr(sb, indent, nestedIteExpr, method, function, env, caseCtx, pathConds, true);
        } else {
          sb.AppendLine(" else {");
          var elsConds = new List<string>(pathConds ?? new List<string>()) { $"!({cond})" };
          FollowExpr(sb, indent + 1, iteExpr.Els, method, function, env, caseCtx, elsConds);
          sb.AppendLine($"{Indent(indent)}}}");
        }
      } else {
        foreach (var subExpr in expr.SubExpressions) {
          FollowExpr(sb, indent, subExpr, method, function, env, caseCtx, pathConds);
        }
      }
    }

    public Dictionary<string, IVariable> ExtendEnvironment(Dictionary<string, IVariable> env, List<IVariable> caseVars) {
      var newEnv = new Dictionary<string, IVariable>(env);
      foreach (var variable in caseVars) {
        newEnv[variable.Name] = variable;
      }
      return newEnv;
    }

    private bool ExprIsRecursiveCall(Expression expr, string functionName) {
      return expr is FunctionCallExpr funcCall && funcCall.Name == functionName;
    }

    private void HandleRecursiveCall(StringBuilder sb, int indent, Method method, Dictionary<string, IVariable> env,
                                     Dictionary<IVariable, Expression> recursiveMap, CaseContext caseCtx) {
      sb.Append($"{Indent(indent)}{method.Name}(");

      // We may need specialized extras from driverInvariant for parameters that are NOT part of the followed function.
      // Figure out which actual subtree we recurse on for the lemma's ADT parameter.
      Expression adtActual = null;
      // Heuristic: the lemma's ADT parameter is the first ADT-typed input.
      var lemmaAdtFormal = method.Ins.FirstOrDefault(p => p.Type != null && p.Type.IsDatatype);
      if (lemmaAdtFormal != null && env.TryGetValue(lemmaAdtFormal.Name, out var funAdtFormal)) {
        if (recursiveMap.TryGetValue(funAdtFormal, out var a)) {
          adtActual = a;
        }
      }
      string adtActualName = adtActual != null ? PrintExpression(adtActual) : null;
      int adtBinderIndex = -1;
      var adtVar = VarOf(adtActual);
      if (caseCtx != null && adtVar != null) {
        for (int i = 0; i < caseCtx.Binders.Count; i++) {
          if (ReferenceEquals(caseCtx.Binders[i], adtVar)) { adtBinderIndex = i; break; }
        }
      }

      // Prepare specialized extras if we have invariant info and we are under a case.
      List<string> specialized = null;
      if (driverInvariant != null && caseCtx != null && adtBinderIndex >= 0) {
        if (driverInvariant.CaseObligations.TryGetValue(caseCtx.CaseKey, out var list)) {
          var match = list.FirstOrDefault(o => o.BinderIndex == adtBinderIndex) ?? list.FirstOrDefault();
          if (match != null) {
            specialized = new List<string>(match.SpecializedArgStrings.Select(s => {
              var t = s;
              for (int i = 0; i < caseCtx.Binders.Count; i++) {
                t = Regex.Replace(t, $@"BINDER\${i}", caseCtx.Binders[i]?.Name ?? "_");
              }
              return t;
            }));
          }
        }
      }

      bool first = true;
      for (int i = 0; i < method.Ins.Count; i++) {
        var formal = method.Ins[i];
        if (!first) { sb.Append(", "); }
        first = false;

        // 1) If this lemma formal is an input to the followed function (available via env),
        //    mirror the actual from the recursive call.
        if (env.TryGetValue(formal.Name, out var funVar) && recursiveMap.TryGetValue(funVar, out var mirrored)) {
          sb.Append(PrintExpression(mirrored));
          continue;
        }

        // 2) Else, if this lemma formal aligns with a parameter slot of P, fill from specialized extras.
        if (driverInvariant != null) {
          var slotPair = driverInvariant.PParamIndexToMethodFormal.FirstOrDefault(kv => kv.Value == formal);
          if (slotPair.Value != null && specialized != null) {
            int slot = slotPair.Key - 1; // our 'specialized' list is [a2..ak]
            if (0 <= slot && slot < specialized.Count) {
              sb.Append(specialized[slot]);
              continue;
            }
          }
        }

        // 3) Fallback: keep the original formal (status quo)
        sb.Append(formal.Name);
      }

      sb.AppendLine(");");
    }

    public Dictionary<IVariable, Expression> ExtractVariables(LetExpr letExpr) {
      var variableMap = new Dictionary<IVariable, Expression>();

      for (int i = 0; i < letExpr.LHSs.Count; i++) {
        var casePattern = letExpr.LHSs[i];
        var rhs = letExpr.RHSs[i];

        foreach (var boundVar in casePattern.Vars) {
          variableMap[boundVar] = rhs;
        }
      }

      return variableMap;
    }

    private string GenerateStandardInductionProofSketch(Method method) {
      driverInvariant = null;
      var sb = new StringBuilder();

      var inductionVariables = FindInductionVariables(method);
      if (inductionVariables.Count > 0) {
        var proofSketch = BuildProofSketch(method, inductionVariables[0]);
        sb.Append(proofSketch);
      } else {
        sb.AppendLine($"{Indent(0)}// No suitable induction variable found.");
      }

      return sb.ToString();
    }

    public List<IVariable> FindInductionVariables(Method method) {
      var inductionVariables = new List<IVariable>();

      if (method.Decreases.Expressions.Count > 0) {
        var decreasesExpr = method.Decreases.Expressions.First();
        var decreasesVar = GetVariableFromExpression(decreasesExpr);
        if (decreasesVar != null) {
          inductionVariables.Add(decreasesVar);
        }
      }

      foreach (var formal in method.Ins) {
        if (IsNatType(formal.Type) || (formal.Type != null && formal.Type.IsDatatype)) {
          inductionVariables.Add(formal);
        }
      }

      return inductionVariables;
    }

    private IVariable? GetVariableFromExpression(Expression expr) {
      if (expr.Resolved is IdentifierExpr idExpr) {
        return idExpr.Var;
      }
      return null;
    }

    private bool IsNatType(Type type) {
      var userDefinedType = type as UserDefinedType;
      return userDefinedType != null && userDefinedType.Name == "nat";
    }

    public string BuildProofSketch(Method method, IVariable inductionVar) {
      var sb = new StringBuilder();

      if (inductionVar.Type.IsDatatype) {
        sb.AppendLine($"{Indent(0)}// Structural induction on {inductionVar.Name}");
        sb.AppendLine($"{Indent(0)}match {inductionVar.Name} {{");

        var datatypeDecl = inductionVar.Type.AsDatatype;
        foreach (var ctor in datatypeDecl.Ctors) {
          var formalParams = string.Join(", ", ctor.Formals.Select(f => f.Name));
          sb.AppendLine($"{Indent(1)}case {ctor.Name}({formalParams}) => {{");

          var recursiveFields = ctor.Formals
              .Where(f => f.Type.IsDatatype && f.Type.AsDatatype == inductionVar.Type.AsDatatype)
              .Select(f => f.Name);

          foreach (var recursiveField in recursiveFields) {
            sb.AppendLine(recursiveMethodCall(2, method, inductionVar, recursiveField));
          }

          sb.AppendLine($"{Indent(1)}}}");
        }

        sb.AppendLine($"{Indent(0)}}}");
      } else if (IsNatType(inductionVar.Type)) {
        sb.AppendLine($"{Indent(0)}// Natural induction on {inductionVar.Name}");
        sb.AppendLine($"{Indent(0)}if ({inductionVar.Name} == 0) {{");
        sb.AppendLine($"{Indent(1)}// Base case");
        sb.AppendLine($"{Indent(0)}}} else {{");
        sb.AppendLine(recursiveMethodCall(1, method, inductionVar, $"{inductionVar.Name} - 1"));
        sb.AppendLine($"{Indent(0)}}}");
      } else {
        sb.AppendLine($"{Indent(0)}// Cannot generate induction proof sketch for this type.");
      }

      return sb.ToString();
    }

    private string methodParams(Method method, IVariable inductionVar, string decreasedArg) {
      return string.Join(", ", method.Ins.Select(param => {
        if (param == inductionVar) {
          return decreasedArg;
        }
        return param.Name;
      }));
    }

    private string recursiveMethodCall(int indent, Method method, IVariable inductionVar, string decreasedArg) {
      return $"{Indent(indent)}{method.Name}({methodParams(method, inductionVar, decreasedArg)});";
    }

    private string PrintExpression(Expression expr) {
      return Printer.ExprToString(reporter.Options, expr);
    }

    private IVariable VarOf(Expression e) {
      if (e is NameSegment ns && ns.Resolved is IdentifierExpr id1) {
        return id1.Var;
      }
      if (e?.Resolved is IdentifierExpr id2) {
        return id2.Var;
      }
      return null;
    }
    
    private static bool HasAttribute(Attributes a, string name) {
      for (var it = a; it != null; it = it.Prev) {
        if (it.Name == name) {
          return true;
        }
      }
      return false;
    }
    private static bool NeedsReveal(Function f) {
      // Conservative: reveal only if the function is explicitly opaque or has no body.
      return f == null || f.Body == null || HasAttribute(f.Attributes, "opaque");
    }
  }
}
