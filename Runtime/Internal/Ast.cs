using System;
using System.Collections.Generic;
using GameKit.Scripting.Runtime;

namespace GameKit.Scripting.Internal
{
    public interface IVisitStatements
    {
        void EnterScope() { }
        void ExitScope() { }

        void Call(Call call) { }
        void If(If @if) { }
        void LocalVariableDecl(LocalVariableDecl localVariableDecl) { }
        void Assignment(Assignment assignment) { }
        void PropertyDecl(PropertyDecl propertyDecl) { }
        void FunctionDecl(FunctionDecl functionDecl) { }
        void ValueExpr(ValueExpr valueExpr) { }
        void VariableExpr(VariableExpr variableExpr) { }
        void GroupingExpr(GroupingExpr groupingExpr) { }
        void NegateExpr(NegateExpr negateExpr) { }
        void CmpExpr(CmpExpr cmpExpr) { }
        void MulExpr(MulExpr mulExpr) { }
        void AddExpr(AddExpr addExpr) { }
        void ObjectRef(ObjectRefExpr objectRefExpr) { }
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
        public Type ResultType;

        public abstract void Visit(IVisitStatements visitor);

        public abstract string ToString(string padding);
    }

    public abstract class Statement : Expression
    {
    }

    public class Call : Statement
    {
        public string Name;
        public List<Expression> Arguments;

        public override void Visit(IVisitStatements visitor)
        {
            visitor.Call(this);
            foreach (var arg in Arguments)
            {
                arg.Visit(visitor);
            }
        }

        public override string ToString(string padding)
        {
            var str = padding + $"[Call '{Name}'] <{ResultType}>";
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
        public List<Expression> TrueStatements;
        public List<Expression> FalseStatements;

        public override void Visit(IVisitStatements visitor)
        {
            visitor.If(this);
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
            var str = padding + $"[If] <{ResultType}>";
            str += "\n" + padding + "\tCondition:";
            str += "\n" + Condition.ToString(padding + "\t\t");
            str += "\n" + padding + "\tTrue:";
            foreach (var stmt in TrueStatements)
            {
                str += "\n" + stmt.ToString(padding + "\t\t");
            }
            str += "\n" + padding + "\tFalse:";
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

    public class LocalVariableDecl : Statement
    {
        public string VariableName;
        public Expression Value;

        public override void Visit(IVisitStatements visitor)
        {
            Value.Visit(visitor);
            visitor.LocalVariableDecl(this);
        }

        public override string ToString(string padding)
        {
            var str = padding + $"[LocalVariableDecl '{VariableName}'] <{ResultType}>\n";
            str += Value.ToString(padding + "\t");
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
            visitor.Assignment(this);
        }

        public override string ToString(string padding)
        {
            var str = padding + $"[Assignment '{VariableName}' {ScopeInfo}] <{ResultType}>\n";
            str += Value.ToString(padding + "\t");
            return str;
        }
    }
    public class PropertyDecl : Statement
    {
        public string Name;
        public string DeclaredTypeName;

        public override void Visit(IVisitStatements visitor)
        {
            visitor.PropertyDecl(this);
        }

        public override string ToString(string padding) => $"[Property '{Name}'] <{ResultType}>";
    }

    public class FunctionDecl : Statement
    {
        public string Name;
        public List<Expression> Statements;
        public List<string> ParameterNames;

        public override void Visit(IVisitStatements visitor)
        {
            visitor.EnterScope();
            visitor.FunctionDecl(this);
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
            return $"{Name}({string.Join(',', ParameterNames)})";
        }

        public override string ToString(string padding)
        {
            var str = padding + $"[DeclareFunc '{Name}' ({string.Join(',', ParameterNames)})] <{ResultType}>";
            foreach (var stmt in Statements)
            {
                str += "\n" + padding + stmt.ToString(padding + "\t");
            }
            return str;
        }
    }

    public enum ValueType { Null, Bool, Int, Float, Double, String }

    public class ValueExpr : Expression
    {
        public ValueType ValueType;
        public object Value;

        public override void Visit(IVisitStatements visitor)
        {
            visitor.ValueExpr(this);
        }

        public override string ToString(string padding) => padding + $"[Value '{Value}'] <{ResultType}>";
    }

    public class GroupingExpr : Expression
    {
        public Expression Value;

        public override void Visit(IVisitStatements visitor)
        {
            Value.Visit(visitor);
            visitor.GroupingExpr(this);
        }

        public override string ToString(string padding)
        {
            var str = padding + $"[Grouping] <{ResultType}>\n";
            str += Value.ToString(padding + "\t");
            return str;
        }
    }

    public class NegateExpr : Expression
    {
        public Expression Value;

        public override void Visit(IVisitStatements visitor)
        {
            visitor.NegateExpr(this);
            Value.Visit(visitor);
        }

        public override string ToString(string padding)
        {
            return padding + $"[Negate] <{ResultType}>\n" + Value.ToString(padding + "\t");
        }
    }

    public abstract class BinaryExpr : Expression
    {
        public Expression Left, Right;
    }

    public class AddExpr : BinaryExpr
    {
        public override void Visit(IVisitStatements visitor)
        {
            visitor.AddExpr(this);
            Left.Visit(visitor);
            Right.Visit(visitor);
        }

        public override string ToString(string padding)
        {
            return padding + $"[Add] <{ResultType}>\n" + Left.ToString(padding + "\t") + "\n" + Right.ToString(padding + "\t");
        }
    }

    public class MulExpr : BinaryExpr
    {
        public override void Visit(IVisitStatements visitor)
        {
            visitor.MulExpr(this);
            Left.Visit(visitor);
            Right.Visit(visitor);
        }

        public override string ToString(string padding)
        {
            return padding + $"[Mul] <{ResultType}>\n" + Left.ToString(padding + "\t") + "\n" + Right.ToString(padding + "\t");
        }
    }

    public enum CmpType
    {
        And,
        Equal,
        NotEqual,
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

        public override void Visit(IVisitStatements visitor)
        {
            visitor.CmpExpr(this);
            Left.Visit(visitor);
            Right.Visit(visitor);
        }

        public override string ToString(string padding)
        {
            return padding + $"[{Type}] <{ResultType}>\n" + Left.ToString(padding + "\t") + "\n" + Right.ToString(padding + "\t");
        }
    }


    public enum VariableSource { None, Local, Argument, Property }

    public class VariableExpr : Expression
    {
        public string Name;
        public ScopeVariableInfo ScopeInfo;

        public override void Visit(IVisitStatements visitor)
        {
            visitor.VariableExpr(this);
        }

        public override string ToString(string padding) => padding + $"[VariableExpr '{Name}' {ScopeInfo}] <{ResultType}>";
    }

    public class ObjectRefExpr : Expression
    {
        public string Name;

        public override void Visit(IVisitStatements visitor)
        {
            visitor.ObjectRef(this);
        }

        public override string ToString(string padding)
        {
            var str = padding + $"[ObjectRef '{Name}'] <{ResultType}>";
            return str;
        }
    }

    public class Ast
    {
        public string FileNameHint;
        public List<FunctionDecl> Functions;
        public List<PropertyDecl> Properties;

        public void Visit(IVisitStatements visitor)
        {
            visitor.EnterScope();
            foreach (var prop in Properties)
            {
                visitor.PropertyDecl(prop);
            }
            foreach (var func in Functions)
            {
                func.Visit(visitor);
            }
            visitor.ExitScope();
        }
    }
}