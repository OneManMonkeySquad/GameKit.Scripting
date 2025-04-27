using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using GameKit.Scripting.Runtime;
using GrEmit;
using UnityEditor;
using UnityEngine;

namespace GameKit.Scripting.Internal
{
    public class CompiledScript
    {
        Dictionary<string, MethodInfo> Functions;

        public CompiledScript(Dictionary<string, MethodInfo> functions)
        {
            Functions = functions;
        }

        public void Execute(string name)
        {
            Functions[name].Invoke(null, null);
        }

        public void Execute(string name, Value arg0)
        {
            Functions[name].Invoke(null, new object[] { arg0 });
        }
    }

    public static class Buildin
    {
        static List<string> strings = new();

        [Scriptable("print")]
        public static void Print(Value val)
        {
            var str = val.Type switch
            {
                ValueTypeIdx.Null => "null",
                ValueTypeIdx.Bool => val.AsBool ? "true" : "false",
                ValueTypeIdx.Int => val.AsInt.ToString(),
                ValueTypeIdx.Float => val.AsFloat.ToString(),
                ValueTypeIdx.Double => val.AsDouble.ToString(),
                ValueTypeIdx.Entity => val.AsEntity.ToString(),
                ValueTypeIdx.StringIdx => strings[val.AsInt],
                _ => throw new Exception("Todo ToString"),
            };

            Debug.Log(str);
            if (ILCompiler.Output != null)
            {
                ILCompiler.Output += str;
            }
        }

        public static Value CreateString(string str)
        {
            var idx = strings.Count;
            strings.Add(str);
            return Value.FromStringIdx(idx);
        }

        public static bool ConvertValueToBool(Value val)
        {
            return (bool)val;
        }

        public static Value Negate(Value value)
        {
            return value.Type switch
            {
                ValueTypeIdx.Null => Value.Null,
                ValueTypeIdx.Int => Value.FromInt(-value.AsInt),
                ValueTypeIdx.Float => Value.FromFloat(-value.AsFloat),
                ValueTypeIdx.Double => Value.FromDouble(-value.AsDouble),
                _ => throw new Exception("Unexpected types for Negate " + value.Type),
            };
        }

        public static Value Add(Value left, Value right)
        {
            return (left.Type, right.Type) switch
            {
                (ValueTypeIdx.Int, ValueTypeIdx.Int) => Value.FromInt(left.AsInt + right.AsInt),
                (ValueTypeIdx.Float, ValueTypeIdx.Float) => Value.FromFloat(left.AsFloat + right.AsFloat),
                (ValueTypeIdx.Float, ValueTypeIdx.Double) => Value.FromDouble(left.AsFloat + right.AsDouble),
                (ValueTypeIdx.StringIdx, ValueTypeIdx.StringIdx) => CreateString(strings[left.AsInt] + strings[right.AsInt]),
                (ValueTypeIdx.StringIdx, ValueTypeIdx.Entity) => CreateString(strings[left.AsInt] + right.AsEntity),
                _ => throw new Exception("Unexpected types for add " + (left.Type, right.Type)),
            };
        }

        public static Value Mul(Value left, Value right)
        {
            return (left.Type, right.Type) switch
            {
                (ValueTypeIdx.Int, ValueTypeIdx.Int) => Value.FromInt(left.AsInt * right.AsInt),
                (ValueTypeIdx.Double, ValueTypeIdx.Int) => Value.FromDouble(left.AsDouble * right.AsInt),
                _ => throw new Exception("Unexpected types for Mul " + (left.Type, right.Type)),
            };
        }

        public static Value CmpEq(Value left, Value right)
        {
            return (left.Type, right.Type) switch
            {
                (ValueTypeIdx.Int, ValueTypeIdx.Int) => Value.FromBool(left.AsInt == right.AsInt),
                _ => throw new Exception("Unexpected types for CmpEq " + (left.Type, right.Type)),
            };
        }

        public static Value Greater(Value left, Value right)
        {
            return (left.Type, right.Type) switch
            {
                (ValueTypeIdx.Int, ValueTypeIdx.Int) => Value.FromBool(left.AsInt > right.AsInt),
                _ => throw new Exception("Unexpected types for Greater " + (left.Type, right.Type)),
            };
        }

