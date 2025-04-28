using System.Collections.Generic;
using System.IO;

namespace GameKit.Scripting.Internal
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
        Minus,
        CmpEq, // ==
        CmpGt, // >
        CmpLEq, // <=
        Star, // *
        CmpAnd, // &&

        Return,
        Function, // func
        Property, // prop
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
        public SourceLocation SourceLocation;

        public override string ToString()
        {
            if (Content != null)
                return $"'{Content}'";
            else
                return TokenTypeToString(Kind);
        }

        public static string TokenTypeToString(TokenKind kind) => kind switch
        {
            TokenKind.NonTerminal => "<non-terminal>",
            TokenKind.ParenOpen => "(",
            TokenKind.ParenClose => ")",
            TokenKind.BraceOpen => "{",
            TokenKind.BraceClose => "}",
            TokenKind.Semicolon => ";",
            TokenKind.Comma => ",",
            TokenKind.Equal => "=",
            TokenKind.Plus => "+",
            TokenKind.Minus => "-",
            TokenKind.CmpGt => ">",
            TokenKind.CmpLEq => "<=",
            TokenKind.Star => "*",
            TokenKind.CmpAnd => "&&",
            TokenKind.Return => "return",
            TokenKind.Function => "func",
            TokenKind.Property => "prop",
            TokenKind.If => "if",
            TokenKind.Else => "else",
            TokenKind.String => "<string>",
            TokenKind.Integer => "<integer>",
            TokenKind.Boolean => "<boolean>",
            TokenKind.Float => "<float>",
            TokenKind.Double => "<double>",
            TokenKind.CmpEq => "==",
            _ => throw new System.Exception("Missing case"),
        };
    }

    public class Lexer
    {
        List<Token> _tokens;
        int _currentTokenIdx;

        public Lexer(string code, string fileNameHint)
        {
            var result = new List<Token>();

            int line = 1;

            int lastI = 0;
            bool inComment = false;
            bool inString = false;
            for (int i = 0; i < code.Length; ++i)
            {
                var sourceLoc = new SourceLocation(fileNameHint, line);

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
                        result.Add(new Token { Kind = TokenKind.String, Content = code[lastI..i], SourceLocation = sourceLoc });

                        lastI = i + 1;
                        inString = false;
                    }
                }
                else
                {
                    var c = code[i];
                    var c2 = i + 1 < code.Length ? code[i + 1] : '\0';

                    // Start comment?
                    if (c == '/' && c2 == '/')
                    {
                        AddNonTerminal(result, code[lastI..i], sourceLoc);

                        inComment = true;
                    }
                    // Double delimiter?
                    else if (c == '&' && c2 == '&')
                    {
                        AddNonTerminal(result, code[lastI..i], sourceLoc);

                        result.Add(new Token { Kind = TokenKind.CmpAnd, SourceLocation = sourceLoc });

                        ++i;
                        lastI = i + 1;
                    }
                    else if (c == '=' && c2 == '=')
                    {
                        AddNonTerminal(result, code[lastI..i], sourceLoc);

                        result.Add(new Token { Kind = TokenKind.CmpEq, SourceLocation = sourceLoc });

                        ++i;
                        lastI = i + 1;
                    }
                    else if (c == '<' && c2 == '=')
                    {
                        AddNonTerminal(result, code[lastI..i], sourceLoc);

                        result.Add(new Token { Kind = TokenKind.CmpLEq, SourceLocation = sourceLoc });

                        ++i;
                        lastI = i + 1;
                    }
                    // Delimiter?
                    else if (c == ' ' || c == '\n'
                        || c == '(' || c == ')'
                        || c == '{' || c == '}'
                        || c == ';' || c == ',' || c == '=' || c == '"'
                        || c == '+' || c == '-' || c == '*' || c == '>')
                    {
                        AddNonTerminal(result, code[lastI..i], sourceLoc);

                        if (c == '"')
                        {
                            inString = true;
                        }
                        else
                        {
                            if (c != ' ' && c != '\n')
                            {
                                result.Add(new Token { Kind = GetTokenKind(c), SourceLocation = sourceLoc });
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
                AddNonTerminal(result, code[lastI..code.Length], new SourceLocation(fileNameHint, line));
            }

            AddSemicolons(result);

            _tokens = result;

            //
            File.WriteAllText("E:\\tk.txt", "");
            int printLine = 0;
            foreach (var tk in _tokens)
            {
                if (printLine != tk.SourceLocation.Line)
                {
                    File.AppendAllText("E:\\tk.txt", $"\n--- line {tk.SourceLocation} ---\n");
                    printLine = tk.SourceLocation.Line;
                }

                File.AppendAllText("E:\\tk.txt", $"{tk.Kind}\t");
                if (tk.Content != null)
                {
                    File.AppendAllText("E:\\tk.txt", $"'{tk.Content}'");
                }
                File.AppendAllText("E:\\tk.txt", $"\n");
            }
        }

        static void AddSemicolons(List<Token> tokens)
        {
            // See golang for semicolon rules

            for (int i = 0; i < tokens.Count; ++i)
            {
                var tk = tokens[i];

                // Note: assume new line at EOF
                var isLastTokenInLine = i + 1 >= tokens.Count || tk.SourceLocation.Line != tokens[i + 1].SourceLocation.Line;
                if (isLastTokenInLine)
                {
                    if (tk.Kind == TokenKind.ParenClose
                        || tk.Kind == TokenKind.Return
                        || tk.Kind == TokenKind.NonTerminal
                        || tk.Kind == TokenKind.Integer
                        || tk.Kind == TokenKind.Float
                        || tk.Kind == TokenKind.Double)
                    {
                        tokens.Insert(i + 1, new Token { Kind = TokenKind.Semicolon, SourceLocation = tk.SourceLocation });
                        ++i; // Skip the new token
                    }
                }
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
                throw new System.Exception($"Expected {Token.TokenTypeToString(kind)}, got {tk} (at {tk.SourceLocation})");

            ++_currentTokenIdx;
            return tk;
        }

        public Token Peek()
        {
            var tk = _tokens[_currentTokenIdx];
            return tk;
        }

        public void ThrowError(string str)
        {
            var tk = _tokens[_currentTokenIdx];
            ThrowError(str, tk.SourceLocation);
        }

        public void ThrowError(string str, SourceLocation location)
        {
            var tk = _tokens[_currentTokenIdx];
            throw new System.Exception($"{location}: {str}");
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
                '-' => TokenKind.Minus,
                '*' => TokenKind.Star,
                '>' => TokenKind.CmpGt,
                _ => throw new System.Exception("Todo"),
            };
        }

        static void AddNonTerminal(List<Token> tokens, string content, SourceLocation sourceLoc)
        {
            content = content.Trim();
            if (content.Length == 0)
                return;

            if (content == "return")
            {
                tokens.Add(new Token { Kind = TokenKind.Return, SourceLocation = sourceLoc });
            }
            else if (content == "if")
            {
                tokens.Add(new Token { Kind = TokenKind.If, SourceLocation = sourceLoc });
            }
            else if (content == "else")
            {
                tokens.Add(new Token { Kind = TokenKind.Else, SourceLocation = sourceLoc });
            }
            else if (content == "func")
            {
                tokens.Add(new Token { Kind = TokenKind.Function, SourceLocation = sourceLoc });
            }
            else if (content == "prop")
            {
                tokens.Add(new Token { Kind = TokenKind.Property, SourceLocation = sourceLoc });
            }
            else if (content == "true" || content == "false")
            {
                tokens.Add(new Token { Kind = TokenKind.Boolean, Content = content, SourceLocation = sourceLoc });
            }
            else if (int.TryParse(content, out int _))
            {
                tokens.Add(new Token { Kind = TokenKind.Integer, Content = content, SourceLocation = sourceLoc });
            }
            else if (double.TryParse(content, out double d))
            {
                if (d >= float.MinValue && d <= float.MaxValue)
                {
                    tokens.Add(new Token { Kind = TokenKind.Float, Content = content, SourceLocation = sourceLoc });
                }
                else
                {
                    tokens.Add(new Token { Kind = TokenKind.Double, Content = content, SourceLocation = sourceLoc });
                }
            }
            else
            {
                tokens.Add(new Token { Kind = TokenKind.NonTerminal, Content = content, SourceLocation = sourceLoc });
            }
        }
    }
}