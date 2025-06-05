using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using GameKit.Scripting.Runtime;
using GrEmit;
using NUnit.Framework;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace GameKit.Scripting.Internal
{
    public class ILCompiler
    {
        public static string Output;

        class Globals
        {
            public Dictionary<string, MethodInfo> Methods;
            public Dictionary<string, FieldInfo> Properties;
        }

        static int numRecompiles = 0;

        public CompiledScript Compile(Ast ast)
        {
            File.WriteAllText("E:\\il.txt", "");

            ++numRecompiles;

            var asmName = new AssemblyName("MyDynamicAssembly");
            var asmBuilder = AssemblyBuilder.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.RunAndCollect);
            var modBuilder = asmBuilder.DefineDynamicModule("MyModule");
            var typeBuilder = modBuilder.DefineType("MyDynamicType" + numRecompiles, TypeAttributes.Public | TypeAttributes.Class);

            // Create Fields for every property
            var properties = new Dictionary<string, FieldInfo>();
            foreach (var prop in ast.Properties)
            {
                var type = ScriptingTypeCache.ByName(prop.DeclaredTypeName);

                var staticField = typeBuilder.DefineField(
                      prop.Name,
                      type,
                      FieldAttributes.Public | FieldAttributes.Static
                );
                properties.Add(prop.Name, staticField);
            }

            var methods = new Dictionary<string, MethodInfo>();
            RegisterScriptableFunctions(methods);

            foreach (var func in ast.Functions)
            {
                var parameters = new Type[func.ParameterNames.Count];
                for (int i = 0; i < func.ParameterNames.Count; ++i)
                {
                    parameters[i] = typeof(object);
                }

                Type resultType = null;
                if (func.HasReturnValue)
                {
                    resultType = typeof(object);
                }

                var mb = typeBuilder.DefineMethod(func.Name, MethodAttributes.Public | MethodAttributes.Static, resultType, parameters);
                methods[func.Name] = mb;
            }

            var globals = new Globals
            {
                Methods = methods,
                Properties = properties,
            };
            foreach (var func in ast.Functions)
            {
                var method = methods[func.Name];
                EmitFunctionIL(func, (MethodBuilder)method, globals);
            }

            var myType = typeBuilder.CreateType();

            var methods2 = new Dictionary<string, Delegate>(ast.Functions.Count);
            var properties2 = new Dictionary<string, FieldInfo>(ast.Properties.Count);
            foreach (var func in ast.Functions)
            {
                var method = myType.GetMethod(func.Name);

                Delegate d = null;


                if (method.GetParameters().Length == 0)
                {
                    if (func.HasReturnValue)
                    {
                        d = method.CreateDelegate(typeof(Func<object>), null);
                    }
                    else
                    {
                        d = method.CreateDelegate(typeof(Action), null);
                    }
                }
                else if (method.GetParameters().Length == 1)
                {
                    if (func.HasReturnValue)
                    {
                        d = method.CreateDelegate(typeof(Func<object, object>), null);
                    }
                    else
                    {
                        d = method.CreateDelegate(typeof(Action<object>), null);
                    }
                }
                else if (method.GetParameters().Length == 2)
                {
                    if (func.HasReturnValue)
                    {
                        d = method.CreateDelegate(typeof(Func<object, object, object>), null);
                    }
                    else
                    {
                        d = method.CreateDelegate(typeof(Action<object, object>), null);
                    }
                }

                if (d == null)
                    throw new Exception("todo");

                methods2.Add(func.Name, d);
            }
            foreach (var prop in ast.Properties)
            {
                properties2.Add(prop.Name, myType.GetField(prop.Name));
            }

            var ca = new CompiledScript(methods2, properties2);
            return ca;
        }

        void RegisterScriptableFunctions(Dictionary<string, MethodInfo> methods)
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

        void EmitFunctionIL(FunctionDecl func, MethodBuilder method, Globals globals)
        {
            File.AppendAllText("E:\\il.txt", $"=== {func} ===\n");

            var localVars = new Dictionary<string, GroboIL.Local>();
            using (var il = new GroboIL(method))
            {
                foreach (var stmt in func.Statements)
                {
                    VisitStatement(stmt, il, globals, localVars);
                }

                if (!func.HasReturnValue)
                {
                    il.Ret();
                }

                File.AppendAllText("E:\\il.txt", il.GetILCode() + "\n");
            }
        }

        void VisitStatement(Statement stmt, GroboIL il, Globals globals, Dictionary<string, GroboIL.Local> localVars)
        {
            Assert.IsNotNull(stmt);

            switch (stmt)
            {
                case PropertyDecl:
                    break;

                case Call call:
                    VisitCall(call, il, globals, localVars);
                    break;

                case Assignment assignment:
                    VisitExpression(assignment.Value, il, globals, localVars);
                    switch (assignment.ScopeInfo.Source)
                    {
                        case VariableSource.Local:
                            if (!localVars.TryGetValue(assignment.VariableName, out var local))
                            {
                                local = il.DeclareLocal(typeof(object));
                                localVars.Add(assignment.VariableName, local);
                            }
                            il.Stloc(local);
                            break;
                        case VariableSource.Property:
                            {
                                var prop = globals.Properties[assignment.VariableName];
                                il.Unbox_Any(prop.FieldType);
                                il.Stfld(prop);
                                break;
                            }
                        default:
                            throw new Exception("missing case (assignment)");
                    }
                    break;

                case LocalVariableDecl variableDecl:
                    VisitExpression(variableDecl.Value, il, globals, localVars);
                    var local2 = il.DeclareLocal(typeof(object));
                    localVars.Add(variableDecl.VariableName, local2);
                    il.Stloc(local2);
                    break;

                case If ifStmt:
                    VisitExpression(ifStmt.Condition, il, globals, localVars);
                    il.Call(typeof(Buildin).GetMethod("ConvertValueToBool"));
                    if (ifStmt.FalseStatements != null)
                    {
                        var conditionWasTrue = il.DefineLabel("if_end");
                        var conditionWasFalse = il.DefineLabel("if_false");

                        il.Brfalse(conditionWasFalse);
                        foreach (var stmt2 in ifStmt.TrueStatements)
                        {
                            VisitStatement(stmt2, il, globals, localVars);
                            il.Nop();
                        }
                        il.Br(conditionWasTrue);

                        il.MarkLabel(conditionWasFalse);
                        foreach (var stmt2 in ifStmt.FalseStatements)
                        {
                            VisitStatement(stmt2, il, globals, localVars);
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
                            VisitStatement(stmt2, il, globals, localVars);
                            il.Nop();
                        }

                        il.MarkLabel(conditionWasFalse);
                    }
                    break;

                case Return ret:
                    if (ret.Value != null)
                    {
                        VisitExpression(ret.Value, il, globals, localVars);
                    }
                    il.Ret();
                    break;

                default:
                    throw new Exception("missing case");
            }
        }

        void VisitExpression(Expression expr, GroboIL il,
            Globals globals,
            Dictionary<string, GroboIL.Local> localVars)
        {
            Assert.IsNotNull(expr);

            switch (expr)
            {
                case ValueExpr var:
                    switch (var.ValueType)
                    {
                        case ValueType.Null:
                            il.Ldnull();
                            break;

                        case ValueType.Bool:
                            il.Ldc_I4((bool)var.Value ? 1 : 0);
                            il.Box(typeof(bool));
                            break;

                        case ValueType.Int:
                            il.Ldc_I4((int)var.Value);
                            il.Box(typeof(int));
                            break;

                        case ValueType.Float:
                            il.Ldc_R4((float)var.Value);
                            il.Box(typeof(float));
                            break;

                        case ValueType.Double:
                            il.Ldc_R8((double)var.Value);
                            il.Box(typeof(double));
                            break;

                        case ValueType.String:
                            il.Ldstr((string)var.Value);
                            break;

                        default:
                            throw new Exception("case missing (value)");
                    }

                    break;

                case VariableExpr var:
                    switch (var.ScopeInfo.Source)
                    {
                        case VariableSource.Local:
                            var local = localVars[var.Name];
                            il.Ldloc(local);
                            break;
                        case VariableSource.Argument:
                            il.Ldarg(var.ScopeInfo.ArgumentIdx);
                            break;
                        case VariableSource.Property:
                            {
                                var prop = globals.Properties[var.Name];
                                il.Ldfld(prop);
                                if (prop.FieldType.IsValueType)
                                {
                                    il.Box(prop.FieldType);
                                }

                                // if (prop.FieldType == typeof(Entity))
                                // {
                                //     il.Call(typeof(Value).GetMethod("FromEntity"));
                                // }
                                // else if (prop.FieldType == typeof(int))
                                // {
                                //     il.Call(typeof(Value).GetMethod("FromInt"));
                                // }
                                // else
                                // {
                                //     throw new Exception("case missing");
                                // }
                                break;
                            }
                        default:
                            throw new Exception("case missing (variable source)");
                    }
                    break;

                case AddExpr var:
                    VisitExpression(var.Left, il, globals, localVars);
                    VisitExpression(var.Right, il, globals, localVars);
                    il.Call(typeof(Buildin).GetMethod("Add"));
                    break;

                case MulExpr var:
                    VisitExpression(var.Left, il, globals, localVars);
                    VisitExpression(var.Right, il, globals, localVars);
                    il.Call(typeof(Buildin).GetMethod("Mul"));
                    break;

                case CmpExpr cmp:
                    VisitExpression(cmp.Left, il, globals, localVars);
                    VisitExpression(cmp.Right, il, globals, localVars);
                    switch (cmp.Type)
                    {
                        case CmpType.And:
                            il.Call(typeof(Buildin).GetMethod("And"));
                            break;
                        case CmpType.Equal:
                            il.Call(typeof(Buildin).GetMethod("CmpEq"));
                            break;
                        case CmpType.NotEqual:
                            il.Call(typeof(Buildin).GetMethod("CmpNEq"));
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
                    VisitExpression(var.Value, il, globals, localVars);
                    il.Call(typeof(Buildin).GetMethod("Negate"));
                    break;

                case Call call:
                    VisitCall(call, il, globals, localVars);
                    break;

                case GroupingExpr group:
                    VisitExpression(group.Value, il, globals, localVars);
                    break;

                default:
                    throw new Exception("Missing case");
            }
        }

        void VisitCall(Call call, GroboIL il, Globals globals, Dictionary<string, GroboIL.Local> localVars)
        {
            if (!globals.Methods.TryGetValue(call.Name, out MethodInfo method))
                throw new Exception($"Function '{call.Name}' not found (at {call.SourceLocation})");

            // var parameters = method.GetParameters();

            for (int i = 0; i < call.Arguments.Count; i++)
            {
                var arg = call.Arguments[i];
                VisitExpression(arg, il, globals, localVars);

                // var param = parameters[i];
                // if (param.GetType() != typeof(object))
                // {
                //     // #todo
                // }
            }

            il.Call(method);
        }
    }
}