        public static Value LEqual(Value left, Value right)
        {
            return (left.Type, right.Type) switch
            {
                (ValueTypeIdx.Int, ValueTypeIdx.Int) => Value.FromBool(left.AsInt <= right.AsInt),
                _ => throw new Exception("Unexpected types for LEqual " + (left.Type, right.Type)),
            };
        }

        public static Value And(Value left, Value right)
        {
            return (left.Type, right.Type) switch
            {
                (ValueTypeIdx.Bool, ValueTypeIdx.Bool) => Value.FromBool(left.AsBool && right.AsBool),
                _ => throw new Exception("Unexpected types for and " + (left.Type, right.Type)),
            };
        }
    }

    public class ILCompiler
    {
        public static string Output;

        void RegisterMethods(Dictionary<string, MethodInfo> methods)
        {
#if UNITY_EDITOR
            var taggedMethods = TypeCache.GetMethodsWithAttribute<ScriptableAttribute>();
            foreach (var taggedMethod in taggedMethods)
            {
                var name = taggedMethod.GetCustomAttribute<ScriptableAttribute>().Name;
                if (methods.ContainsKey(name))
                {
                    Debug.LogError($"Multiple Scriptable methods with the same name '{name}'. This is not supported.");
                    continue;
                }

                methods[name] = taggedMethod;
            }
#else
            // #todo
#endif
        }

        public CompiledScript Compile(Ast ast)
        {
            File.WriteAllText("E:\\il.txt", "");

            var methods = new Dictionary<string, MethodInfo>();
            foreach (var func in ast.Functions)
            {
                var parameters = new Type[func.Parameters.Count];
                for (int i = 0; i < func.Parameters.Count; ++i)
                {
                    parameters[i] = typeof(Value);
                }

                Type resultType = null;
                if (func.HasReturnValue)
                {
                    resultType = typeof(Value);
                }

                var method = new DynamicMethod(func.Name, resultType, parameters, typeof(ILCompiler));

                methods[func.Name] = method;
            }

            RegisterMethods(methods);

            foreach (var func in ast.Functions)
            {
                var method = methods[func.Name];
                EmitIL(func, (DynamicMethod)method, methods);
            }

            var ca = new CompiledScript(methods);
            return ca;
        }

        DynamicMethod EmitIL(FunctionDecl func, DynamicMethod method, Dictionary<string, MethodInfo> methods)
        {
            File.AppendAllText("E:\\il.txt", $"=== {func} ===\n");

            var localVars = new Dictionary<string, GroboIL.Local>();
            using (var il = new GroboIL(method))
            {
                foreach (var stmt in func.Statements)
                {
                    VisitStatement(stmt, il, methods, localVars);
                    il.Nop();
                }
                il.Ret();

                File.AppendAllText("E:\\il.txt", il.GetILCode());
            }

            return method;
        }

