using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Boogie.SMTLib;
using Microsoft.Dafny;
using RAST;

namespace Microsoft.Dafny {

  public class InductiveProofSketcher : ProofSketcher {
    private readonly ErrorReporter reporter;

    public InductiveProofSketcher(ErrorReporter reporter) : base(reporter) {
      this.reporter = reporter;
    }

    public override string GenerateProofSketch(Method method, int lineNumber) {
      // Determine if function-based induction should be applied
      var functionCallExpr = RequiresCallsFunction(method);
      if (functionCallExpr != null) {
        return GenerateFunctionBasedInductionProofSketch(method, functionCallExpr);
      }
      // Fallback to structural induction
      return GenerateStandardInductionProofSketch(method);
    }

    private FunctionCallExpr? RequiresCallsFunction(Method method) {
      FunctionCallExpr? functionCallExpr = null;
      foreach (var req in method.Req) {
        functionCallExpr = FindFunctionCallExpr(req.E);
        if (functionCallExpr != null) {
          return functionCallExpr;
        }
      }
      return null;
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

    private string GenerateFunctionBasedInductionProofSketch(Method method, FunctionCallExpr functionCallExpr) {
      var sb = new StringBuilder();
      sb.AppendLine($"{Indent(0)}// Inductive proof using rule induction");
      sb.AppendLine($"{Indent(0)}// Function to follow: {functionCallExpr.Function.Name}");

      var followedFunction = functionCallExpr.Function;
      if (followedFunction == null || followedFunction.Body == null) {
        sb.AppendLine($"{Indent(0)}// Cannot analyze the function body; no body defined.");
        return sb.ToString();
      }

      var map = MapFunctionParametersToArguments(followedFunction, functionCallExpr);
      sb.AppendLine($"{Indent(0)}// Parameter to Argument Map:");
      foreach (var kvp in map) {
          sb.AppendLine($"{Indent(0)}// {kvp.Key.Name} => {PrintExpression(kvp.Value)} ({kvp.Value.GetType()})");
      }

      var env = ReverseMapForVarValues(map);
      sb.AppendLine($"{Indent(0)}// Env:");
      foreach (var kvp in env) {
          sb.AppendLine($"{Indent(0)}// {kvp.Key} => {kvp.Value.Name}");
      }
  
      var substitutedBody = SubstituteExpression(followedFunction.Body, map);
      sb.AppendLine($"{Indent(0)}// Substituted Function Body:");
      sb.AppendLine($"{Indent(0)}// {PrintExpression(substitutedBody)}");

      FollowExpr(sb, 0, substitutedBody, method, followedFunction, env);

      return sb.ToString();
    }

    private Dictionary<string, IVariable> ReverseMapForVarValues(Dictionary<IVariable, Expression> map)
    {
      var env = new Dictionary<string, IVariable>();
      foreach (var kvp in map) {
        if (kvp.Value is NameSegment) {
          env[(kvp.Value as NameSegment).Name] = kvp.Key;
        }
      }
      return env;
    }

    private Dictionary<IVariable, Expression> MapFunctionParametersToArguments(Function function, FunctionCallExpr functionCallExpr) {
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

    private void FollowExpr(StringBuilder sb, int indent, Expression expr, Method method, Function function, Dictionary<string, IVariable> env, bool noIndent = false) {
        if (ExprIsRecursiveCall(expr, function.Name)) {
            var functionCallExpr = (FunctionCallExpr)expr;
            var recursiveEnv = MapFunctionParametersToArguments(function, functionCallExpr);
            HandleRecursiveCall(sb, indent, method, env, recursiveEnv);
        } else if (expr is ITEExpr iteExpr) {
            var firstIndent = noIndent ? "" : Indent(indent);
            sb.AppendLine($"{firstIndent}if ({PrintExpression(iteExpr.Test)}) {{");
            FollowExpr(sb, indent + 1, iteExpr.Thn, method, function, env);
            sb.Append($"{Indent(indent)}}}");
            if (iteExpr.Els is ITEExpr nestedIteExpr) {
                sb.Append(" else ");
                FollowExpr(sb, indent, nestedIteExpr, method, function, env, true);
            } else {
                sb.AppendLine(" else {");
                FollowExpr(sb, indent + 1, iteExpr.Els, method, function, env);
                sb.AppendLine($"{Indent(indent)}}}");
            }
        } else if (expr is MatchExpr matchExpr) {
            sb.AppendLine($"{Indent(indent)}match {PrintExpression(matchExpr.Source)} {{");
            foreach (var caseExpr in matchExpr.Cases) {
                // TODO
                sb.AppendLine($"{Indent(indent + 1)}case {caseExpr.Arguments} => {{");
                FollowExpr(sb, indent + 2, caseExpr.Body, method, function, env);
                sb.AppendLine($"{Indent(indent + 1)}}}");
            }
            sb.AppendLine($"{Indent(indent)}}}");
        } else {
          foreach (var subExpr in expr.SubExpressions) {
              FollowExpr(sb, indent, subExpr, method, function, env);
          }
        }
    }
    private bool ExprIsRecursiveCall(Expression expr, string functionName) {
      return expr is FunctionCallExpr funcCall && funcCall.Name == functionName;
    }

    private void HandleRecursiveCall(StringBuilder sb, int indent, Method method, Dictionary<string, IVariable> env, Dictionary<IVariable, Expression> recursiveMap) {
        sb.Append($"{Indent(indent)}{method.Name}(");

        var first = true;
        foreach (var formal in method.Ins) {
            if (!first) { sb.Append(", "); }
            first = false;

            if (env.TryGetValue(formal.Name, out var funVar)) {
                sb.Append(recursiveMap[funVar]);
            } else {
                sb.Append(formal.Name);
            }
        }

        sb.AppendLine(");");
    }

    private Expression SubstituteExpression(Expression expr, Dictionary<IVariable, Expression> map) {
        var substituter = new Substituter(null, map, new Dictionary<TypeParameter, Type>());
        return substituter.Substitute(expr);
    }

    private string GenerateIfExpressionCase(Method method, ITEExpr iteExpr, FunctionCallExpr functionCallExpr) {
      var sb = new StringBuilder();

      // Handle the "if" condition
      sb.AppendLine($"{Indent(0)}if ({PrintExpression(iteExpr.Test)}) {{");

      // Handle the "then" branch
      if (ExprContainsRecursiveCall(new[] { iteExpr.Thn }, method.Name)) {
        sb.AppendLine($"{Indent(1)}// Recursive step in 'then' branch");
        sb.AppendLine($"{Indent(1)}{GenerateRecursiveCall(method, iteExpr.Thn)};");
      } else {
        sb.AppendLine($"{Indent(1)}// Base case logic in 'then' branch");
      }

      // Handle "else if" or "else"
      if (iteExpr.Els is ITEExpr nestedIteExpr) {
        sb.AppendLine($"{Indent(0)}}} else if ({PrintExpression(nestedIteExpr.Test)}) {{");
        sb.Append(GenerateIfExpressionCase(method, nestedIteExpr, functionCallExpr).TrimStart());
      } else {
        sb.AppendLine($"{Indent(0)}}} else {{");

        // Handle the "else" branch
        if (ExprContainsRecursiveCall(new[] { iteExpr.Els }, method.Name)) {
          sb.AppendLine($"{Indent(1)}// Recursive step in 'else' branch");
          sb.AppendLine($"{Indent(1)}{GenerateRecursiveCall(method, iteExpr.Els)};");
        } else {
          sb.AppendLine($"{Indent(1)}// Base case logic in 'else' branch");
        }

        sb.AppendLine($"{Indent(0)}}}");
      }

      return sb.ToString();
    }

    private string GenerateRecursiveCall(Method method, Expression recursiveExpr) {
      var sb = new StringBuilder();
      sb.Append($"{method.Name}(");

      // Handle arguments for recursive calls
      sb.Append(PrintExpression(recursiveExpr));

      sb.Append(")");
      return sb.ToString();
    }

    private string GenerateMatchCase(Method method, MatchExpr matchExpr, FunctionCallExpr functionCallExpr) {
      var sb = new StringBuilder();
      sb.AppendLine($"{Indent(0)}match {PrintExpression(matchExpr.Source)} {{");

      foreach (var caseExpr in matchExpr.Cases) {
        sb.AppendLine($"{Indent(1)}case {PrintExpression(caseExpr.Body)} => {{");

        // Check if the case body contains a recursive call
        if (ExprContainsRecursiveCall(new[] { caseExpr.Body }, method.Name)) {
          sb.AppendLine($"{Indent(2)}{GenerateRecursiveCall(method, functionCallExpr)};");
        } else {
          sb.AppendLine($"{Indent(2)}// Base case logic");
        }

        sb.AppendLine($"{Indent(1)}}}");
      }

      sb.AppendLine($"{Indent(0)}}}");
      return sb.ToString();
    }

    private string GenerateRecursiveCall(Method method, FunctionCallExpr functionCallExpr) {
      var sb = new StringBuilder();
      sb.Append($"{method.Name}(");

      for (int i = 0; i < functionCallExpr.Args.Count; i++) {
        if (i > 0) { sb.Append(", "); }

        // Decrease recursive arguments where applicable
        var arg = functionCallExpr.Args[i];
        sb.Append(PrintExpression(arg));
      }

      sb.Append(")");
      return sb.ToString();
    }

    private bool ExprContainsRecursiveCall(IEnumerable<Expression> expressions, string methodName) {
      foreach (var expr in expressions) {
        if (expr is FunctionCallExpr funcCall && funcCall.Name == methodName) {
          return true; // Recursive call found
        }

        if (expr is ITEExpr iteExpr) {
          if (ExprContainsRecursiveCall(new[] { iteExpr.Thn, iteExpr.Els }, methodName)) {
            return true;
          }
        }

        foreach (var subExpr in expr.SubExpressions) {
          if (ExprContainsRecursiveCall(new[] { subExpr }, methodName)) {
            return true;
          }
        }
      }
      return false;
    }

    private string GenerateStandardInductionProofSketch(Method method) {
      var sb = new StringBuilder();
      sb.AppendLine($"{Indent(0)}// Structural induction on {method.Name}");

      var inductionVariables = FindInductionVariables(method);
      if (inductionVariables.Count > 0) {
        var proofSketch = BuildProofSketch(method, inductionVariables);
        sb.Append(proofSketch);
      } else {
        sb.AppendLine($"{Indent(0)}// No suitable induction variable found.");
      }

      return sb.ToString();
    }

    private List<IVariable> FindInductionVariables(Method method) {
      var inductionVariables = new List<IVariable>();

      if (method.Decreases.Expressions.Count > 0) {
        var decreasesExpr = method.Decreases.Expressions.First();
        var decreasesVar = GetVariableFromExpression(decreasesExpr);
        if (decreasesVar != null) {
          inductionVariables.Add(decreasesVar);
          return inductionVariables;
        }
      }

      foreach (var formal in method.Ins) {
        if (IsNatType(formal.Type) || formal.Type.IsDatatype) {
          inductionVariables.Add(formal);
          break;
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

    private string BuildProofSketch(Method method, List<IVariable> inductionVariables) {
      var sb = new StringBuilder();
      var inductionVar = inductionVariables[0];

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
  }
}