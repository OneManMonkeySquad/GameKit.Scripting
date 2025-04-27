using System.Collections.Generic;
using System.IO;
using GameKit.Scripting.Runtime;

namespace GameKit.Scripting.Internal
{
    public class Parser
    {
        Lexer _lexer;

        /// <summary>
        /// Compile the passed code into an AST. Throws exceptions on error.
        /// </summary>
        public Ast ParseToAst(string str, string fileNameHint)
        {
            File.WriteAllText("E:\\stmt.txt", "");

            _lexer = new Lexer(str, fileNameHint);

            var functions = new List<FunctionDecl>();

            var statements = new List<Statement>();
            while (!_lexer.EndOfFile())
            {
                var stmt = ParseStatement();
                if (stmt is FunctionDecl f)
                {
                    functions.Add(f);
                }
                else
                {
                    statements.Add(stmt);
                }
            }

            functions.Add(new FunctionDecl { Name = "main", Statements = statements, Parameters = new() });

            //
            var ast = new Ast
            {
                FileNameHint = fileNameHint,
                Functions = functions
            };

            var sa = new SemanticAnalysis();
            sa.Analyse(ast);

            //
            foreach (var f in functions)
            {
                File.AppendAllText("E:\\stmt.txt", $"=== {f} ===\n");
                foreach (var st in f.Statements)
                {
                    File.AppendAllText("E:\\stmt.txt", $"{st.ToString("")}\n");
                }
            }

            return ast;
        }

        Statement ParseStatement()
        {
            if (_lexer.Peek(TokenKind.Return))
            {
                var tk = _lexer.Accept(TokenKind.Return);
                if (!_lexer.Peek(TokenKind.Semicolon))
                {
                    var value = ParseExpression();
                    _lexer.Accept(TokenKind.Semicolon);

                    return new Return { Value = value, SourceLocation = value.SourceLocation };
                }

                _lexer.Accept(TokenKind.Semicolon);

                return new Return { SourceLocation = tk.SourceLocation };
            }

            if (_lexer.Peek(TokenKind.If))
                return ParseIfStatement();

            if (_lexer.Peek(TokenKind.Function))
                return ParseFunction();

            var name = _lexer.Accept(TokenKind.NonTerminal);

            // Assignment?
            if (_lexer.Peek(TokenKind.Equal))
            {
                _lexer.Accept(TokenKind.Equal);
                var value = ParseExpression();
                _lexer.Accept(TokenKind.Semicolon);

                return new Assignment { VariableName = name.Content, Value = value, SourceLocation = name.SourceLocation };
            }

            // Call
            if (!_lexer.Peek(TokenKind.ParenOpen))
                _lexer.ThrowError("Expected on of =, (");

            var arguments = ParseArguments();

            _lexer.Accept(TokenKind.Semicolon);

            return new Call { Name = name.Content, Arguments = arguments, SourceLocation = name.SourceLocation };
        }

        Statement ParseIfStatement()
        {
            _lexer.Accept(TokenKind.If);

            // Condition
            var cond = ParseExpression();

            // True Body
            var statements = ParseBody();

            // Else?
            List<Statement> falseStatements = null;
            if (_lexer.Peek(TokenKind.Else))
            {
                _lexer.Accept(TokenKind.Else);

                falseStatements = ParseBody();
            }

            return new If
            {
                Condition = cond,
                TrueStatements = statements,
                FalseStatements = falseStatements,
                SourceLocation = cond.SourceLocation
            };
        }

        Statement ParseFunction()
        {
            _lexer.Accept(TokenKind.Function);

            var name = _lexer.Accept(TokenKind.NonTerminal);

            var parameters = ParseParameters();
            var statements = ParseBody();

            return new FunctionDecl
            {
                Name = name.Content,
                Statements = statements,
                Parameters = parameters,
                SourceLocation = name.SourceLocation
            };
        }

        List<Statement> ParseBody()
        {
            _lexer.Accept(TokenKind.BraceOpen);

            var statements = new List<Statement>();
            while (!_lexer.Peek(TokenKind.BraceClose))
            {
                statements.Add(ParseStatement());
            }
            _lexer.Accept(TokenKind.BraceClose);

            return statements;
        }

        Expression ParseExpression()
        {
            return ParseAnd();
        }

        /// <summary>
        /// Relational {( "&&" | "==" ) Relational}
        /// </summary>
        Expression ParseAnd()
        {
            var left = ParseRelational();

            while (_lexer.Peek(TokenKind.CmpAnd) || _lexer.Peek(TokenKind.CmpEq))
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
                }


            }

            return left;
        }

        /// <summary>
        /// PlusMinus {( ">" | "<=" ) PlusMinus}
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
                    case TokenKind.CmpLEq:
                        left = new CmpExpr(CmpType.LessOrEqual) { Left = left, Right = right, SourceLocation = left.SourceLocation };
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
                left = new AddExpr { Left = left, Right = right, SourceLocation = left.SourceLocation };
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
                left = new MulExpr { Left = left, Right = right, SourceLocation = left.SourceLocation };
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

                return new NegateExpr { Value = ParsePrimary(), SourceLocation = tk.SourceLocation };
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
            if (_lexer.Peek(TokenKind.String))
            {
                var val = _lexer.Accept(TokenKind.String);
                return new StringExpr { Content = val.Content, SourceLocation = val.SourceLocation };
            }
            else if (_lexer.Peek(TokenKind.Integer))
            {
                var val = _lexer.Accept(TokenKind.Integer);
                return new ValueExpr { Value = Value.FromInt(int.Parse(val.Content)), SourceLocation = val.SourceLocation };
            }
            else if (_lexer.Peek(TokenKind.Boolean))
            {
                var val = _lexer.Accept(TokenKind.Boolean);
                return new ValueExpr { Value = Value.FromBool(bool.Parse(val.Content)), SourceLocation = val.SourceLocation };
            }
            else if (_lexer.Peek(TokenKind.Float))
            {
                var val = _lexer.Accept(TokenKind.Float);
                return new ValueExpr { Value = Value.FromFloat(float.Parse(val.Content)), SourceLocation = val.SourceLocation };
            }
            else if (_lexer.Peek(TokenKind.Double))
            {
                var val = _lexer.Accept(TokenKind.Double);
                return new ValueExpr { Value = Value.FromDouble(double.Parse(val.Content)), SourceLocation = val.SourceLocation };
            }
            else
            {
                var name = _lexer.Accept(TokenKind.NonTerminal);

                // Call?
                if (_lexer.Peek(TokenKind.ParenOpen))
                {
                    var arguments = ParseArguments();
                    return new Call { Name = name.Content, Arguments = arguments, SourceLocation = name.SourceLocation };
                }
                else
                {
                    return new VariableExpr { Name = name.Content, SourceLocation = name.SourceLocation };
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
