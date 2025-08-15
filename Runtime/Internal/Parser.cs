using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace GameKit.Scripting.Internal
{
    public struct ParserResult
    {
        public bool Failed => Ast == null;

        public Ast Ast;

        public List<ParserException> Errors;
    }

    public class Parser
    {
        Lexer _lexer;

        /// <summary>
        /// Compile the passed code into an AST. Throws exceptions on error.
        /// </summary>
        public ParserResult ParseToAst(string str, string fileNameHint, Dictionary<string, MethodInfo> methods)
        {
            try
            {
                _lexer = new Lexer(str, fileNameHint);

                var functions = new List<FunctionDecl>();

                var statements = new List<Expression>();
                while (!_lexer.EndOfFile())
                {
                    var stmt = ParseStatement();
                    if (stmt is FunctionDecl func)
                    {
                        functions.Add(func);
                    }
                    else
                    {
                        statements.Add(stmt);
                    }
                }

                // Note: We emit "main" function even if it's empty - that's intentional
                functions.Add(new FunctionDecl
                {
                    Name = "main",
                    Body = statements,
                    ParameterNames = new(),
                    ResultType = typeof(object)
                });

                // Write AST before SA
                File.WriteAllText("E:\\ast.txt", "");
                File.AppendAllText("E:\\ast.txt", "\n");

                foreach (var f in functions)
                {
                    File.AppendAllText("E:\\ast.txt", $"{f.ToString("")}\n");
                    File.AppendAllText("E:\\ast.txt", "\n");
                }

                //
                var ast = new Ast
                {
                    FileNameHint = fileNameHint,
                    Functions = functions,
                };

                SemanticAnalysis.Analyse(ast, methods);

                // Write AST after SA
                File.WriteAllText("E:\\ast.txt", "");
                File.AppendAllText("E:\\ast.txt", "\n");

                foreach (var f in functions)
                {
                    File.AppendAllText("E:\\ast.txt", $"{f.ToString("")}\n");
                    File.AppendAllText("E:\\ast.txt", "\n");
                }

                return new ParserResult { Ast = ast };
            }
            catch (ParserException e)
            {
                return new ParserResult { Errors = new List<ParserException> { e } };
            }
        }

        Expression ParseStatement()
        {
            if (_lexer.Peek(TokenKind.Function))
                return ParseFunction();

            if (_lexer.Peek(TokenKind.If))
                return ParseExpression();

            if (_lexer.Peek(TokenKind.Integer) || _lexer.Peek(TokenKind.String) || _lexer.Peek(TokenKind.Float))
            {
                var value = ParseExpression();
                _lexer.Accept(TokenKind.Semicolon);
                return value;
            }

            if (_lexer.Peek(TokenKind.Branch))
            {
                var tk = _lexer.Consume();
                var statements = ParseBlock();
                return new BranchExpr
                {
                    Body = statements,
                    SourceLocation = tk.SourceLoc
                };
            }

            if (_lexer.Peek(TokenKind.Sync))
            {
                var tk = _lexer.Consume();
                var statements = ParseBlock();
                return new SyncExpr
                {
                    Body = statements,
                    SourceLocation = tk.SourceLoc
                };
            }

            //
            var name = _lexer.Accept(TokenKind.NonTerminal, "Identifier");

            // Assignment?
            if (_lexer.Peek(TokenKind.Equal))
            {
                _lexer.Consume();
                var value = ParseExpression();
                _lexer.Accept(TokenKind.Semicolon);
                return new Assignment { VariableName = name.Content, Value = value, SourceLocation = name.SourceLoc };
            }

            // Variable declaration?
            if (_lexer.Peek(TokenKind.DeclareVariable))
            {
                _lexer.Consume();
                var value = ParseExpression();
                _lexer.Accept(TokenKind.Semicolon);
                return new LocalVariableDecl { VariableName = name.Content, Value = value, SourceLocation = name.SourceLoc };
            }

            // Call
            if (!_lexer.Peek(TokenKind.ParenOpen))
                _lexer.ThrowError($"Incomplete expression {name}");

            var arguments = ParseArguments();

            _lexer.Accept(TokenKind.Semicolon);

            return new Call { Name = name.Content, Arguments = arguments, SourceLocation = name.SourceLoc };
        }

        /// <summary>
        /// "func" identifier "(" Parameters ")" "{" Body "}"
        /// </summary>
        FunctionDecl ParseFunction()
        {
            _lexer.Accept(TokenKind.Function);

            var name = _lexer.Accept(TokenKind.NonTerminal, "Function Name");

            var parameters = ParseParameters();
            var statements = ParseBlock();

            return new FunctionDecl
            {
                Name = name.Content,
                Body = statements,
                ParameterNames = parameters,
                SourceLocation = name.SourceLoc,
                ResultType = name.Content.StartsWith("_") ? typeof(IEnumerator) : typeof(object)
            };
        }

        /// <summary>
        /// "{" {Expression} "}"
        /// </summary>
        List<Expression> ParseBlock()
        {
            var braceOpenTk = _lexer.Accept(TokenKind.BraceOpen);

            var statements = new List<Expression>();
            while (!_lexer.Peek(TokenKind.BraceClose))
            {
                if (_lexer.EndOfFile())
                    throw new ParserException("Unexpected end of file (missing '}')", braceOpenTk.SourceLoc);

                var firstTk = _lexer.Peek();

                var stmt = ParseStatement();
                if (stmt is FunctionDecl)
                    throw new ParserException("Local functions are not supported", firstTk.SourceLoc);

                statements.Add(stmt);
            }
            _lexer.Accept(TokenKind.BraceClose);

            return statements;
        }

        Expression ParseExpression()
        {
            if (_lexer.Peek(TokenKind.If))
            {
                _lexer.Consume();

                // Condition
                var cond = ParseAnd();

                // True Body
                var statements = ParseBlock();

                // Else?
                List<Expression> falseStatements = null;
                if (_lexer.Peek(TokenKind.Else))
                {
                    _lexer.Consume();

                    if (_lexer.Peek(TokenKind.If))
                    {
                        falseStatements = new List<Expression>{
                            ParseExpression()
                        };
                    }
                    else
                    {
                        falseStatements = ParseBlock();
                    }
                }

                return new If
                {
                    Condition = cond,
                    TrueStatements = statements,
                    FalseStatements = falseStatements,
                    SourceLocation = cond.SourceLocation
                };
            }

            return ParseAnd();
        }

        /// <summary>
        /// Relational {( "&&" | "==" | "!=" ) Relational}
        /// </summary>
        Expression ParseAnd()
        {
            var left = ParseRelational();

            while (_lexer.Peek(TokenKind.CmpAnd, TokenKind.CmpEq, TokenKind.CmpNEq))
            {
                var tk = _lexer.Consume();
                switch (tk.Kind)
                {
                    case TokenKind.CmpAnd:
                        {
                            var right = ParseRelational();
                            left = new CmpExpr(CmpType.And) { Left = left, Right = right, SourceLocation = left.SourceLocation };
                            break;
                        }
                    case TokenKind.CmpEq:
                        {
                            var right = ParseRelational();
                            left = new CmpExpr(CmpType.Equal) { Left = left, Right = right, SourceLocation = left.SourceLocation };
                            break;
                        }
                    case TokenKind.CmpNEq:
                        {
                            var right = ParseRelational();
                            left = new CmpExpr(CmpType.NotEqual) { Left = left, Right = right, SourceLocation = left.SourceLocation };
                            break;
                        }
                }


            }

            return left;
        }

        /// <summary>
        /// PlusMinus {( ">" | "<" | "<=" | ">=" ) PlusMinus}
        /// </summary>
        Expression ParseRelational()
        {
            var left = ParsePlusMinus();

            while (_lexer.Peek(TokenKind.CmpGt) || _lexer.Peek(TokenKind.CmpLEq))
            {
                var tk = _lexer.Consume();

                var right = ParsePlusMinus();
                switch (tk.Kind)
                {
                    case TokenKind.CmpGt:
                        left = new CmpExpr(CmpType.Greater) { Left = left, Right = right, SourceLocation = left.SourceLocation };
                        break;
                    case TokenKind.CmpLt:
                        left = new CmpExpr(CmpType.Less) { Left = left, Right = right, SourceLocation = left.SourceLocation };
                        break;
                    case TokenKind.CmpLEq:
                        left = new CmpExpr(CmpType.LessOrEqual) { Left = left, Right = right, SourceLocation = left.SourceLocation };
                        break;
                    case TokenKind.CmpGEq:
                        left = new CmpExpr(CmpType.GreaterOrEqual) { Left = left, Right = right, SourceLocation = left.SourceLocation };
                        break;
                }

            }

            return left;
        }

        /// <summary>
        /// term {( "-" | "+" ) term}
        /// </summary>
        Expression ParsePlusMinus()
        {
            var left = ParseTerm();

            while (_lexer.Peek(TokenKind.Plus))
            {
                _lexer.Accept(TokenKind.Plus);

                var right = ParseTerm();
                left = new MathExpr(MathType.Add) { Left = left, Right = right, SourceLocation = left.SourceLocation };
            }

            return left;
        }

        /// <summary>
        /// unary {( "/" | "*" ) unary}
        /// </summary>
        Expression ParseTerm()
        {
            var left = ParseUnary();

            while (_lexer.Peek(TokenKind.Star))
            {
                _lexer.Accept(TokenKind.Star);

                var right = ParseUnary();
                left = new MathExpr(MathType.Mul) { Left = left, Right = right, SourceLocation = left.SourceLocation };
            }

            return left;
        }

        /// <summary>
        /// ["+" | "-"] primary
        /// </summary>
        Expression ParseUnary()
        {
            // #todo plus
            if (_lexer.Peek(TokenKind.Minus))
            {
                var tk = _lexer.Accept(TokenKind.Minus);

                return new NegateExpr { Value = ParsePrimary(), SourceLocation = tk.SourceLoc };
            }
            else
            {
                return ParsePrimary();
            }
        }

        /// <summary>
        /// number | ident
        /// </summary>
        Expression ParsePrimary()
        {
            if (_lexer.Peek(TokenKind.Null))
            {
                var tk = _lexer.Accept(TokenKind.Null);
                return new ValueExpr { Value = null, ValueType = ValueType.Null, SourceLocation = tk.SourceLoc };
            }
            else if (_lexer.Peek(TokenKind.String))
            {
                var tk = _lexer.Accept(TokenKind.String);
                return new ValueExpr { Value = tk.Content, ValueType = ValueType.String, SourceLocation = tk.SourceLoc };
            }
            else if (_lexer.Peek(TokenKind.Integer))
            {
                var tk = _lexer.Accept(TokenKind.Integer);
                return new ValueExpr { Value = int.Parse(tk.Content), ValueType = ValueType.Int, SourceLocation = tk.SourceLoc };
            }
            else if (_lexer.Peek(TokenKind.Boolean))
            {
                var tk = _lexer.Accept(TokenKind.Boolean);
                return new ValueExpr { Value = bool.Parse(tk.Content), ValueType = ValueType.Bool, SourceLocation = tk.SourceLoc };
            }
            else if (_lexer.Peek(TokenKind.Float))
            {
                var tk = _lexer.Accept(TokenKind.Float);
                return new ValueExpr { Value = float.Parse(tk.Content), ValueType = ValueType.Float, SourceLocation = tk.SourceLoc };
            }
            else if (_lexer.Peek(TokenKind.Double))
            {
                var tk = _lexer.Accept(TokenKind.Double);
                return new ValueExpr { Value = double.Parse(tk.Content), ValueType = ValueType.Double, SourceLocation = tk.SourceLoc };
            }
            else if (_lexer.Peek(TokenKind.ParenOpen))
            {
                _lexer.Accept(TokenKind.ParenOpen);
                var expr = ParseExpression();
                _lexer.Accept(TokenKind.ParenClose);

                return new GroupingExpr { Value = expr, SourceLocation = expr.SourceLocation };
            }
            else if (_lexer.Peek(TokenKind.At))
            {
                _lexer.Consume();
                var objectNameTk = _lexer.Accept(TokenKind.String);
                return new ObjectRefExpr { Name = objectNameTk.Content, ResultType = typeof(object), SourceLocation = objectNameTk.SourceLoc };
            }
            else
            {
                var name = _lexer.Accept(TokenKind.NonTerminal);

                // Assignment?
                if (_lexer.Peek(TokenKind.Equal))
                {
                    _lexer.Consume();
                    var value = ParseExpression();
                    return new Assignment { VariableName = name.Content, Value = value, SourceLocation = name.SourceLoc };
                }

                // Variable declaration?
                if (_lexer.Peek(TokenKind.DeclareVariable))
                {
                    _lexer.Consume();
                    var value = ParseExpression();
                    return new LocalVariableDecl { VariableName = name.Content, Value = value, SourceLocation = name.SourceLoc };
                }

                // Call?
                if (_lexer.Peek(TokenKind.ParenOpen))
                {
                    var arguments = ParseArguments();
                    return new Call { Name = name.Content, Arguments = arguments, SourceLocation = name.SourceLoc };
                }
                else
                {
                    return new VariableExpr { Name = name.Content, SourceLocation = name.SourceLoc };
                }
            }
        }

        List<string> ParseParameters()
        {
            var result = new List<string>();

            _lexer.Accept(TokenKind.ParenOpen);

            while (!_lexer.Peek(TokenKind.ParenClose))
            {
                var tk = _lexer.Accept(TokenKind.NonTerminal);
                result.Add(tk.Content);

                if (!_lexer.Peek(TokenKind.ParenClose))
                {
                    _lexer.Accept(TokenKind.Comma);
                }
            }

            _lexer.Accept(TokenKind.ParenClose);

            return result;
        }

        List<Expression> ParseArguments()
        {
            var arguments = new List<Expression>();

            _lexer.Accept(TokenKind.ParenOpen);

            while (!_lexer.Peek(TokenKind.ParenClose))
            {
                var expr = ParseExpression();
                arguments.Add(expr);

                if (!_lexer.Peek(TokenKind.ParenClose))
                {
                    _lexer.Accept(TokenKind.Comma);
                }
            }

            _lexer.Accept(TokenKind.ParenClose);

            return arguments;
        }
    }
}
