using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using GrEmit;
using UnityEngine;

namespace GameKit.Scripting.Runtime
{
    public class CompiledAst
    {
        public Dictionary<string, MethodInfo> Functions;
    }

    public static class Buildin
    {
        static List<string> strings = new();

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

        public static void Print(Value val)
        {
            var str = val.Type switch
            {
                ValueType.Null => "null",
                ValueType.Bool => val.AsBool ? "true" : "false",
                ValueType.Int => val.AsInt.ToString(),
                ValueType.Float => val.AsFloat.ToString(),
                ValueType.Double => val.AsDouble.ToString(),
                ValueType.Entity => val.AsEntity.ToString(),
                ValueType.StringIdx => strings[val.AsInt],
                _ => throw new Exception("Todo ToString"),
            };

            Debug.Log(str);
            if (ILCompiler.Output != null)
            {
                ILCompiler.Output += str;
            }
        }

        public static Value Add(Value left, Value right)
        {
            return (left.Type, right.Type) switch
            {
                (ValueType.Int, ValueType.Int) => Value.FromInt(left.AsInt + right.AsInt),
                (ValueType.Float, ValueType.Float) => Value.FromFloat(left.AsFloat + right.AsFloat),
                (ValueType.Float, ValueType.Double) => Value.FromDouble(left.AsFloat + right.AsDouble),
                (ValueType.StringIdx, ValueType.StringIdx) => CreateString(strings[left.AsInt] + strings[right.AsInt]),
                _ => throw new Exception("Unexpected types for add " + (left.Type, right.Type)),
            };
        }

        public static Value Mul(Value left, Value right)
        {
            return (left.Type, right.Type) switch
            {
                (ValueType.Int, ValueType.Int) => Value.FromInt(left.AsInt * right.AsInt),
                (ValueType.Double, ValueType.Int) => Value.FromDouble(left.AsDouble * right.AsInt),
                _ => throw new Exception("Unexpected types for mul " + (left.Type, right.Type)),
            };
        }

        public static Value Greater(Value left, Value right)
        {
            return (left.Type, right.Type) switch
            {
                (ValueType.Int, ValueType.Int) => Value.FromBool(left.AsInt > right.AsInt),
                _ => throw new Exception("Unexpected types for greater " + (left.Type, right.Type)),
            };
        }

        public static Value And(Value left, Value right)
        {
            return (left.Type, right.Type) switch
            {
                (ValueType.Bool, ValueType.Bool) => Value.FromBool(left.AsBool && right.AsBool),
                _ => throw new Exception("Unexpected types for and " + (left.Type, right.Type)),
            };
        }
    }

    public class ILCompiler
    {
        public static string Output;

        public static int Test()
        {
            return 42;
        }

        public CompiledAst Compile(Ast ast)
        {
            File.WriteAllText("E:\\il.txt", "");

            var ca = new CompiledAst();

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

            methods["print"] = typeof(Buildin).GetMethod("Print");

            foreach (var func in ast.Functions)
            {
                var method = methods[func.Name];
                EmitIL(func, (DynamicMethod)method, methods);
            }

            ca.Functions = methods;

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
                    var end = il.DefineLabel("if_false");
                    il.Call(typeof(Buildin).GetMethod("ConvertValueToBool"));
                    il.Brfalse(end);
                    foreach (var stmt2 in ifStmt.TrueStatements)
                    {
                        VisitStatement(stmt2, il, methods, localVars);
                        il.Nop();
                    }
                    il.MarkLabel(end);
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
                        case ValueType.Bool:
                            il.Ldc_I4(var.Value.AsBool ? 1 : 0);
                            il.Call(typeof(Value).GetMethod("FromBool"));
                            break;

                        case ValueType.Int:
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

                case GreaterExpr var:
                    VisitExpression(var.Left, il, methods, localVars);
                    VisitExpression(var.Right, il, methods, localVars);
                    il.Call(typeof(Buildin).GetMethod("Greater"));
                    break;

                case AndExpr var:
                    VisitExpression(var.Left, il, methods, localVars);
                    VisitExpression(var.Right, il, methods, localVars);
                    il.Call(typeof(Buildin).GetMethod("And"));
                    break;

                case Call call:
                    foreach (var arg in call.Arguments)
                    {
                        VisitExpression(arg, il, methods, localVars);
                    }
                    il.Call(methods[call.Name]);
                    break;

                default:
                    throw new Exception("Missing case");
            }
        }
    }
}