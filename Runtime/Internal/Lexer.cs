using System.Collections.Generic;
using System.IO;

namespace GameKit.Scripting.Runtime
{
    public enum TokenKind
    {
        NonTerminal,
        /// <summary>
        /// (
        /// </summary>
        ParenOpen,
        /// <summary>
        /// )
        /// </summary>
        ParenClose,
        /// <summary>
        /// {
        /// </summary>
        BraceOpen,
        /// <summary>
        /// }
        /// </summary>
        BraceClose,
        Semicolon,
        Comma,
        Equal, // =
        Plus,
        CmpEq, // ==
        CmpGt, // >
        CmpLEq, // <=
        Star, // *
        CmpAnd, // &&

        Return,
        Function, // fn
        If,
        Else,

        String,
        Integer,
        Boolean,
        Float,
        Double
    }

    public class Token
    {
        public TokenKind Kind;
        public string Content;
        public int Line;

        public override string ToString()
        {
            if (Content != null)
                return $"{TokenTypeToString(Kind)}:{Content}";
            else
                return TokenTypeToString(Kind);
        }

        public static string TokenTypeToString(TokenKind kind) => kind switch
        {
            TokenKind.NonTerminal => "Non-Terminal",
            TokenKind.ParenOpen => "(",
            TokenKind.ParenClose => ")",
            TokenKind.BraceOpen => "{",
            TokenKind.BraceClose => "}",
            TokenKind.Semicolon => ";",
            TokenKind.Comma => ",",
            TokenKind.Equal => "=",
            TokenKind.Plus => "+",
            TokenKind.CmpGt => ">",
            TokenKind.CmpLEq => "<=",
            TokenKind.Star => "*",
            TokenKind.CmpAnd => "&&",
            TokenKind.Return => "return",
            TokenKind.Function => "fn",
            TokenKind.If => "if",
            TokenKind.Else => "else",
            TokenKind.String => "<string>",
            TokenKind.Integer => "<integer>",
            TokenKind.Boolean => "<boolean>",
            TokenKind.Float => "<float>",
            TokenKind.Double => "<double>",
            _ => throw new System.Exception("Missing case"),
        };
    }

    public class Lexer
    {
        List<Token> _tokens;
        int _currentTokenIdx;

        string _fileNameHint;

        public Lexer(string code, string fileNameHint)
        {
            _fileNameHint = fileNameHint;

            var result = new List<Token>();

            int line = 1;

            int lastI = 0;
            bool inComment = false;
            bool inString = false;
            for (int i = 0; i < code.Length; ++i)
            {
                if (inComment)
                {
                    if (code[i] == '\n')
                    {
                        inComment = false;
                        lastI = i + 1;
                    }
                }
                else if (inString)
                {
                    if (code[i] == '"')
                    {
                        result.Add(new Token { Kind = TokenKind.String, Content = code[lastI..i], Line = line });

                        lastI = i + 1;
                        inString = false;
                    }
                }
                else
                {
                    // Start comment?
                    if (i + 1 < code.Length && code[i] == '/' && code[i + 1] == '/')
                    {
                        AddNonTerminal(result, code[lastI..i], line);

                        inComment = true;
                    }
                    // Double delimiter?
                    else if (i + 1 < code.Length && code[i] == '&' && code[i + 1] == '&')
                    {
                        AddNonTerminal(result, code[lastI..i], line);

                        result.Add(new Token { Kind = TokenKind.CmpAnd, Line = line });

                        ++i;
                        lastI = i + 1;
                    }
                    else if (i + 1 < code.Length && code[i] == '=' && code[i + 1] == '=')
                    {
                        AddNonTerminal(result, code[lastI..i], line);

                        result.Add(new Token { Kind = TokenKind.CmpEq, Line = line });

                        ++i;
                        lastI = i + 1;
                    }
                    else if (i + 1 < code.Length && code[i] == '<' && code[i + 1] == '=')
                    {
                        AddNonTerminal(result, code[lastI..i], line);

                        result.Add(new Token { Kind = TokenKind.CmpLEq, Line = line });

                        ++i;
                        lastI = i + 1;
                    }
                    // Delimiter?
                    else if (code[i] == '(' || code[i] == ')'
                        || code[i] == '{' || code[i] == '}'
                        || code[i] == ';' || code[i] == ',' || code[i] == '=' || code[i] == ' ' || code[i] == '"'
                        || code[i] == '+' || code[i] == '*' || code[i] == '>')
                    {
                        AddNonTerminal(result, code[lastI..i], line);

                        if (code[i] == '"')
                        {
                            inString = true;
                        }
                        else
                        {
                            if (code[i] != ' ')
                            {
                                result.Add(new Token { Kind = GetTokenKind(code[i]), Line = line });
                            }
                        }

                        lastI = i + 1;
                    }
                }

                // This *always* needs to run, don't skip!
                if (code[i] == '\n')
                {
                    ++line;
                }
            }

            if (!inComment)
            {
                AddNonTerminal(result, code[lastI..code.Length], line);
            }


            _tokens = result;

            //
            File.WriteAllText("E:\\tk.txt", "");
            int printLine = 0;
            foreach (var tk in _tokens)
            {
                if (printLine != tk.Line)
                {
                    File.AppendAllText("E:\\tk.txt", $"\n--- line {tk.Line} ---\n");
                    printLine = tk.Line;
                }

                File.AppendAllText("E:\\tk.txt", $"{tk.Kind}\t");
                if (tk.Content != null)
                {
                    File.AppendAllText("E:\\tk.txt", $"'{tk.Content}'");
                }
                File.AppendAllText("E:\\tk.txt", $"\n");
            }
        }

