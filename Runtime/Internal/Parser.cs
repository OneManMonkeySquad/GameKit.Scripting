using System.Collections.Generic;
using System.IO;

namespace GameKit.Scripting.Runtime
{
    public class Expression
    {
        public int Line;

        public virtual string ToString(string padding)
        {
            return "Expression";
        }
    }

    public class Statement : Expression
    {
    }

    public class Call : Statement
    {
        public string Name;
        public List<Expression> Arguments;

        public override string ToString(string padding)
        {
            var str = padding + $"[Call '{Name}']";
            foreach (var arg in Arguments)
            {
                str += "\n" + arg.ToString(padding + "\t");
            }
            return str;
        }
    }

    public class If : Statement
    {
        public Expression Condition;
        public List<Statement> TrueStatements;

        public override string ToString(string padding)
        {
            var str = padding + $"[If]";
            str += "\n" + padding + "\tCondition:";
            str += "\n" + Condition.ToString(padding + "\t\t");
            str += "\n" + padding + "\tStatements:";
            foreach (var stmt in TrueStatements)
            {
                str += "\n" + stmt.ToString(padding + "\t\t");
            }
            return str;
        }
    }

    public class Assignment : Statement
    {
        public string Variable;
        public Expression Value;

        public override string ToString(string padding)
        {
            var str = padding + $"[Assignment '{Variable}']\n";
            str += Value.ToString(padding + "\t");
            return str;
        }
    }

    public class Return : Statement
    {
        public Expression Value;

        public override string ToString(string padding)
        {
            var str = padding + $"[Return]\n";
            str += Value.ToString(padding + "\t");
            return str;
        }
    }

    public class DeclareFunc : Statement
    {
        public string Name;
        public List<Statement> Statements;
        public List<string> Parameters;

        public override string ToString(string padding)
        {
            var str = padding + $"DeclareFunc '{Name}' Parameters: " + Parameters;
            foreach (var stmt in Statements)
            {
                str += "\n" + padding + stmt.ToString(padding + "\t");
            }
            return str;
        }
    }

    public class StringExpr : Expression
    {
        public string Content;

        public override string ToString(string padding) => padding + $"[String '{Content}']";
    }

    public class ValueExpr : Expression
    {
        public Value Value;

        public override string ToString(string padding) => padding + $"[Value {Value.Type} '{Value}']";
    }

    public class AddExpr : Expression
    {
        public Expression Left, Right;

        public override string ToString(string padding)
        {
            return padding + $"[Add]\n" + Left.ToString(padding + "\t") + "\n" + Right.ToString(padding + "\t");
        }
    }

    public class MulExpr : Expression
    {
        public Expression Left, Right;

        public override string ToString(string padding)
        {
            return padding + $"[Mul]\n" + Left.ToString(padding + "\t") + "\n" + Right.ToString(padding + "\t");
        }
    }

    public class AndExpr : Expression
    {
        public Expression Left, Right;

        public override string ToString(string padding)
        {
            return padding + $"[&&]\n" + Left.ToString(padding + "\t") + "\n" + Right.ToString(padding + "\t");
        }
    }

    public class GreaterExpr : Expression
    {
        public Expression Left, Right;

        public override string ToString(string padding)
        {
            return padding + $"[>]\n" + Left.ToString(padding + "\t") + "\n" + Right.ToString(padding + "\t");
        }
    }

    public class LEqualExpr : Expression
    {
        public Expression Left, Right;

        public override string ToString(string padding)
        {
            return padding + $"[<=]\n" + Left.ToString(padding + "\t") + "\n" + Right.ToString(padding + "\t");
        }
    }

    public class VariableExpr : Expression
    {
        public string Name;

        public override string ToString(string padding) => padding + $"[Variable '{Name}']";
    }

    public class Ast
    {
        public string FileNameHint;
        public Dictionary<string, DeclareFunc> Functions;
    }

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

            var functions = new Dictionary<string, DeclareFunc>();

            var statements = new List<Statement>();
            while (!_lexer.EndOfFile())
            {
                var stmt = ParseStatement();
                if (stmt is DeclareFunc f)
                {
                    functions.Add(f.Name, f);
                }
                else
                {
                    statements.Add(stmt);
                }
            }

            functions.Add("main", new DeclareFunc { Name = "main", Statements = statements, Parameters = new() });

            //
            foreach (var f in functions.Values)
            {
                File.AppendAllText("E:\\stmt.txt", $"=== {f.Name}({string.Join(',', f.Parameters)}) ===\n");
                foreach (var st in f.Statements)
                {
                    File.AppendAllText("E:\\stmt.txt", $"{st.ToString("")}\n");
                }
            }

