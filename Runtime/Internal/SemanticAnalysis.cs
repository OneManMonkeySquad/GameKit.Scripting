using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Assertions;

namespace GameKit.Scripting.Internal
{
    public class ScopeVariableInfo
    {
        public VariableSource Source;
        public int ArgumentIdx;
        public Expression Declaration;

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

        public void Call(Call call) { }
        public void If(If @if) { }
        public void ValueExpr(ValueExpr valueExpr) { }
        public void GroupingExpr(GroupingExpr groupingExpr) { }
        public void NegateExpr(NegateExpr negateExpr) { }
        public void CmpExpr(CmpExpr cmpExpr) { }
        public void MathExpr(MathExpr mulExpr) { }
        public void ObjectRef(ObjectRefExpr objectRefExpr) { }
        public void BranchExpr(BranchExpr branchExpr) { }
        public void SyncExpr(SyncExpr syncExpr) { }
    }

    class ResolveTypesVisitor : IVisitStatements
    {
        readonly Dictionary<string, FunctionInfo> _methods = new();

        public ResolveTypesVisitor(Ast ast, Dictionary<string, MethodInfo> methods)
        {
            foreach (var (name, info) in methods)
            {
                _methods.Add(name, new FunctionInfo(info));
            }

            foreach (var func in ast.Functions)
            {
                _methods.Add(func.Name, new FunctionInfo(func));
            }
        }

        public void Assignment(Assignment assignment)
        {
            Assert.IsNotNull(assignment.Value.ResultType);

            assignment.ResultType = assignment.Value.ResultType;
        }

        public void CmpExpr(CmpExpr cmpExpr)
        {
            cmpExpr.ResultType = typeof(bool);
        }

        public void MathExpr(MathExpr expr)
        {
            var l = expr.Left.ResultType;
            var r = expr.Right.ResultType;

            // #todo lol
            if (l == typeof(int) && r == typeof(int))
            {
                expr.ResultType = typeof(int);
            }
            else if (l == typeof(string) && r == typeof(string))
            {
                expr.ResultType = typeof(string);
            }
            else if (l == typeof(int) && r == typeof(string))
            {
                expr.ResultType = typeof(string);
            }
            else if (l == typeof(string) && r == typeof(int))
            {
                expr.ResultType = typeof(string);
            }
            else if (l == typeof(string) && r == typeof(object))
            {
                expr.ResultType = typeof(string);
            }
            else if (l == null || r == null)
            {
                throw new Exception($"Invalid MathExpr one is null");
            }
            else
            {
                throw new Exception($"Invalid MathExpr {l}/{r}");
            }
        }

        public void Call(Call call)
        {
            if (!_methods.TryGetValue(call.Name, out FunctionInfo info))
                throw new Exception($"Function '{call.Name}' not found (at {call.SourceLocation})");

            Assert.IsNotNull(info.ReturnType);

            call.Target = info;
            call.ResultType = info.ReturnType;

            bool isCoroutineCall = info.ReturnType == typeof(IEnumerator);
            if (isCoroutineCall && !call.IsCoroutine)
                throw new Exception($"Call to {call.Name} is coroutine but name does not start with _ (at {call.SourceLocation})");

            if (!isCoroutineCall && call.IsCoroutine)
                throw new Exception($"Call to {call.Name} is not a coroutine but name does start with _ (at {call.SourceLocation})");
        }

        public void FunctionDecl(FunctionDecl functionDecl) { }

        public void GroupingExpr(GroupingExpr groupingExpr)
        {
            Assert.IsNotNull(groupingExpr.Value.ResultType);

            groupingExpr.ResultType = groupingExpr.Value.ResultType;
        }

        public void If(If expr)
        {
            Type trueType = typeof(object);
            if (expr.TrueStatements.Count > 0)
            {
                var lastStatement = expr.TrueStatements[^1];
                trueType = lastStatement.ResultType;
            }

            Type falseType = typeof(object);
            if (expr.FalseStatements?.Count > 0)
            {
                var lastStatement = expr.FalseStatements[^1];
                falseType = lastStatement.ResultType;
            }

            // #todo what to do if there'S no else? what if true and false types diverge?

            expr.ResultType = trueType;
        }

        public void LocalVariableDecl(LocalVariableDecl localVariableDecl)
        {
            Assert.IsNotNull(localVariableDecl.Value.ResultType);

            localVariableDecl.ResultType = localVariableDecl.Value.ResultType;
        }

