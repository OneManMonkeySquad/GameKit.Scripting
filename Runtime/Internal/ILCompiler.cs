using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using GameKit.Scripting.Runtime;
using GrEmit;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GameKit.Scripting.Internal
{
    public class CompiledScript
    {
        Dictionary<string, MethodInfo> _functions;
        Dictionary<string, FieldInfo> _properties;

        public CompiledScript(Dictionary<string, MethodInfo> functions, Dictionary<string, FieldInfo> properties)
        {
            _functions = functions;
            _properties = properties;
        }

        public void SetProperty(string name, Value value)
        {
            _properties[name].SetValue(null, value);
        }

        public void CopyPropertiesTo(CompiledScript other)
        {
            foreach (var entry in _properties)
            {
                other.SetProperty(entry.Key, (Value)entry.Value.GetValue(null));
            }
        }

        public void Execute(string name)
        {
            Delegate d = _functions[name].CreateDelegate(typeof(Action), null);
            d.DynamicInvoke();
        }

        public void Execute(string name, Value arg0)
        {
            Delegate d = _functions[name].CreateDelegate(typeof(Action<Value>), null);
            d.DynamicInvoke(arg0);
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

        public static string GetString(Value str)
        {
            if (str.Type != ValueTypeIdx.StringIdx)
                throw new Exception("Unexpected types for GetString " + str.Type);

            return strings[str.AsInt];
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
                (ValueTypeIdx.Double, ValueTypeIdx.Float) => Value.FromDouble(left.AsDouble + right.AsFloat),
                (ValueTypeIdx.StringIdx, ValueTypeIdx.StringIdx) => CreateString(strings[left.AsInt] + strings[right.AsInt]),
                (ValueTypeIdx.StringIdx, ValueTypeIdx.Entity) => CreateString(strings[left.AsInt] + right.AsEntity),
                _ => throw new Exception("Unexpected types for Add " + (left.Type, right.Type)),
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
            var typeBuilder = modBuilder.DefineType(
                "MyDynamicType" + numRecompiles,                   // Type name
                TypeAttributes.Public | TypeAttributes.Class // Modifiers (public class)
            );

            var properties = new Dictionary<string, FieldInfo>();
            foreach (var prop in ast.Properties)
            {
                var staticField = typeBuilder.DefineField(
                      prop.Name,
                      typeof(Value),
                      FieldAttributes.Public | FieldAttributes.Static
                );
                properties.Add(prop.Name, staticField);
            }

            var methods = new Dictionary<string, MethodInfo>();
            RegisterScriptableFunctions(methods);

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

            var methods2 = new Dictionary<string, MethodInfo>();
            var properties2 = new Dictionary<string, FieldInfo>();
            foreach (var func in ast.Functions)
            {
                methods2.Add(func.Name, myType.GetMethod(func.Name));
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
            switch (stmt)
            {
                case PropertyDecl prop:
                    // #todo
                    break;

                case Call call:
                    foreach (var arg in call.Arguments)
                    {
                        VisitExpression(arg, il, globals, localVars);
                    }
                    il.Call(globals.Methods[call.Name]);
                    break;

                case Assignment assignment:
                    VisitExpression(assignment.Value, il, globals, localVars);

                    switch (assignment.ScopeInfo.Source)
                    {
                        case VariableSource.Local:
                            if (!localVars.TryGetValue(assignment.VariableName, out var local))
                            {
                                local = il.DeclareLocal(typeof(Value));
                                localVars.Add(assignment.VariableName, local);
                            }
                            il.Stloc(local);
                            break;
                        case VariableSource.Property:
                            il.Stfld(globals.Properties[assignment.VariableName]);
                            break;
                        default:
                            throw new Exception("missing case");
                    }


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

                        case ValueTypeIdx.Float:
                            il.Ldc_R4(var.Value.AsFloat);
                            il.Call(typeof(Value).GetMethod("FromFloat"));
                            break;

                        case ValueTypeIdx.Double:
                            il.Ldc_R8(var.Value.AsDouble);
                            il.Call(typeof(Value).GetMethod("FromDouble"));
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
                            il.Ldfld(globals.Properties[var.Name]);
                            break;
                        default:
                            throw new Exception("case missing (variable source)");
                    }
                    break;

                case StringExpr var:
                    il.Ldstr(var.Content);
                    il.Call(typeof(Buildin).GetMethod("CreateString"));
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
                    foreach (var arg in call.Arguments)
                    {
                        VisitExpression(arg, il, globals, localVars);
                    }
                    if (!globals.Methods.ContainsKey(call.Name))
                        throw new Exception($"({call.SourceLocation}): Function not found '{call.Name}'");

                    il.Call(globals.Methods[call.Name]);
                    break;

                default:
                    throw new Exception("Missing case");
            }
        }
    }
}