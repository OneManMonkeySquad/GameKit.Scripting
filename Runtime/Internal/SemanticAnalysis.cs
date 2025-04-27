using System.Collections.Generic;

namespace GameKit.Scripting.Internal
{
    public class ScopeVariableInfo
    {
        public VariableSource Source;
        public int ArgumentIdx;

        public override string ToString() => Source switch
        {
            VariableSource.None => "None",
            VariableSource.Local => "Local",
            VariableSource.Argument => $"Argument:{ArgumentIdx}",
            _ => throw new System.Exception("case missing"),
        };
    }

    public class Scope
    {
        public Scope ParentScope;
        public Dictionary<string, ScopeVariableInfo> Variables = new();

        public Scope(Scope parent = null)
        {
            ParentScope = parent;
        }

        public bool TryFind(string name, out ScopeVariableInfo source)
        {
            if (Variables.TryGetValue(name, out source))
                return true;

            return ParentScope?.TryFind(name, out source) ?? false;
        }
    }

    class HasReturnValueVisitor : IVisitAst
    {
        public bool HasReturnValue = false;

        public void Expression(Expression expr)
        {
        }

        public void Statement(Statement stmt)
        {
            if (stmt is Return)
            {
                HasReturnValue = true;
            }
        }
    }

    class ResolveVariablesVisitor : IVisitAst
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

        public void Expression(Expression expr)
        {
            if (expr is VariableExpr var)
            {
                if (!currentScope.TryFind(var.Name, out var.ScopeInfo))
                    throw new System.Exception($"${var.Line}: Unknown identifier '{var.Name}'");
            }
        }

        public void Statement(Statement stmt)
        {
            if (stmt is Assignment asn)
            {
                // #todo we just assume the variable has the same source but if not...
                currentScope.Variables[asn.VariableName] = new ScopeVariableInfo { Source = VariableSource.Local };
            }

            if (stmt is FunctionDecl func)
            {
                for (int paramIdx = 0; paramIdx < func.Parameters.Count; paramIdx++)
                {
                    string param = func.Parameters[paramIdx];
                    currentScope.Variables.Add(param, new ScopeVariableInfo
                    {
                        Source = VariableSource.Argument,
                        ArgumentIdx = paramIdx,
                    });
                }
            }
        }
    }

    public class SemanticAnalysis
    {
        public void Analyse(Ast ast)
        {
            foreach (var func in ast.Functions)
            {
                {
                    var visitor = new HasReturnValueVisitor();
                    func.Visit(visitor);
                    func.HasReturnValue = visitor.HasReturnValue;
                    // #todo ensure all branches return a value
                }

                {
                    var visitor = new ResolveVariablesVisitor();
                    func.Visit(visitor);
                }
            }
        }
    }
}