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
            VariableSource.Property => "Property",
            _ => throw new System.Exception("case missing"),
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

    class HasReturnValueVisitor : IVisitStatements
    {
        public bool HasReturnValue = false;

        public void Expression(Expression expr)
        {
        }

        public void Statement(Statement stmt)
        {
            if (stmt is Return ret && ret.Value != null)
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
                    throw new System.Exception($"Unknown identifier '{var.Name}' (at {var.SourceLocation})");
            }
        }

        public void Property(PropertyDecl prop)
        {
            currentScope.LocalVariables[prop.Name] = new ScopeVariableInfo { Source = VariableSource.Property };
        }

        public void Statement(Statement stmt)
        {
            if (stmt is Assignment asn)
            {
                if (!currentScope.TryFind(asn.VariableName, out ScopeVariableInfo info))
                    throw new System.Exception($"Unknown identifier '{asn.VariableName}', did you mean := to declare a new variable? (at {asn.SourceLocation})");

                if (info.Source == VariableSource.Argument)
                    throw new System.Exception($"Assigning to argument is not allowed '{asn.VariableName}' (at {asn.SourceLocation})");

                asn.ScopeInfo = info;
            }

            if (stmt is LocalVariableDecl variableDecl)
            {
                if (currentScope.LocalVariables.ContainsKey(variableDecl.VariableName))
                    throw new System.Exception($"Variable already declared '{variableDecl.VariableName}' (at {variableDecl.SourceLocation})");

                var info = new ScopeVariableInfo { Source = VariableSource.Local };
                currentScope.LocalVariables.Add(variableDecl.VariableName, info);
            }

            if (stmt is FunctionDecl func)
            {
                for (int paramIdx = 0; paramIdx < func.Parameters.Count; paramIdx++)
                {
                    string param = func.Parameters[paramIdx];
                    currentScope.LocalVariables.Add(param, new ScopeVariableInfo
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
                var visitor = new HasReturnValueVisitor();
                func.Visit(visitor);
                func.HasReturnValue = visitor.HasReturnValue;
                // #todo ensure all branches return a value
            }

            var resolveVariables = new ResolveVariablesVisitor();
            ast.Visit(resolveVariables);
        }
    }
}