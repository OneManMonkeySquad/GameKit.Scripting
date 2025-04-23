using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Entities;
using UnityEngine;

namespace Los.Runtime
{
    public class Heap
    {
        Dictionary<int, string> _strings = new();
        int _nextStringIdx = 0;

        public int Store(string str)
        {
            _strings[_nextStringIdx] = str;
            return _nextStringIdx++;
        }

        public string Get(int idx)
        {
            return _strings[idx];
        }
    }

    public unsafe struct ExecContext
    {
        public EntityManager EntityManager;
        public Heap StringPool;
        public void* Stack;
    }

    public class Engine
    {
        Ast _ast;
        Dictionary<string, Func<ExecContext, Ast, Call, Value>> _buildinFunctions = new();
        Dictionary<string, Value> _vars = new();

        public Engine(Ast ast)
        {
            _ast = ast;
        }

        public void SetVar(string name, Value value)
        {
            _vars[name] = value;
        }

        public void RegisterAction(string name, Action<ExecContext, Value> act)
        {
            _buildinFunctions.Add(name, (ctx, ast, call) =>
            {
                if (call.Arguments.Count != 1)
                    throw new Exception($"{call.Line}: Wrong number of arguments (expected 1, was {call.Arguments.Count})");

                var arg0 = ExecuteExpression(call.Arguments[0], ctx);
                act(ctx, arg0);
                return Value.Null;
            });
        }

        public void RegisterAction(string name, Action<ExecContext, Value, Value> act)
        {
            _buildinFunctions.Add(name, (ctx, ast, call) =>
            {
                if (call.Arguments.Count != 2)
                    throw new Exception($"{call.Line}: Wrong number of arguments (expected 2, was {call.Arguments.Count})");

                var arg0 = ExecuteExpression(call.Arguments[0], ctx);
                var arg1 = ExecuteExpression(call.Arguments[1], ctx);
                act(ctx, arg0, arg1);
                return Value.Null;
            });
        }

        public void RegisterAction(string name, Action<ExecContext, Value, Value, Value> act)
        {
            _buildinFunctions.Add(name, (ctx, ast, call) =>
            {
                if (call.Arguments.Count != 3)
                    throw new Exception($"{call.Line}: Wrong number of arguments (expected 3, was {call.Arguments.Count})");

                var arg0 = ExecuteExpression(call.Arguments[0], ctx);
                var arg1 = ExecuteExpression(call.Arguments[1], ctx);
                var arg2 = ExecuteExpression(call.Arguments[2], ctx);
                act(ctx, arg0, arg1, arg2);
                return Value.Null;
            });
        }

        public void RegisterAction(string name, Action<ExecContext, Value, Value, Value, Value> act)
        {
            _buildinFunctions.Add(name, (ctx, ast, call) =>
            {
                if (call.Arguments.Count != 4)
                    throw new Exception($"{call.Line}: Wrong number of arguments (expected 4, was {call.Arguments.Count})");

                var arg0 = ExecuteExpression(call.Arguments[0], ctx);
                var arg1 = ExecuteExpression(call.Arguments[1], ctx);
                var arg2 = ExecuteExpression(call.Arguments[2], ctx);
                var arg3 = ExecuteExpression(call.Arguments[3], ctx);
                act(ctx, arg0, arg1, arg2, arg3);
                return Value.Null;
            });
        }

        public void RegisterFunc(string name, Func<ExecContext, Value, Value> act)
        {
            _buildinFunctions.Add(name, (ctx, ast, call) =>
            {
                if (call.Arguments.Count != 1)
                    throw new Exception($"{ast.FileNameHint}({call.Line}): Wrong number of arguments (expected 1, was {call.Arguments.Count})");

                var arg0 = ExecuteExpression(call.Arguments[0], ctx);
                return act(ctx, arg0);
            });
        }

        public void RegisterFunc(string name, Func<ExecContext, Value, Value, Value> act)
        {
            _buildinFunctions.Add(name, (ctx, ast, call) =>
            {
                if (call.Arguments.Count != 2)
                    throw new Exception($"{ast.FileNameHint}({call.Line}): Wrong number of arguments (expected 2, was {call.Arguments.Count})");

                var arg0 = ExecuteExpression(call.Arguments[0], ctx);
                var arg1 = ExecuteExpression(call.Arguments[1], ctx);
                return act(ctx, arg0, arg1);
            });
        }

        public void RegisterFunc(string name, Func<ExecContext, Value, Value, Value, Value> act)
        {
            _buildinFunctions.Add(name, (ctx, ast, call) =>
            {
                if (call.Arguments.Count != 3)
                    throw new Exception($"{ast.FileNameHint}({call.Line}): Wrong number of arguments (expected 3, was {call.Arguments.Count})");

                var arg0 = ExecuteExpression(call.Arguments[0], ctx);
                var arg1 = ExecuteExpression(call.Arguments[1], ctx);
                var arg2 = ExecuteExpression(call.Arguments[2], ctx);
                return act(ctx, arg0, arg1, arg2);
            });
        }

        public unsafe Value ExecuteFunc(string name, ExecContext ctx)
        {
            if (ctx.StringPool == null)
            {
                ctx.StringPool = new();
            }

            var mem = Marshal.AllocHGlobal(1024);
            ctx.Stack = mem.ToPointer();

            var func = _ast.Functions[name];

            for (int i = 0; i < func.Statements.Count; ++i)
            {
                var st = func.Statements[i];
                var (result, done) = ExecuteStatement(st, ctx);
                if (done)
                    return result;
            }

            //  Marshal.FreeHGlobal(ctx.HeapMemory);

            return Value.Null;
        }

