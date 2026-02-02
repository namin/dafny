using System.Collections.Generic;
using Microsoft.Dafny.Auditor;

namespace Microsoft.Dafny;

public class HoleStmt : Statement, ICloneable<HoleStmt>, ICanFormat, ICanResolveNewAndOld {
  public readonly string Description;

  public HoleStmt Clone(Cloner cloner) {
    return new HoleStmt(cloner, this);
  }

  public HoleStmt(Cloner cloner, HoleStmt original) : base(cloner, original) {
    Description = original.Description;
  }

  [SyntaxConstructor]
  public HoleStmt(IOrigin origin, string description, Attributes attributes)
    : base(origin, attributes) {
    Description = description;
  }

  public override IEnumerable<Assumption> Assumptions(Declaration decl) {
    yield return new Assumption(decl, Origin, AssumptionDescription.HoleStatement);
  }

  public bool SetIndent(int indentBefore, TokenNewIndentCollector formatter) {
    return formatter.SetIndentAssertLikeStatement(this, indentBefore);
  }

  public override void GenResolve(INewOrOldResolver resolver, ResolutionContext context) {
    base.GenResolve(resolver, context);
    resolver.Reporter.Warning(MessageSource.Verifier, ResolutionErrors.ErrorId.r_hole_statement, Origin,
      "hole: this code path is not yet implemented");
  }

  public override void ResolveGhostness(ModuleResolver resolver, ErrorReporter reporter, bool mustBeErasable,
    ICodeContext codeContext,
    string proofContext, bool allowAssumptionVariables, bool inConstructorInitializationPhase) {
    IsGhost = false;
  }
}
