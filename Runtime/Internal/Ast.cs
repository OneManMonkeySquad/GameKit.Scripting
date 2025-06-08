using System;
using System.Collections.Generic;

namespace GameKit.Scripting.Internal
{
    public interface IVisitStatements
    {
        void EnterScope();
        void ExitScope();

        void Call(Call call);
        void If(If @if);
        void LocalVariableDecl(LocalVariableDecl localVariableDecl);
        void Assignment(Assignment assignment);
        void FunctionDecl(FunctionDecl functionDecl);
        void ValueExpr(ValueExpr valueExpr);
        void VariableExpr(VariableExpr variableExpr);
        void GroupingExpr(GroupingExpr groupingExpr);
        void NegateExpr(NegateExpr negateExpr);
        void CmpExpr(CmpExpr cmpExpr);
        void MulExpr(MulExpr mulExpr);
        void AddExpr(AddExpr addExpr);
        void ObjectRef(ObjectRefExpr objectRefExpr);
        void BranchExpr(BranchExpr branchExpr);
        void SyncExpr(SyncExpr syncExpr);
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
        public Type ResultType; // SA

        public abstract void Visit(IVisitStatements visitor);

        public abstract string ToString(string padding);
    }

    public class Call : Expression
    {
        public string Name;
        public List<Expression> Arguments;
        public bool IsCoroutine => Name.StartsWith("_");

        public bool IsBranch; // SA
        public bool IsSync; // SA

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
            if (IsBranch)
            {
                str += " @Branch";
            }
            if (IsSync)
            {
                str += " @Sync";
            }
            if (IsCoroutine)
            {
                str += " @Coroutine";
            }
            foreach (var arg in Arguments)
            {
                str += "\n" + arg.ToString(padding + "\t");
            }
            return str;
        }
    }

    public class If : Expression
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

    public class LocalVariableDecl : Expression
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

    public class Assignment : Expression
    {
        public string VariableName;
        public Expression Value;
        public ScopeVariableInfo ScopeInfo; // SA

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

    public class FunctionDecl : Expression
    {
        public string Name;
        public List<Expression> Body;
        public List<string> ParameterNames;

        public bool IsCoroutine => Name.StartsWith("_");

        public override void Visit(IVisitStatements visitor)
        {
            visitor.EnterScope();
            visitor.FunctionDecl(this);
            visitor.EnterScope(); // Double scope here because local variables can shadow parameters, so they need their own scope
            foreach (var stmt in Body)
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
            if (IsCoroutine)
            {
                str += " @Coroutine";
            }
            foreach (var stmt in Body)
            {
                str += "\n" + stmt.ToString(padding + "\t");
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

    public enum CmpType { And, Equal, NotEqual, Greater, Less, LessOrEqual, GreaterOrEqual }
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


    public enum VariableSource { None, Local, Argument }
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

    public class BranchExpr : Expression
    {
        public List<Expression> Body;

        public override void Visit(IVisitStatements visitor)
        {
            visitor.EnterScope();
            visitor.BranchExpr(this);
            foreach (var stmt in Body)
            {
                stmt.Visit(visitor);
            }
            visitor.ExitScope();
        }

        public override string ToString(string padding)
        {
            var str = padding + $"[BranchExpr] <{ResultType}>";
            foreach (var stmt in Body)
            {
                str += "\n" + stmt.ToString(padding + "\t");
            }
            return str;
        }
    }

    public class SyncExpr : Expression
    {
        public List<Expression> Body;

        public override void Visit(IVisitStatements visitor)
        {
            visitor.EnterScope();
            visitor.SyncExpr(this);
            foreach (var stmt in Body)
            {
                stmt.Visit(visitor);
            }
            visitor.ExitScope();
        }

        public override string ToString(string padding)
        {
            var str = padding + $"[SyncExpr] <{ResultType}>";
            foreach (var stmt in Body)
            {
                str += "\n" + stmt.ToString(padding + "\t");
            }
            return str;
        }
    }

    public class Ast
    {
        public string FileNameHint;
        public List<FunctionDecl> Functions;

        public void Visit(IVisitStatements visitor)
        {
            visitor.EnterScope();
            foreach (var func in Functions)
            {
                func.Visit(visitor);
            }
            visitor.ExitScope();
        }
    }
}