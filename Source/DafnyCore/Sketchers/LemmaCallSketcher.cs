using System.Collections.Generic;
using System.Text;
using DAST;
using Microsoft.Boogie.SMTLib;

namespace Microsoft.Dafny {
    public class LemmaCallSketcher : ProofSketcher {
        public LemmaCallSketcher(ErrorReporter reporter) : base(reporter) { }

        private StringBuilder? sb = null;
        private void reportError(IOrigin tok, string msg) {
            if (sb != null) {
                sb.AppendLine("// " + tok + ": "+ msg);
            }
            Reporter.Error(MessageSource.Documentation, tok, msg); // TODO: seems like a no-op?
        }
    
        public override string GenerateProofSketch(Program program, Method method, int? lineNumber) {
            var sketchBuilder = new StringBuilder();
            this.sb = sketchBuilder;

            void ProcessStatement(Statement stmt) {
                bool onTarget = !lineNumber.HasValue || IsTargetLine(stmt, lineNumber.Value);

                if (stmt.Attributes != null) {
                    var calledAttribute = ExtractAttribute(stmt.Attributes, "called");
                    var lemmaAttribute = ExtractAttribute(stmt.Attributes, "lemma");

                    if (calledAttribute != null && lemmaAttribute != null) {
                        var calledInfo = ParseCalledAttribute(calledAttribute);
                        var lemmaName = ExtractLemmaName(lemmaAttribute);
                        var lemma = GetLemma(program, lemmaName);

                        if (calledInfo != null && lemma != null) {
                            var lemmaCall = GenerateLemmaCall(lemma, calledInfo.Value);
                            if (!onTarget) {
                                sketchBuilder.AppendLine("// not on target: should be " + stmt.StartToken.line + " <= " + lineNumber + " <= " + (stmt.EndToken.line + 1));
                                sketchBuilder.Append("//");
                            }
                            sketchBuilder.AppendLine(lemmaCall);
                        }
                    }
                }

                // Recursively process nested statements
                if (stmt is BlockStmt blockStmt) {
                    foreach (var innerStmt in blockStmt.Body) {
                        ProcessStatement(innerStmt);
                    }
                } else if (stmt is IfStmt ifStmt) {
                    ProcessStatement(ifStmt.Thn);
                    if (ifStmt.Els != null) {
                        ProcessStatement(ifStmt.Els);
                    }
                } else if (stmt is WhileStmt whileStmt) {
                    ProcessStatement(whileStmt.Body);
                }
                // Add more cases for other statement types with nested bodies if needed
            }

            foreach (var stmt in method.Body.Body) {
                ProcessStatement(stmt);
            }

            return sketchBuilder.ToString();
        }

        private bool IsTargetLine(Statement stmt, int lineNumber) {
            return stmt.StartToken.line <= lineNumber && lineNumber <= stmt.EndToken.line;
        }

        private Attributes? ExtractAttribute(Attributes attributes, string attributeName) {
            while (attributes != null) {
                if (attributes.Name == attributeName) {
                    return attributes;
                }
                attributes = attributes.Prev;
            }
            return null;
        }

        private (string FunctionName, List<Expression> Arguments)? ParseCalledAttribute(Attributes calledAttribute) {
            if (calledAttribute.Args.Count < 1) {
                reportError(calledAttribute.StartToken, ":called attribute is missing arguments");
                return null;
            }

            // Resolve the function name (first argument) as an identifier
            var functionNameExpr = calledAttribute.Args[0] as NameSegment;
            if (functionNameExpr == null) {
                reportError(calledAttribute.StartToken, "Invalid function name in :called attribute");
                return null;
            }

            string functionName = functionNameExpr.Name;

            // Collect remaining arguments
            var arguments = new List<Expression>();
            for (int i = 1; i < calledAttribute.Args.Count; i++) {
                arguments.Add(calledAttribute.Args[i]);
            }

            return (functionName, arguments);
        }

        private string? ExtractLemmaName(Attributes lemmaAttribute) {
            if (lemmaAttribute.Args.Count != 1) {
                reportError(lemmaAttribute.StartToken, "Invalid :lemma attribute; expected one argument");
                return null;
            }

            return (lemmaAttribute.Args[0] as StringLiteralExpr).Value as string;
        }

        private Method? GetLemma(Program program, string lemmaName) {
            if (program.DefaultModuleDef is DefaultModuleDefinition defaultModule) {
                foreach (var topLevelDecl in defaultModule.TopLevelDecls) {
                    if (topLevelDecl is TopLevelDeclWithMembers classDecl) {
                        foreach (var member in classDecl.Members) {
                            if (member is Method) {
                                if (member.Name == lemmaName) {
                                    return member as Method;
                                }
                            }
                        }
                    }
                }
            }
            sb.AppendLine($"// lemma {lemmaName} to call was not found");
            return null;
        }

        private string GenerateLemmaCall(
            Method lemma,
            (string FunctionName, List<Expression> Arguments) calledInfo
        )
        {
            // Step 1: Get the formal parameters of the lemma
            var lemmaParameters = lemma.Ins;

            // Step 2: Start building the argument list
            var matchedArguments = new List<string>();
            var usedArguments = new HashSet<int>(); // Tracks indices of `calledInfo.Arguments` already used

            foreach (var parameter in lemmaParameters) {
                // Step 3: Try to find a matching argument by type from `calledInfo`
                var matchingArgumentIndex = FindMatchingArgumentByType(parameter, calledInfo.Arguments, usedArguments);
                if (matchingArgumentIndex >= 0) {
                    matchedArguments.Add(calledInfo.Arguments[matchingArgumentIndex].ToString());
                    usedArguments.Add(matchingArgumentIndex);
                } else {
                    // Step 4: Infer a default value for unmatched parameters
                    var inferredValue = InferDefaultArgument(lemma, parameter);
                    matchedArguments.Add(inferredValue);
                }
            }

            // Step 5: Generate the final call string
            return $"{lemma.Name}({string.Join(", ", matchedArguments)});";
        }

        private int FindMatchingArgumentByType(Formal parameter, List<Expression> arguments, HashSet<int> usedArguments) {
            for (int i = 0; i < arguments.Count; i++) {
                if (usedArguments.Contains(i)) {
                    continue; // Skip arguments already matched
                }

                if (IsTypeMatch(parameter.Type, arguments[i].Type)) {
                    return i;
                }
            }
            return -1; // No match found
        }

        private bool IsTypeMatch(Type parameterType, Type argumentType) {
            return parameterType.Equals(argumentType);
        }

        private string InferDefaultArgument(Method lemma, Formal parameter) {
            // Fallback to generic defaults based on parameter type
            return parameter.Type.ToString() switch {
                "map<int, ty>" => "map[]",
                "int" => "0",
                "bool" => "false",
                _ => "null" // Fallback for unsupported types
            };
        }
    }
}