        public Token Consume()
        {
            var tk = _tokens[_currentTokenIdx];
            ++_currentTokenIdx;
            return tk;
        }

        public Token Accept(TokenKind kind)
        {
            var tk = _tokens[_currentTokenIdx];
            if (tk.Kind != kind)
                throw new System.Exception($"{_fileNameHint}({tk.Line}): Unexpected token (expected '{Token.TokenTypeToString(kind)}', got '{tk}')");

            ++_currentTokenIdx;
            return tk;
        }

        public bool Peek(TokenKind kind)
        {
            if (_currentTokenIdx >= _tokens.Count)
                return false;

            if (_tokens[_currentTokenIdx].Kind != kind)
                return false;

            return true;
        }

        public bool EndOfFile()
        {
            return _currentTokenIdx >= _tokens.Count;
        }

        static TokenKind GetTokenKind(char c)
        {
            return c switch
            {
                '(' => TokenKind.ParenOpen,
                ')' => TokenKind.ParenClose,
                '{' => TokenKind.BraceOpen,
                '}' => TokenKind.BraceClose,
                ';' => TokenKind.Semicolon,
                ',' => TokenKind.Comma,
                '=' => TokenKind.Equal,
                '+' => TokenKind.Plus,
                '*' => TokenKind.Star,
                '>' => TokenKind.CmpGt,
                _ => throw new System.Exception("Todo"),
            };
        }

        static void AddNonTerminal(List<Token> tokens, string content, int line)
        {
            content = content.Trim();
            if (content.Length == 0)
                return;

            if (content == "return")
            {
                tokens.Add(new Token { Kind = TokenKind.Return, Line = line });
                return;
            }

            if (content == "if")
            {
                tokens.Add(new Token { Kind = TokenKind.If, Line = line });
                return;
            }

            if (content == "else")
            {
                tokens.Add(new Token { Kind = TokenKind.Else, Line = line });
                return;
            }

            if (content == "fn")
            {
                tokens.Add(new Token { Kind = TokenKind.Function, Line = line });
                return;
            }

            if (content == "true" || content == "false")
            {
                tokens.Add(new Token { Kind = TokenKind.Boolean, Content = content, Line = line });
                return;
            }

            if (int.TryParse(content, out int _))
            {
                tokens.Add(new Token { Kind = TokenKind.Integer, Content = content, Line = line });
                return;
            }

            if (double.TryParse(content, out double d))
            {
                if (d >= float.MinValue && d <= float.MaxValue)
                {
                    tokens.Add(new Token { Kind = TokenKind.Float, Content = content, Line = line });
                }
                else
                {
                    tokens.Add(new Token { Kind = TokenKind.Double, Content = content, Line = line });
                }
                return;
            }

            tokens.Add(new Token { Kind = TokenKind.NonTerminal, Content = content, Line = line });
        }
    }
}