using System.Collections.Generic;
using GameKit.Scripting.Runtime;

namespace GameKit.Scripting.Internal
{
    public interface IVisitStatements
    {
        void Statement(Statement stmt);
        void Expression(Expression expr);
        void EnterScope() { }
        void ExitScope() { }
    }

    public interface IVisitAst : IVisitStatements
    {
        void Property(PropertyDecl prop);
    }

    public struct SourceLocation
    {
        public int Line;
        public string File;

        public SourceLocation(string file, int line)
        {
            File = file;
            Line = line;
        }

        public override string ToString() => $"<a href=\"{File}\" line=\"{Line}\">{File}:{Line}</a>";
    }

    public abstract class Expression
    {
        public SourceLocation SourceLocation;

        public virtual void Visit(IVisitStatements visitor)
        {
            visitor.Expression(this);
        }

        public abstract string ToString(string padding);
    }

    public abstract class Statement : Expression
    {
        public override void Visit(IVisitStatements visitor)
        {
            visitor.Statement(this);
        }
    }

    public class Call : Statement
    {
        public string Name;
        public List<Expression> Arguments;

        public override void Visit(IVisitStatements visitor)
        {
            visitor.Statement(this);
            foreach (var arg in Arguments)
            {
                arg.Visit(visitor);
            }
        }

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
        public List<Statement> FalseStatements;

        public override void Visit(IVisitStatements visitor)
        {
            visitor.Statement(this);
            Condition.Visit(visitor);
            foreach (var stmt in TrueStatements)
            {
                stmt.Visit(visitor);
            }
            if (FalseStatements != null)
            {
                foreach (var stmt in FalseStatements)
                {
                    stmt.Visit(visitor);
                }
            }
        }

        public override string ToString(string padding)
        {
            var str = padding + $"[If]";
            str += "\n" + padding + "\tCondition:";
            str += "\n" + Condition.ToString(padding + "\t\t");
            str += "\n" + padding + "\tTrue Body:";
            foreach (var stmt in TrueStatements)
            {
                str += "\n" + stmt.ToString(padding + "\t\t");
            }
            str += "\n" + padding + "\tFalse Body:";
            if (FalseStatements != null)
            {
                foreach (var stmt in FalseStatements)
                {
                    str += "\n" + stmt.ToString(padding + "\t\t");
                }
            }
            return str;
        }
    }

    public class Assignment : Statement
    {
        public string VariableName;
        public Expression Value;
        public ScopeVariableInfo ScopeInfo;

        public override void Visit(IVisitStatements visitor)
        {
            Value.Visit(visitor);
            visitor.Statement(this);
        }

        public override string ToString(string padding)
        {
            var str = padding + $"[Assignment {ScopeInfo} '{VariableName}']\n";
            str += Value.ToString(padding + "\t");
            return str;
        }
    }

    public class Return : Statement
    {
        /// <summary>
        /// May be null!
        /// </summary>
        public Expression Value;

        public override void Visit(IVisitStatements visitor)
        {
            visitor.Statement(this);
            Value?.Visit(visitor);
        }

        public override string ToString(string padding)
        {
            if (Value != null)
            {
                var str = padding + $"[Return]\n";
                str += Value.ToString(padding + "\t");
                return str;
            }
            else
            {
                return padding + $"[Return]";
            }
        }
    }

    public class PropertyDecl : Statement
    {
        public string Name;

        public override string ToString(string padding) => $"[Property '{Name}']";
    }

    public class FunctionDecl : Statement
    {
        public string Name;
        public List<Statement> Statements;
        public List<string> Parameters;
        public bool HasReturnValue;

        public override void Visit(IVisitStatements visitor)
        {
            visitor.EnterScope();
            visitor.Statement(this);
            visitor.EnterScope(); // Double scope here because local variables can shadow parameters, so they need their own scope
            foreach (var stmt in Statements)
            {
                stmt.Visit(visitor);
            }
            visitor.ExitScope();
            visitor.ExitScope();
        }

        public override string ToString()
        {
            return $"{Name}({string.Join(',', Parameters)}) {(HasReturnValue ? "@HasReturnValue" : "")}";
        }

        public override string ToString(string padding)
        {
            var str = padding + $"[DeclareFunc '{Name}' Parameters: {Parameters}]";
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

    public class GroupingExpr : Expression
    {
        public Expression Value;

        public override void Visit(IVisitStatements visitor)
        {
            visitor.Expression(this);
            Value.Visit(visitor);
        }

        public override string ToString(string padding)
        {
            var str = padding + $"[Grouping]\n";
            str += Value.ToString(padding + "\t");
            return str;
        }
    }

    public class NegateExpr : Expression
    {
        public Expression Value;

        public override void Visit(IVisitStatements visitor)
        {
            visitor.Expression(this);
            Value.Visit(visitor);
        }

        public override string ToString(string padding)
        {
            return padding + $"[Negate]\n" + Value.ToString(padding + "\t");
        }
    }

    public abstract class BinaryExpr : Expression
    {
        public Expression Left, Right;

        public override void Visit(IVisitStatements visitor)
        {
            visitor.Expression(this);
            Left.Visit(visitor);
            Right.Visit(visitor);
        }
    }

    public class AddExpr : BinaryExpr
    {
        public override string ToString(string padding)
        {
            return padding + $"[Add]\n" + Left.ToString(padding + "\t") + "\n" + Right.ToString(padding + "\t");
        }
    }

    public class MulExpr : BinaryExpr
    {
        public override string ToString(string padding)
        {
            return padding + $"[Mul]\n" + Left.ToString(padding + "\t") + "\n" + Right.ToString(padding + "\t");
        }
    }


    public enum CmpType
    {
        And,
        Equal,
        Greater,
        LessOrEqual
    }

    public class CmpExpr : BinaryExpr
    {
        public readonly CmpType Type;

        public CmpExpr(CmpType type)
        {
            Type = type;
        }

        public override string ToString(string padding)
        {
            return padding + $"[{Type}]\n" + Left.ToString(padding + "\t") + "\n" + Right.ToString(padding + "\t");
        }
    }


    public enum VariableSource { None, Local, Argument, Property }

    public class VariableExpr : Expression
    {
        public string Name;
        public ScopeVariableInfo ScopeInfo;

        public override string ToString(string padding) => padding + $"[Variable {ScopeInfo} '{Name}']";
    }


    public class Ast
    {
        public string FileNameHint;
        public List<FunctionDecl> Functions;
        public List<PropertyDecl> Properties;

        public void Visit(IVisitAst visitor)
        {
            visitor.EnterScope();
            foreach (var prop in Properties)
            {
                visitor.Property(prop);
            }
            foreach (var func in Functions)
            {
                func.Visit(visitor);
            }
            visitor.ExitScope();
        }
    }
}