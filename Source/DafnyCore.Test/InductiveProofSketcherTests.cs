using System;
using System.Collections.Generic;
using Microsoft.Dafny;
using Xunit;
using System.IO;
using Xunit.Abstractions;
using System.Linq;
using System.Threading.Tasks;

public class InductiveProofSketcherTests {
    private readonly ITestOutputHelper _output;

    public InductiveProofSketcherTests(ITestOutputHelper output) {
        _output = output;
    }

    [Fact]
    public async Task TestGenerateProofSketchNat() {
        var programText = @"
        method TestMethod(n: nat)
            requires n > 0
            ensures n == n // Dummy postcondition
        {
            // Body can be empty or contain code.
        }
        ";
        var proofSketch = await setupAndGenerate(programText);
        // Assert that the generated proof sketch contains the lemma
        Assert.Contains("lemma", proofSketch);
        Assert.Contains("TestMethod_Induction", proofSketch); // Check that the lemma is named correctly
        Assert.Contains("n == 0", proofSketch); // Check for the base case
        Assert.Contains("n - 1", proofSketch); // Check for the inductive step
    }


    [Fact]
    public async Task TestGenerateProofSketchDatatype() {
        var programText = @"
        datatype Expr = constant(c: int) | variable(x: string) | plus(e1: Expr, e2: Expr) | times(e1: Expr, e2: Expr)
        method TestMethod(e: Expr)
        {
        }
        ";
        var proofSketch = await setupAndGenerate(programText);
        // Assert that the generated proof sketch contains the lemma
        Assert.Contains("lemma", proofSketch);
        Assert.Contains("TestMethod_Induction", proofSketch); // Check that the lemma is named correctly
        Assert.Contains("constant", proofSketch);
        Assert.Contains("variable", proofSketch);
        Assert.Contains("plus", proofSketch);
        Assert.Contains("times", proofSketch);
    }

    // Unit test helper
    async Task<string> setupAndGenerate(string programText) {
        // Initialize the error reporter
        var inputReader = new StringReader("");  // Empty input for now
        var outputWriter = new StringWriter();
        var errorWriter = new StringWriter();
        var options = new DafnyOptions(inputReader, outputWriter, errorWriter);
        var reporter = new ConsoleErrorReporter(options);

        // Parse the Dafny program from text
        Uri uri = new Uri("file:///test.dfy");
        Program program = await (new ProgramParser()).Parse(programText, uri, reporter);

        // Resolve the Dafny program using ProgramResolver
        var programResolver = new ProgramResolver(program);
        await programResolver.Resolve(CancellationToken.None);  // Resolve the program

        // Find the method in the parsed program
        var method = FindMethodInDecls(program.DefaultModuleDef.TopLevelDecls, "TestMethod");

        // Assert that the method is found
        Assert.NotNull(method);  // Ensure the method is found

        // Initialize the proof sketcher
        var sketcher = new InductiveProofSketcher(reporter);

        // Generate the proof sketch for the method
        var proofSketch = sketcher.GenerateProofSketch(method);

        _output.WriteLine("// Proof Sketch\n" + proofSketch);

        return proofSketch;
    }

    // Helper function to search for methods in the top-level declarations
    Method FindMethodInDecls(IEnumerable<INode> decls, string methodName) {
        foreach (var decl in decls) {
            _output.WriteLine("Looking at " + decl);
            _output.WriteLine("decl is a " + decl.GetType());
            if (decl is TopLevelDeclWithMembers) {
                _output.WriteLine("Looking inside!");
                var res = FindMethodInDecls(decl.Children, methodName);
                if (res != null) {
                    return res;
                }
            }
            else if (decl is Method methodDecl) {
                _output.WriteLine("found method " + methodDecl.Name);
                if (methodDecl.Name == methodName) {
                    return methodDecl;
                }
            }
        }
        return null;
    }
}