        void VisitStatement(Statement stmt, GroboIL il, Dictionary<string, MethodInfo> methods, Dictionary<string, GroboIL.Local> localVars)
        {
            switch (stmt)
            {
                case Call call:
                    foreach (var arg in call.Arguments)
                    {
                        VisitExpression(arg, il, methods, localVars);
                    }
                    il.Call(methods[call.Name]);
                    break;

                case Assignment assignment:
                    VisitExpression(assignment.Value, il, methods, localVars);
                    if (!localVars.TryGetValue(assignment.VariableName, out var local))
                    {
                        local = il.DeclareLocal(typeof(Value));
                        localVars.Add(assignment.VariableName, local);
                    }
                    il.Stloc(local);
                    break;

                case If ifStmt:
                    VisitExpression(ifStmt.Condition, il, methods, localVars);
                    il.Call(typeof(Buildin).GetMethod("ConvertValueToBool"));
                    if (ifStmt.FalseStatements != null)
                    {
                        var conditionWasTrue = il.DefineLabel("if_end");
                        var conditionWasFalse = il.DefineLabel("if_false");

                        il.Brfalse(conditionWasFalse);
                        foreach (var stmt2 in ifStmt.TrueStatements)
                        {
                            VisitStatement(stmt2, il, methods, localVars);
                            il.Nop();
                        }
                        il.Br(conditionWasTrue);

                        il.MarkLabel(conditionWasFalse);
                        foreach (var stmt2 in ifStmt.FalseStatements)
                        {
                            VisitStatement(stmt2, il, methods, localVars);
                            il.Nop();
                        }

                        il.MarkLabel(conditionWasTrue);
                    }
                    else
                    {
                        var conditionWasFalse = il.DefineLabel("if_false");

                        il.Brfalse(conditionWasFalse);
                        foreach (var stmt2 in ifStmt.TrueStatements)
                        {
                            VisitStatement(stmt2, il, methods, localVars);
                            il.Nop();
                        }

                        il.MarkLabel(conditionWasFalse);
                    }
                    break;

                case Return ret:
                    VisitExpression(ret.Value, il, methods, localVars);
                    break;

                default:
                    throw new Exception("Missing case");
            }
        }

        void VisitExpression(Expression expr, GroboIL il, Dictionary<string, MethodInfo> methods, Dictionary<string, GroboIL.Local> localVars)
        {
            switch (expr)
            {
                case ValueExpr var:
                    switch (var.Value.Type)
                    {
                        case ValueTypeIdx.Bool:
                            il.Ldc_I4(var.Value.AsBool ? 1 : 0);
                            il.Call(typeof(Value).GetMethod("FromBool"));
                            break;

                        case ValueTypeIdx.Int:
                            il.Ldc_I4(var.Value.AsInt);
                            il.Call(typeof(Value).GetMethod("FromInt"));
                            break;
                    }
                    break;

                case VariableExpr var:
                    switch (var.ScopeInfo.Source)
                    {
                        case VariableSource.None:
                            // ???
                            break;
                        case VariableSource.Local:
                            var local = localVars[var.Name];
                            il.Ldloc(local);
                            break;
                        case VariableSource.Argument:
                            il.Ldarg(var.ScopeInfo.ArgumentIdx);
                            break;
                    }
                    break;

                case StringExpr var:
                    il.Ldstr(var.Content);
                    il.Call(typeof(Buildin).GetMethod("CreateString"));
                    break;

                case AddExpr var:
                    VisitExpression(var.Left, il, methods, localVars);
                    VisitExpression(var.Right, il, methods, localVars);
                    il.Call(typeof(Buildin).GetMethod("Add"));
                    break;

                case MulExpr var:
                    VisitExpression(var.Left, il, methods, localVars);
                    VisitExpression(var.Right, il, methods, localVars);
                    il.Call(typeof(Buildin).GetMethod("Mul"));
                    break;

                case CmpExpr cmp:
                    VisitExpression(cmp.Left, il, methods, localVars);
                    VisitExpression(cmp.Right, il, methods, localVars);
                    switch (cmp.Type)
                    {
                        case CmpType.And:
                            il.Call(typeof(Buildin).GetMethod("And"));
                            break;
                        case CmpType.Equal:
                            il.Call(typeof(Buildin).GetMethod("CmpEq"));
                            break;
                        case CmpType.Greater:
                            il.Call(typeof(Buildin).GetMethod("Greater"));
                            break;
                        case CmpType.LessOrEqual:
                            il.Call(typeof(Buildin).GetMethod("LEqual"));
                            break;
                    }
                    break;

                case NegateExpr var:
                    VisitExpression(var.Value, il, methods, localVars);
                    il.Call(typeof(Buildin).GetMethod("Negate"));
                    break;

                case Call call:
                    foreach (var arg in call.Arguments)
                    {
                        VisitExpression(arg, il, methods, localVars);
                    }
                    if (!methods.ContainsKey(call.Name))
                        throw new Exception($"({call.Line}): Function not found '{call.Name}'");

                    il.Call(methods[call.Name]);
                    break;

                default:
                    throw new Exception("Missing case");
            }
        }
    }
}