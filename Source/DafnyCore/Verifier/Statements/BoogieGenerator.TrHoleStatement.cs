using Bpl = Microsoft.Boogie;

namespace Microsoft.Dafny;

public partial class BoogieGenerator {
  private void TrHoleStmt(HoleStmt holeStmt, BoogieStmtListBuilder builder,
    ExpressionTranslator etran) {
    var desc = holeStmt.Description ?? "unspecified";
    AddComment(builder, holeStmt, $"hole statement: {desc}");
    // assume false makes all downstream assertions on this path vacuously true.
    // This means only NON-hole code is verified: any failure is a structural error
    // in the sketch, not a missing hole filling.
    builder.Add(TrAssumeCmd(holeStmt.Origin, Bpl.Expr.False));
  }
}