            return new Ast
            {
                FileNameHint = fileNameHint,
                Functions = functions
            };
        }



        Statement ParseStatement()
        {
            if (_lexer.Peek(TokenKind.Return))
            {
                _lexer.Accept(TokenKind.Return);
                var value = ParseExpression();
                _lexer.Accept(TokenKind.Semicolon);

                return new Return { Value = value, Line = value.Line };
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

                return new Assignment { Variable = name.Content, Value = value, Line = name.Line };
            }

            // Call
            var arguments = ParseArguments();

            _lexer.Accept(TokenKind.Semicolon);

            return new Call { Name = name.Content, Arguments = arguments, Line = name.Line };
        }

        Statement ParseIfStatement()
        {
            _lexer.Accept(TokenKind.If);

            _lexer.Accept(TokenKind.ParenOpen);
            var cond = ParseExpression();
            _lexer.Accept(TokenKind.ParenClose);

            _lexer.Accept(TokenKind.BraceOpen);

            var statements = new List<Statement>();
            while (!_lexer.Peek(TokenKind.BraceClose))
            {
                statements.Add(ParseStatement());
            }
            _lexer.Accept(TokenKind.BraceClose);

            return new If { Condition = cond, TrueStatements = statements, Line = cond.Line };
        }

        Statement ParseFunction()
        {
            _lexer.Accept(TokenKind.Function);

            var name = _lexer.Accept(TokenKind.NonTerminal);

            var parameters = ParseParameters();

            _lexer.Accept(TokenKind.BraceOpen);

            var statements = new List<Statement>();
            while (!_lexer.Peek(TokenKind.BraceClose))
            {
                statements.Add(ParseStatement());
            }

            _lexer.Accept(TokenKind.BraceClose);

            return new DeclareFunc
            {
                Name = name.Content,
                Statements = statements,
                Parameters = parameters,
                Line = name.Line
            };
        }


        Expression ParseExpression()
        {
            return ParseAnd();
        }

        /// <summary>
        /// Relational {( "&&" ) Relational}
        /// </summary>
        Expression ParseAnd()
        {
            var left = ParseRelational();

            while (_lexer.Peek(TokenKind.And))
            {
                _lexer.Accept(TokenKind.And);

                var right = ParseRelational();
                left = new AndExpr { Left = left, Right = right, Line = left.Line };
            }

            return left;
        }

        /// <summary>
        /// PlusMinus {( ">" | "<=" ) PlusMinus}
        /// </summary>
        Expression ParseRelational()
        {
            var left = ParsePlusMinus();

            while (_lexer.Peek(TokenKind.Gt) || _lexer.Peek(TokenKind.LEq))
            {
                var tk = _lexer.Consume();

                var right = ParsePlusMinus();
                switch (tk.Kind)
                {
                    case TokenKind.Gt:
                        left = new GreaterExpr { Left = left, Right = right, Line = left.Line };
                        break;
                    case TokenKind.LEq:
                        left = new LEqualExpr { Left = left, Right = right, Line = left.Line };
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
                left = new AddExpr { Left = left, Right = right, Line = left.Line };
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
                left = new MulExpr { Left = left, Right = right, Line = left.Line };
            }

            return left;
        }

        /// <summary>
        /// ["+" | "-"] primary
        /// </summary>
        Expression ParseUnary()
        {
            return ParsePrimary();
        }

        /// <summary>
        /// number | ident
        /// </summary>
        Expression ParsePrimary()
        {
            if (_lexer.Peek(TokenKind.String))
            {
                var val = _lexer.Accept(TokenKind.String);
                return new StringExpr { Content = val.Content, Line = val.Line };
            }
            else if (_lexer.Peek(TokenKind.Integer))
            {
                var val = _lexer.Accept(TokenKind.Integer);
                return new ValueExpr { Value = Value.FromInt(int.Parse(val.Content)), Line = val.Line };
            }
            else if (_lexer.Peek(TokenKind.Boolean))
            {
                var val = _lexer.Accept(TokenKind.Boolean);
                return new ValueExpr { Value = Value.FromBool(bool.Parse(val.Content)), Line = val.Line };
            }
            else if (_lexer.Peek(TokenKind.Float))
            {
                var val = _lexer.Accept(TokenKind.Float);
                return new ValueExpr { Value = Value.FromFloat(float.Parse(val.Content)), Line = val.Line };
            }
            else if (_lexer.Peek(TokenKind.Double))
            {
                var val = _lexer.Accept(TokenKind.Double);
                return new ValueExpr { Value = Value.FromDouble(double.Parse(val.Content)), Line = val.Line };
            }
            else
            {
                var name = _lexer.Accept(TokenKind.NonTerminal);

                // Call?
                if (_lexer.Peek(TokenKind.ParenOpen))
                {
                    var arguments = ParseArguments();
                    return new Call { Name = name.Content, Arguments = arguments, Line = name.Line };
                }
                else
                {
                    return new VariableExpr { Name = name.Content, Line = name.Line };
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
