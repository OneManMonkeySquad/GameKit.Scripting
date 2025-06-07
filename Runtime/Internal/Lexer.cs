using System.Collections.Generic;
using System.IO;

namespace GameKit.Scripting.Internal
{
    public enum TokenKind
    {
        NonTerminal,
        ParenOpen, // (
        ParenClose, // )
        BraceOpen, // {
        BraceClose, // }
        Semicolon,
        Comma,
        Equal, // =
        DeclareVariable, // :=
        Colon, // :
        Plus, // +
        Minus, // -
        At, // @
        Star, // *
        Function, // func
        If, // if
        Else, // else
        Branch, // branch

        CmpEq, // ==
        CmpNEq, // !=
        CmpGt, // >
        CmpLEq, // <=
        CmpAnd, // &&

        Null, // null
        String,
        Integer,
        Boolean,
        Float,
        Double
    }

    public struct Token
    {
        public TokenKind Kind;
        public string Content;
        public SourceLocation SourceLoc;

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
            TokenKind.DeclareVariable => ":=",
            TokenKind.Colon => ":",
            TokenKind.Plus => "+",
            TokenKind.Minus => "-",
            TokenKind.At => "@",
            TokenKind.Star => "*",
            TokenKind.Function => "func",
            TokenKind.If => "if",
            TokenKind.Else => "else",
            TokenKind.Branch => "branch",
            TokenKind.CmpGt => ">",
            TokenKind.CmpLEq => "<=",
            TokenKind.CmpEq => "==",
            TokenKind.CmpNEq => "!=",
            TokenKind.CmpAnd => "&&",
            TokenKind.Null => "null",
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
                        result.Add(new Token { Kind = TokenKind.String, Content = code[lastI..i], SourceLoc = sourceLoc });

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
                    else if (c == ':' && c2 == '=')
                    {
                        AddNonTerminal(result, code[lastI..i], sourceLoc);

                        result.Add(new Token { Kind = TokenKind.DeclareVariable, SourceLoc = sourceLoc });

                        ++i;
                        lastI = i + 1;
                    }
                    else if (c == '&' && c2 == '&')
                    {
                        AddNonTerminal(result, code[lastI..i], sourceLoc);

                        result.Add(new Token { Kind = TokenKind.CmpAnd, SourceLoc = sourceLoc });

                        ++i;
                        lastI = i + 1;
                    }
                    else if (c == '=' && c2 == '=')
                    {
                        AddNonTerminal(result, code[lastI..i], sourceLoc);

                        result.Add(new Token { Kind = TokenKind.CmpEq, SourceLoc = sourceLoc });

                        ++i;
                        lastI = i + 1;
                    }
                    else if (c == '!' && c2 == '=')
                    {
                        AddNonTerminal(result, code[lastI..i], sourceLoc);

                        result.Add(new Token { Kind = TokenKind.CmpNEq, SourceLoc = sourceLoc });

                        ++i;
                        lastI = i + 1;
                    }
                    else if (c == '<' && c2 == '=')
                    {
                        AddNonTerminal(result, code[lastI..i], sourceLoc);

                        result.Add(new Token { Kind = TokenKind.CmpLEq, SourceLoc = sourceLoc });

                        ++i;
                        lastI = i + 1;
                    }
                    // Delimiter?
                    else if (c == ' ' || c == '\n'
                        || c == '(' || c == ')'
                        || c == '{' || c == '}'
                        || c == ';' || c == ',' || c == '=' || c == '"'
                        || c == '+' || c == '-' || c == '*' || c == '>'
                        || c == ':' || c == '@')
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
                                result.Add(new Token { Kind = GetTokenKind(c), SourceLoc = sourceLoc });
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
                if (printLine != tk.SourceLoc.Line)
                {
                    File.AppendAllText("E:\\tk.txt", $"\n--- line {tk.SourceLoc.Line} ---\n");
                    printLine = tk.SourceLoc.Line;
                }

