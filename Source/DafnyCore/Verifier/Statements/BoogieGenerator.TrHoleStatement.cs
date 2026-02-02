namespace Microsoft.Dafny;

public partial class BoogieGenerator {
  private void TrHoleStmt(HoleStmt holeStmt, BoogieStmtListBuilder builder,
    ExpressionTranslator etran) {
    // hole = skip: emit nothing in Boogie (matches wp(Hole, Q) = Q from Olive)
    var desc = holeStmt.Description ?? "unspecified";
    AddComment(builder, holeStmt, $"hole statement: {desc}");

    if (options.TestGenOptions.Mode != TestGenerationOptions.Modes.None) {
      builder.AddCaptureState(holeStmt);
    }
  }
}
