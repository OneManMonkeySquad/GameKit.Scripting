using System;
using System.Collections.Generic;

namespace GameKit.Scripting.Internal
{
    public class ScopeVariableInfo
    {
        public VariableSource Source;
        public int ArgumentIdx;
        public Statement Declaration;

        public override string ToString() => Source switch
        {
            VariableSource.None => "None",
            VariableSource.Local => "Local",
            VariableSource.Argument => $"Argument:{ArgumentIdx}",
            _ => throw new Exception("case missing"),
        };
    }

    public class Scope
    {
        public Scope ParentScope;
        public Dictionary<string, ScopeVariableInfo> LocalVariables = new();

        public Scope(Scope parent = null)
        {
            ParentScope = parent;
        }

        public bool TryFind(string name, out ScopeVariableInfo source)
        {
            if (LocalVariables.TryGetValue(name, out source))
                return true;

            return ParentScope?.TryFind(name, out source) ?? false;
        }
    }

    class ResolveVariablesVisitor : IVisitStatements
    {
        Scope currentScope = null;

        public void EnterScope()
        {
            currentScope = new Scope(currentScope);
        }

        public void ExitScope()
        {
            currentScope = currentScope.ParentScope;
        }

        public void VariableExpr(VariableExpr var)
        {
            if (!currentScope.TryFind(var.Name, out var.ScopeInfo))
                throw new Exception($"Unknown identifier '{var.Name}' (at {var.SourceLocation})");
        }

        public void Assignment(Assignment asn)
        {
            if (!currentScope.TryFind(asn.VariableName, out ScopeVariableInfo info))
                throw new Exception($"Unknown identifier '{asn.VariableName}', did you mean := to declare a new variable? (at {asn.SourceLocation})");

            if (info.Source == VariableSource.Argument)
                throw new Exception($"Assigning to argument is not allowed '{asn.VariableName}' (at {asn.SourceLocation})");

            asn.ScopeInfo = info;
        }

        public void LocalVariableDecl(LocalVariableDecl variableDecl)
        {
            if (currentScope.LocalVariables.ContainsKey(variableDecl.VariableName))
                throw new Exception($"Variable already declared '{variableDecl.VariableName}' (at {variableDecl.SourceLocation})");

            var info = new ScopeVariableInfo
            {
                Source = VariableSource.Local,
                Declaration = variableDecl,
            };
            currentScope.LocalVariables.Add(variableDecl.VariableName, info);
        }

        public void FunctionDecl(FunctionDecl func)
        {
            for (int paramIdx = 0; paramIdx < func.ParameterNames.Count; paramIdx++)
            {
                string param = func.ParameterNames[paramIdx];
                currentScope.LocalVariables.Add(param, new ScopeVariableInfo
                {
                    Source = VariableSource.Argument,
                    ArgumentIdx = paramIdx,
                    Declaration = func,
                });
            }
        }
    }


    class ResolveTypes : IVisitStatements
    {
        public void Assignment(Assignment assignment)
        {
            assignment.ResultType = assignment.Value.ResultType;
        }

        public void CmpExpr(CmpExpr cmpExpr)
        {
            cmpExpr.ResultType = typeof(bool);
        }

        public void MulExpr(MulExpr mulExpr)
        {
            mulExpr.ResultType = typeof(object); // #todo
        }

        public void AddExpr(AddExpr addExpr)
        {
            addExpr.ResultType = typeof(object); // #todo
        }

        public void Call(Call call)
        {
            call.ResultType = typeof(object);
        }

        public void FunctionDecl(FunctionDecl functionDecl) { }

        public void GroupingExpr(GroupingExpr groupingExpr)
        {
            groupingExpr.ResultType = groupingExpr.Value.ResultType;
        }

        public void If(If @if)
        {
            @if.ResultType = typeof(void);
        }

        public void LocalVariableDecl(LocalVariableDecl localVariableDecl)
        {
            localVariableDecl.ResultType = localVariableDecl.Value.ResultType;
        }

        public void NegateExpr(NegateExpr negateExpr)
        {
            negateExpr.ResultType = negateExpr.Value.ResultType;
        }

        public void ValueExpr(ValueExpr valueExpr)
        {
            switch (valueExpr.ValueType)
            {
                case ValueType.Null:
                    valueExpr.ResultType = typeof(object);
                    break;
                case ValueType.Bool:
                    valueExpr.ResultType = typeof(bool);
                    break;
                case ValueType.Int:
                    valueExpr.ResultType = typeof(int);
                    break;
                case ValueType.Float:
                    valueExpr.ResultType = typeof(float);
                    break;
                case ValueType.Double:
                    valueExpr.ResultType = typeof(double);
                    break;
                case ValueType.String:
                    valueExpr.ResultType = typeof(string);
                    break;
            }
        }

        public void VariableExpr(VariableExpr variableExpr)
        {
            switch (variableExpr.ScopeInfo.Source)
            {
                case VariableSource.Local:
                    {
                        var decl = (LocalVariableDecl)variableExpr.ScopeInfo.Declaration;
                        variableExpr.ResultType = decl.ResultType;
                        break;
                    }
                case VariableSource.Argument:
                    {
                        variableExpr.ResultType = typeof(object); // #todo
                        break;
                    }
            }



        }
    }

    public class SemanticAnalysis
    {
        public void Analyse(Ast ast)
        {
            var resolveVariables = new ResolveVariablesVisitor();
            ast.Visit(resolveVariables);

            var resolveTypes = new ResolveTypes();
            ast.Visit(resolveTypes);
        }
    }
}