                File.AppendAllText("E:\\tk.txt", $"{tk.Kind}\t");
                if (tk.Content != null)
                {
                    File.AppendAllText("E:\\tk.txt", $"'{tk.Content}'");
                }
                File.AppendAllText("E:\\tk.txt", $"\n");
            }
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
                ':' => TokenKind.Colon,
                '@' => TokenKind.At,
                _ => throw new System.Exception("Todo"),
            };
        }

        static void AddNonTerminal(List<Token> tokens, string content, SourceLocation sourceLoc)
        {
            content = content.Trim();
            if (content.Length == 0)
                return;

            if (content == "if")
            {
                tokens.Add(new Token { Kind = TokenKind.If, SourceLoc = sourceLoc });
            }
            else if (content == "else")
            {
                tokens.Add(new Token { Kind = TokenKind.Else, SourceLoc = sourceLoc });
            }
            else if (content == "func")
            {
                tokens.Add(new Token { Kind = TokenKind.Function, SourceLoc = sourceLoc });
            }
            else if (content == "branch")
            {
                tokens.Add(new Token { Kind = TokenKind.Branch, SourceLoc = sourceLoc });
            }
            else if (content == "null")
            {
                tokens.Add(new Token { Kind = TokenKind.Null, SourceLoc = sourceLoc });
            }
            else if (content == "true" || content == "false")
            {
                tokens.Add(new Token { Kind = TokenKind.Boolean, Content = content, SourceLoc = sourceLoc });
            }
            else if (int.TryParse(content, out int _))
            {
                tokens.Add(new Token { Kind = TokenKind.Integer, Content = content, SourceLoc = sourceLoc });
            }
            else if (double.TryParse(content, out double d))
            {
                if (d >= float.MinValue && d <= float.MaxValue)
                {
                    tokens.Add(new Token { Kind = TokenKind.Float, Content = content, SourceLoc = sourceLoc });
                }
                else
                {
                    tokens.Add(new Token { Kind = TokenKind.Double, Content = content, SourceLoc = sourceLoc });
                }
            }
            else
            {
                tokens.Add(new Token { Kind = TokenKind.NonTerminal, Content = content, SourceLoc = sourceLoc });
            }
        }

        static void AddSemicolons(List<Token> tokens)
        {
            // See golang for semicolon rules

            for (int i = 0; i < tokens.Count; ++i)
            {
                var tk = tokens[i];

                // Note: assume new line at EOF
                var isLastTokenInLine = i + 1 >= tokens.Count || tk.SourceLoc.Line != tokens[i + 1].SourceLoc.Line;
                if (isLastTokenInLine)
                {
                    if (tk.Kind == TokenKind.ParenClose
                        || tk.Kind == TokenKind.NonTerminal
                        || tk.Kind == TokenKind.Null
                        || tk.Kind == TokenKind.Boolean
                        || tk.Kind == TokenKind.Integer
                        || tk.Kind == TokenKind.Float
                        || tk.Kind == TokenKind.Double
                        || tk.Kind == TokenKind.String)
                    {
                        tokens.Insert(i + 1, new Token { Kind = TokenKind.Semicolon, SourceLoc = tk.SourceLoc });
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

        public Token Accept(TokenKind kind, string what = null)
        {
            var tk = _tokens[_currentTokenIdx];
            if (tk.Kind != kind)
                throw new System.Exception($"Expected {(what != null ? $"({what}) " : "")}{Token.TokenTypeToString(kind)} got {tk} (at {tk.SourceLoc})");

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
            ThrowError(str, tk.SourceLoc);
        }

        public void ThrowError(string str, SourceLocation location)
        {
            var tk = _tokens[_currentTokenIdx];
            throw new System.Exception($"{str} (at {location})");
        }

        public bool Peek(params TokenKind[] kinds)
        {
            if (_currentTokenIdx >= _tokens.Count)
                return false;

            var tk = _tokens[_currentTokenIdx];
            foreach (var kind in kinds)
            {
                if (tk.Kind == kind)
                    return true;
            }
            return false;
        }

        public bool EndOfFile()
        {
            return _currentTokenIdx >= _tokens.Count;
        }
    }
}