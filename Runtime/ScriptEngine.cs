using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace GameKit.Scripting.Runtime
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

    public class ScriptEngine
    {
        Dictionary<string, Func<ExecContext, Ast, Call, Value>> _buildinFunctions = new();
        Dictionary<string, Value> _vars = new();

        public ScriptEngine()
        {
            RegisterBuildinFunctions();
        }

        void RegisterBuildinFunctions()
        {
            RegisterAction("print", Print);
            RegisterFunc("str", str);
            RegisterFunc("sin", sin);
            RegisterFunc("as_float", as_float);
            RegisterFunc("has_component", has_component);
        }

        public static string Output;

        static void Print(ExecContext ctx, Value obj)
        {
            Debug.Log(obj.ToString(ctx));
            if (Output != null)
            {
                Output += obj.ToString(ctx);
            }
        }

        static Value str(ExecContext ctx, Value str)
        {
            return Value.FromStringIdx(ctx.StringPool.Store(str.ToString()));
        }

        static Value sin(ExecContext ctx, Value t)
        {
            return Value.FromDouble(math.sin((double)t));
        }

        static Value as_float(ExecContext ctx, Value t)
        {
            switch (t.Type)
            {
                case ValueType.Int: return Value.FromFloat((int)t);
                case ValueType.Float: return t;
                case ValueType.Double: return Value.FromFloat((float)(double)t);
                default: throw new System.Exception("Unexpected type");
            }
        }

        static Value has_component(ExecContext ctx, Value entity, Value type_idx)
        {
            var val = ctx.EntityManager.HasComponent((Entity)entity, ComponentType.ReadOnly((int)type_idx));
            return Value.FromBool(val);
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

                var arg0 = ExecuteExpression(ast, call.Arguments[0], ctx);
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

                var arg0 = ExecuteExpression(ast, call.Arguments[0], ctx);
                var arg1 = ExecuteExpression(ast, call.Arguments[1], ctx);
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

                var arg0 = ExecuteExpression(ast, call.Arguments[0], ctx);
                var arg1 = ExecuteExpression(ast, call.Arguments[1], ctx);
                var arg2 = ExecuteExpression(ast, call.Arguments[2], ctx);
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

                var arg0 = ExecuteExpression(ast, call.Arguments[0], ctx);
                var arg1 = ExecuteExpression(ast, call.Arguments[1], ctx);
                var arg2 = ExecuteExpression(ast, call.Arguments[2], ctx);
                var arg3 = ExecuteExpression(ast, call.Arguments[3], ctx);
                act(ctx, arg0, arg1, arg2, arg3);
                return Value.Null;
            });
        }

        public void RegisterFunc(string name, Func<ExecContext, Value, Value> act)
        {
            _buildinFunctions.Add(name, (ctx, ast, call) =>
            {
                if (call.Arguments.Count != 1)
                    throw new Exception($"({call.Line}): Wrong number of arguments (expected 1, was {call.Arguments.Count})");

                var arg0 = ExecuteExpression(ast, call.Arguments[0], ctx);
                return act(ctx, arg0);
            });
        }

        public void RegisterFunc(string name, Func<ExecContext, Value, Value, Value> act)
        {
            _buildinFunctions.Add(name, (ctx, ast, call) =>
            {
                if (call.Arguments.Count != 2)
                    throw new Exception($"({call.Line}): Wrong number of arguments (expected 2, was {call.Arguments.Count})");

                var arg0 = ExecuteExpression(ast, call.Arguments[0], ctx);
                var arg1 = ExecuteExpression(ast, call.Arguments[1], ctx);
                return act(ctx, arg0, arg1);
            });
        }

        public void RegisterFunc(string name, Func<ExecContext, Value, Value, Value, Value> act)
        {
            _buildinFunctions.Add(name, (ctx, ast, call) =>
            {
                if (call.Arguments.Count != 3)
                    throw new Exception($"({call.Line}): Wrong number of arguments (expected 3, was {call.Arguments.Count})");

                var arg0 = ExecuteExpression(ast, call.Arguments[0], ctx);
                var arg1 = ExecuteExpression(ast, call.Arguments[1], ctx);
                var arg2 = ExecuteExpression(ast, call.Arguments[2], ctx);
                return act(ctx, arg0, arg1, arg2);
            });
        }

        public unsafe Value ExecuteFunc(Ast ast, string name, ExecContext ctx)
        {
            if (ctx.StringPool == null)
            {
                ctx.StringPool = new();
            }

            var mem = Marshal.AllocHGlobal(1024);
            ctx.Stack = mem.ToPointer();

            var func = ast.Functions.First(f => f.Name == name);

            for (int i = 0; i < func.Statements.Count; ++i)
            {
                var st = func.Statements[i];
                var (result, done) = ExecuteStatement(ast, st, ctx);
                if (done)
                    return result;
            }

            //  Marshal.FreeHGlobal(ctx.HeapMemory);

            return Value.Null;
        }

        (Value, bool) ExecuteStatement(Ast ast, Statement stmt, ExecContext ctx)
        {
            if (stmt == null)
                throw new ArgumentNullException("stmt");

            // Debug.Log("Execute " + stmt.ToString(""));

            switch (stmt)
            {
                case Call call:
                    return (ExecuteCall(ast, call, ctx), false);
                case Assignment a:
                    {
                        var val = ExecuteExpression(ast, a.Value, ctx);
                        _vars[a.VariableName] = val;
                        return (Value.Null, false);
                    }
                case If ifExpr:
                    {
                        var cnd = ExecuteExpression(ast, ifExpr.Condition, ctx);
                        if (cnd.AsBool) // #todo assert is bool
                        {
                            for (int i = 0; i < ifExpr.TrueStatements.Count; ++i)
                            {
                                var stmt2 = ifExpr.TrueStatements[i];
                                var (result, done) = ExecuteStatement(ast, stmt2, ctx);
                                if (done)
                                    return (result, true);
                            }
                        }
                        return (Value.Null, false);
                    }
                case Return ret:
                    return (ExecuteExpression(ast, ret.Value, ctx), true);
                default:
                    throw new Exception("Todo " + stmt.ToString(""));
            }
        }

        Value ExecuteExpression(Ast ast, Expression expr, ExecContext ctx)
        {
            if (expr == null)
                throw new ArgumentNullException("expr");

            //  Debug.Log("Execute " + expr.ToString(""));

            switch (expr)
            {
                case Call call:
                    return ExecuteCall(ast, call, ctx);
                case AddExpr add:
                    {
                        var left = ExecuteExpression(ast, add.Left, ctx);
                        var right = ExecuteExpression(ast, add.Right, ctx);
                        return (left.Type, right.Type) switch
                        {
                            (ValueType.Int, ValueType.Int) => Value.FromInt(left.AsInt + right.AsInt),
                            (ValueType.Float, ValueType.Float) => Value.FromFloat(left.AsFloat + right.AsFloat),
                            (ValueType.Float, ValueType.Double) => Value.FromDouble(left.AsFloat + right.AsDouble),
                            (ValueType.StringIdx, ValueType.StringIdx) => Value.FromStringIdx(ctx.StringPool.Store(ctx.StringPool.Get(left.AsInt) + ctx.StringPool.Get(right.AsInt))),
                            _ => throw new Exception("Unexpected types for add " + (left.Type, right.Type)),
                        };
                    }
                case MulExpr mul:
                    {
                        var left = ExecuteExpression(ast, mul.Left, ctx);
                        var right = ExecuteExpression(ast, mul.Right, ctx);
                        return (left.Type, right.Type) switch
                        {
                            (ValueType.Int, ValueType.Int) => Value.FromInt(left.AsInt * right.AsInt),
                            (ValueType.Double, ValueType.Int) => Value.FromDouble(left.AsDouble * right.AsInt),
                            _ => throw new Exception("Unexpected types for mul " + (left.Type, right.Type)),
                        };
                    }
                case GreaterExpr greater:
                    {
                        var left = ExecuteExpression(ast, greater.Left, ctx);
                        var right = ExecuteExpression(ast, greater.Right, ctx);
                        return (left.Type, right.Type) switch
                        {
                            (ValueType.Int, ValueType.Int) => Value.FromBool(left.AsInt > right.AsInt),
                            _ => throw new Exception("Unexpected types for greater " + (left.Type, right.Type)),
                        };
                    }
                case LEqualExpr leq:
                    {
                        var left = ExecuteExpression(ast, leq.Left, ctx);
                        var right = ExecuteExpression(ast, leq.Right, ctx);
                        return (left.Type, right.Type) switch
                        {
                            (ValueType.Int, ValueType.Int) => Value.FromBool(left.AsInt <= right.AsInt),
                            (ValueType.Float, ValueType.Int) => Value.FromBool(left.AsFloat <= right.AsInt),
                            _ => throw new Exception("Unexpected types for leq " + (left.Type, right.Type)),
                        };
                    }
                case AndExpr and:
                    {
                        var left = ExecuteExpression(ast, and.Left, ctx);
                        var right = ExecuteExpression(ast, and.Right, ctx);
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

        Value ExecuteCall(Ast ast, Call call, ExecContext ctx)
        {
            if (_buildinFunctions.ContainsKey(call.Name))
            {
                var f = _buildinFunctions[call.Name];
                return f(ctx, ast, call);
            }
            else
            {
                var f = ast.Functions.First(f => f.Name == call.Name);

                // Arguments
                if (call.Arguments.Count != f.Parameters.Count)
                    throw new Exception($"{call.Line}: Wrong number of arguments for call (expected {f.Parameters.Count}, got {call.Arguments.Count})");
                for (int i = 0; i < call.Arguments.Count; ++i)
                {
                    _vars[f.Parameters[i]] = ExecuteExpression(ast, call.Arguments[i], ctx);
                }

                // Call
                return ExecuteFunc(ast, call.Name, ctx);
            }
        }
    }
}