        (Value, bool) ExecuteStatement(Statement stmt, ExecContext ctx)
        {
            if (stmt == null)
                throw new ArgumentNullException("stmt");

            // Debug.Log("Execute " + stmt.ToString(""));

            switch (stmt)
            {
                case Call call:
                    return (ExecuteCall(call, ctx), false);
                case Assignment a:
                    {
                        var val = ExecuteExpression(a.Value, ctx);
                        _vars[a.Variable] = val;
                        return (Value.Null, false);
                    }
                case If ifExpr:
                    {
                        var cnd = ExecuteExpression(ifExpr.Condition, ctx);
                        if (cnd.AsBool) // #todo assert is bool
                        {
                            for (int i = 0; i < ifExpr.TrueStatements.Count; ++i)
                            {
                                var stmt2 = ifExpr.TrueStatements[i];
                                var (result, done) = ExecuteStatement(stmt2, ctx);
                                if (done)
                                    return (result, true);
                            }
                        }
                        return (Value.Null, false);
                    }
                case Return ret:
                    return (ExecuteExpression(ret.Value, ctx), true);
                default:
                    throw new Exception("Todo " + stmt.ToString(""));
            }
        }

        Value ExecuteExpression(Expression expr, ExecContext ctx)
        {
            if (expr == null)
                throw new ArgumentNullException("expr");

            //  Debug.Log("Execute " + expr.ToString(""));

            switch (expr)
            {
                case Call call:
                    return ExecuteCall(call, ctx);
                case AddExpr add:
                    {
                        var left = ExecuteExpression(add.Left, ctx);
                        var right = ExecuteExpression(add.Right, ctx);
                        return (left.Type, right.Type) switch
                        {
                            (ValueType.Int, ValueType.Int) => Value.FromInt(left.AsInt + right.AsInt),
                            (ValueType.Float, ValueType.Float) => Value.FromFloat(left.AsFloat + right.AsFloat),
                            (ValueType.Float, ValueType.Double) => Value.FromDouble(left.AsFloat + right.AsDouble),
                            (ValueType.StringIdx, ValueType.StringIdx) => Value.FromStringIdx(ctx.StringPool.Store(ctx.StringPool.Get(left.AsStringIdx) + ctx.StringPool.Get(right.AsStringIdx))),
                            _ => throw new Exception("Unexpected types for add " + (left.Type, right.Type)),
                        };
                    }
                case MulExpr mul:
                    {
                        var left = ExecuteExpression(mul.Left, ctx);
                        var right = ExecuteExpression(mul.Right, ctx);
                        return (left.Type, right.Type) switch
                        {
                            (ValueType.Int, ValueType.Int) => Value.FromInt(left.AsInt * right.AsInt),
                            (ValueType.Double, ValueType.Int) => Value.FromDouble(left.AsDouble * right.AsInt),
                            _ => throw new Exception("Unexpected types for mul " + (left.Type, right.Type)),
                        };
                    }
                case GreaterExpr greater:
                    {
                        var left = ExecuteExpression(greater.Left, ctx);
                        var right = ExecuteExpression(greater.Right, ctx);
                        return (left.Type, right.Type) switch
                        {
                            (ValueType.Int, ValueType.Int) => Value.FromBool(left.AsInt > right.AsInt),
                            _ => throw new Exception("Unexpected types for greater " + (left.Type, right.Type)),
                        };
                    }
                case LEqualExpr leq:
                    {
                        var left = ExecuteExpression(leq.Left, ctx);
                        var right = ExecuteExpression(leq.Right, ctx);
                        return (left.Type, right.Type) switch
                        {
                            (ValueType.Int, ValueType.Int) => Value.FromBool(left.AsInt <= right.AsInt),
                            (ValueType.Float, ValueType.Int) => Value.FromBool(left.AsFloat <= right.AsInt),
                            _ => throw new Exception("Unexpected types for leq " + (left.Type, right.Type)),
                        };
                    }
                case AndExpr and:
                    {
                        var left = ExecuteExpression(and.Left, ctx);
                        var right = ExecuteExpression(and.Right, ctx);
                        return (left.Type, right.Type) switch
                        {
                            (ValueType.Bool, ValueType.Bool) => Value.FromBool(left.AsBool && right.AsBool),
                            _ => throw new Exception("Unexpected types for and " + (left.Type, right.Type)),
                        };
                    }
                case StringExpr str:
                    return Value.FromStringIdx(ctx.StringPool.Store(str.Content));
                case ValueExpr v:
                    return v.Value;
                case VariableExpr var:
                    return _vars[var.Name];
                default:
                    throw new Exception("Todo ExecuteExpression " + expr.ToString(""));
            }
        }

        Value ExecuteCall(Call call, ExecContext ctx)
        {
            if (_buildinFunctions.ContainsKey(call.Name))
            {
                var f = _buildinFunctions[call.Name];
                return f(ctx, _ast, call);
            }
            else if (_ast.Functions.ContainsKey(call.Name))
            {
                var f = _ast.Functions[call.Name];

                // Arguments
                if (call.Arguments.Count != f.Parameters.Count)
                    throw new Exception($"{call.Line}: Wrong number of arguments for call (expected {f.Parameters.Count}, got {call.Arguments.Count})");
                for (int i = 0; i < call.Arguments.Count; ++i)
                {
                    _vars[f.Parameters[i]] = ExecuteExpression(call.Arguments[i], ctx);
                }

                // Call
                return ExecuteFunc(call.Name, ctx);
            }
            else
                throw new Exception($"Function '{call.Name}' not found");
        }
    }
}