        public void NegateExpr(NegateExpr negateExpr)
        {
            Assert.IsNotNull(negateExpr.Value.ResultType);

            negateExpr.ResultType = negateExpr.Value.ResultType;
        }

        public void ValueExpr(ValueExpr valueExpr)
        {
            valueExpr.ResultType = valueExpr.ValueType switch
            {
                ValueType.Null => typeof(object),
                ValueType.Bool => typeof(bool),
                ValueType.Int => typeof(int),
                ValueType.Float => typeof(float),
                ValueType.Double => typeof(double),
                ValueType.String => typeof(string),
                _ => throw new Exception("missing case"),
            };
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

        public void ObjectRef(ObjectRefExpr objectRefExpr)
        {
            objectRefExpr.ResultType = typeof(object);
        }

        public void BranchExpr(BranchExpr branchExpr)
        {
            branchExpr.ResultType = typeof(object);
        }

        public void SyncExpr(SyncExpr syncExpr)
        {
            syncExpr.ResultType = typeof(object);
        }

        public void EnterScope() { }
        public void ExitScope() { }
    }

    class ResolveCoroutines : IVisitStatements
    {
        class ScopeInfo
        {
            public bool IsBranch;
            public bool IsSync;
        }

        readonly Dictionary<string, object> _methods = new();
        readonly List<ScopeInfo> _scopeStack = new();

        public ResolveCoroutines(Ast ast, Dictionary<string, MethodInfo> methods)
        {
            foreach (var (name, info) in methods)
            {
                _methods.Add(name, info);
            }

            foreach (var func in ast.Functions)
            {
                _methods.Add(func.Name, func);
            }
        }

        FunctionDecl _currentFunctionDecl;
        public void FunctionDecl(FunctionDecl functionDecl)
        {
            _currentFunctionDecl = functionDecl;
        }

        public void Call(Call call)
        {
            if (!_methods.TryGetValue(call.Name, out object info))
                throw new Exception($"Function '{call.Name}' not found (at {call.SourceLocation})");

            if (!_currentFunctionDecl.IsCoroutine)
            {
                if (call.IsCoroutine && !_scopeStack[^1].IsBranch)
                    throw new Exception($"Coroutine call to {call.Name} in non-coroutine function: add branch (at {call.SourceLocation})");
            }

            if (call.IsCoroutine)
            {
                if (_scopeStack[^1].IsBranch) // #todo maybe need to recurse, might not be the top stack
                {
                    call.IsBranch = true;
                }
                if (_scopeStack[^1].IsSync) // #todo maybe need to recurse, might not be the top stack
                {
                    call.IsSync = true;
                }
            }
        }

        public void SyncExpr(SyncExpr syncExpr)
        {
            if (!_currentFunctionDecl.IsCoroutine)
                throw new Exception($"sync is only allowed in coroutine (at {syncExpr.SourceLocation})");

            _scopeStack[^1].IsSync = true;
        }

        public void BranchExpr(BranchExpr branchExpr)
        {
            _scopeStack[^1].IsBranch = true;
        }

        public void EnterScope()
        {
            _scopeStack.Add(new());
        }
        public void ExitScope()
        {
            _scopeStack.RemoveAt(_scopeStack.Count - 1);
        }

        public void If(If @if) { }
        public void LocalVariableDecl(LocalVariableDecl localVariableDecl) { }
        public void Assignment(Assignment assignment) { }
        public void ValueExpr(ValueExpr valueExpr) { }
        public void VariableExpr(VariableExpr variableExpr) { }
        public void GroupingExpr(GroupingExpr groupingExpr) { }
        public void NegateExpr(NegateExpr negateExpr) { }
        public void CmpExpr(CmpExpr cmpExpr) { }
        public void MathExpr(MathExpr mathExpr) { }
        public void ObjectRef(ObjectRefExpr objectRefExpr) { }
    }

    public static class SemanticAnalysis
    {
        public static void Analyse(Ast ast, Dictionary<string, MethodInfo> methods)
        {
            var resolveVariables = new ResolveVariablesVisitor();
            ast.Visit(resolveVariables);

            var resolveTypes = new ResolveTypesVisitor(ast, methods);
            ast.Visit(resolveTypes);

            var resolveCoroutines = new ResolveCoroutines(ast, methods);
            for (int i = 0; i < ast.Functions.Count; ++i) // #todo Coroutine status needs to propagate iteratively... god this is bad
            {
                ast.Visit(resolveCoroutines);
            }
        }
    }
}