using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using GameKit.Scripting.Runtime;
using GrEmit;
using Unity.Assertions;
using UnityEngine;

namespace GameKit.Scripting.Internal
{
    public class ILCompiler
    {
        class Globals
        {
            public Dictionary<string, MethodInfo> Methods;
        }

        static int numRecompiles = 0;

        public CompiledScript Compile(Ast ast, Dictionary<string, MethodInfo> scriptableFunctions)
        {
            File.WriteAllText("E:\\il.txt", "");

            ++numRecompiles;

            var asmName = new AssemblyName("MyDynamicAssembly");
            var asmBuilder = AssemblyBuilder.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.RunAndCollect);
            var modBuilder = asmBuilder.DefineDynamicModule("MyModule");
            var typeBuilder = modBuilder.DefineType("MyDynamicType" + numRecompiles, TypeAttributes.Public | TypeAttributes.Class);

            var methods = scriptableFunctions;

            foreach (var func in ast.Functions)
            {
                var parameters = new Type[func.ParameterNames.Count];
                for (int i = 0; i < func.ParameterNames.Count; ++i)
                {
                    parameters[i] = typeof(object);
                }

                Type resultType = typeof(object);
                var mb = typeBuilder.DefineMethod(func.Name, MethodAttributes.Public | MethodAttributes.Static, resultType, parameters);
                methods[func.Name] = mb;
            }

            var globals = new Globals
            {
                Methods = methods,
            };
            foreach (var func in ast.Functions)
            {
                var method = methods[func.Name];
                EmitFunctionIL(func, (MethodBuilder)method, globals, typeBuilder);
            }

            var myType = typeBuilder.CreateType();

            var methodDelegates = new Dictionary<string, Delegate>(ast.Functions.Count);
            foreach (var func in ast.Functions)
            {
                var method = myType.GetMethod(func.Name);

                Delegate d;
                if (method.GetParameters().Length == 0)
                {
                    d = method.CreateDelegate(typeof(Func<object>), null);
                }
                else if (method.GetParameters().Length == 1)
                {
                    d = method.CreateDelegate(typeof(Func<object, object>), null);
                }
                else if (method.GetParameters().Length == 2)
                {
                    d = method.CreateDelegate(typeof(Func<object, object, object>), null);
                }
                else
                {
                    throw new Exception("todo");
                }

                methodDelegates.Add(func.Name, d);
            }

            var ca = new CompiledScript(methodDelegates);
            return ca;
        }

        static bool _isInCoroutineFunction;
        void EmitFunctionIL(FunctionDecl func, MethodBuilder method, Globals globals, TypeBuilder typeBuilder)
        {
            File.AppendAllText("E:\\il.txt", $"=== {func} ===\n");

            _isInCoroutineFunction = func.IsCoroutine;

            using var il = new GroboIL(method);
            var localVars = new Dictionary<string, GroboIL.Local>();
            if (!func.IsCoroutine)
            {
                EmitBodyIL(func.Body, il, globals, localVars);
                il.Ret();

                File.AppendAllText("E:\\il.txt", il.GetILCode() + "\n\n");
            }
            else
            {
                // Function is coroutine, setup statemachine struct

                var stateMachineBuilder = typeBuilder.DefineNestedType(func.Name + "CoroutineStateMachine", TypeAttributes.NestedPrivate | TypeAttributes.Sealed);
                stateMachineBuilder.AddInterfaceImplementation(typeof(IEnumerator));
                stateMachineBuilder.AddInterfaceImplementation(typeof(IDisposable));

                var stateField = stateMachineBuilder.DefineField("state", typeof(int), FieldAttributes.Public);
                var currentField = stateMachineBuilder.DefineField("current", typeof(object), FieldAttributes.Public);

                // Constructor
                var ctor = stateMachineBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { typeof(int) });
                var ctorIL = new GroboIL(ctor);
                ctorIL.Ldarg(0);
                ctorIL.Call(typeof(object).GetConstructor(Type.EmptyTypes));
                ctorIL.Ldarg(0);
                ctorIL.Ldarg(1);
                ctorIL.Stfld(stateField);
                ctorIL.Ret();

                // get_Current
                var getCurrent = stateMachineBuilder.DefineMethod("get_Current", MethodAttributes.Public | MethodAttributes.Virtual, typeof(object), Type.EmptyTypes);
                var currentIL = new GroboIL(getCurrent);
                currentIL.Ldarg(0);
                currentIL.Ldfld(currentField);
                currentIL.Ret();
                stateMachineBuilder.DefineMethodOverride(getCurrent, typeof(IEnumerator).GetProperty("Current").GetGetMethod());

                // IEnumerator.Current (non-generic)
                var getCurrent2 = stateMachineBuilder.DefineMethod("System.Collections.IEnumerator.get_Current", MethodAttributes.Private | MethodAttributes.Virtual, typeof(object), Type.EmptyTypes);
                var currentIL2 = new GroboIL(getCurrent2);
                currentIL2.Ldarg(0);
                currentIL2.Ldfld(currentField);
                currentIL2.Ret();
                stateMachineBuilder.DefineMethodOverride(getCurrent2, typeof(IEnumerator).GetProperty("Current").GetGetMethod());

                // Reset
                var reset = stateMachineBuilder.DefineMethod("Reset", MethodAttributes.Public | MethodAttributes.Virtual, typeof(void), Type.EmptyTypes);
                var resetIL = new GroboIL(reset);
                resetIL.Newobj(typeof(NotSupportedException).GetConstructor(Type.EmptyTypes));
                resetIL.Throw();
                stateMachineBuilder.DefineMethodOverride(reset, typeof(IEnumerator).GetMethod("Reset"));

                // Dispose
                var dispose = stateMachineBuilder.DefineMethod("Dispose", MethodAttributes.Public | MethodAttributes.Virtual, typeof(void), Type.EmptyTypes);
                var disposeIL = new GroboIL(dispose);
                disposeIL.Ret();
                stateMachineBuilder.DefineMethodOverride(dispose, typeof(IDisposable).GetMethod("Dispose"));

                // MoveNext
                var moveNext = stateMachineBuilder.DefineMethod("MoveNext", MethodAttributes.Public | MethodAttributes.Virtual, typeof(bool), Type.EmptyTypes);
                {
                    var moveNextIl = new GroboIL(moveNext);

                    int numLabels = CountCoroutineCalls(func.Body);
                    ++numLabels; // First label is start

                    var labels = new List<GroboIL.Label>();
                    for (int i = 0; i < numLabels; ++i)
                    {
                        var newLabel = moveNextIl.DefineLabel("Label");
                        labels.Add(newLabel);
                    }

                    var labelEnd = moveNextIl.DefineLabel("End");

                    _labels = labels;
                    _labelIdx = 0;
                    _stateField = stateField;
                    _currentField = currentField;

                    moveNextIl.Ldarg(0);
                    moveNextIl.Ldfld(stateField);
                    moveNextIl.Switch(labels.ToArray());
                    moveNextIl.Br(labelEnd);

                    moveNextIl.MarkLabel(labels[_labelIdx++]); // Start label

                    EmitBodyIL(func.Body, moveNextIl, globals, localVars);

                    moveNextIl.Pop();
                    moveNextIl.Ldc_I4(0);
                    moveNextIl.Ret();

                    moveNextIl.MarkLabel(labelEnd);
                    moveNextIl.Ldc_I4(0);
                    moveNextIl.Ret();

                    _labels = null;
                    _labelIdx = 0;
                    _stateField = null;
                    _currentField = null;

                    File.AppendAllText("E:\\il.txt", "= Coroutine =\n");
                    File.AppendAllText("E:\\il.txt", moveNextIl.GetILCode() + "\n");
                }


                stateMachineBuilder.DefineMethodOverride(moveNext, typeof(IEnumerator).GetMethod("MoveNext"));

                //
                var smType = stateMachineBuilder.CreateType();

                il.Ldc_I4(0);
                il.Newobj(smType.GetConstructor(new[] { typeof(int) }));
                il.Ret();

                File.AppendAllText("E:\\il.txt", "= Method =\n");
                File.AppendAllText("E:\\il.txt", il.GetILCode() + "\n\n");
            }
        }

        static List<GroboIL.Label> _labels;
        static int _labelIdx;
        static FieldBuilder _stateField;
        static FieldBuilder _currentField;
        static int _syncStoreIdx;

        void EmitBodyIL(List<Expression> stmts, GroboIL il, Globals globals, Dictionary<string, GroboIL.Local> localVars)
        {
            for (int i = 0; i < stmts.Count; i++)
            {
                Expression stmt = stmts[i];
                VisitExpression(stmt, il, globals, localVars);
                if (i != stmts.Count - 1)
                {
                    il.Pop();
                }
            }

            if (stmts.Count == 0)
            {
                il.Ldnull(); // Caller expects exactly 1 value on stack
            }
        }

        void VisitExpression(Expression expr, GroboIL il,
            Globals globals,
            Dictionary<string, GroboIL.Local> localVars)
        {
            Assert.IsNotNull(expr);

            switch (expr)
            {
                case Assignment assignment:
                    VisitExpression(assignment.Value, il, globals, localVars);
                    il.Dup(); // Make sure the expression value remains after store
                    switch (assignment.ScopeInfo.Source)
                    {
                        case VariableSource.Local:
                            if (!localVars.TryGetValue(assignment.VariableName, out var local))
                                throw new Exception("this should not happen");

                            il.Stloc(local);
                            break;
                        default:
                            throw new Exception("missing case (assignment)");
                    }
                    break;

                case ValueExpr var:
                    switch (var.ValueType)
                    {
                        case ValueType.Null:
                            il.Ldnull();
                            break;

                        case ValueType.Bool:
                            il.Ldc_I4((bool)var.Value ? 1 : 0);
                            break;

                        case ValueType.Int:
                            il.Ldc_I4((int)var.Value);
                            break;

                        case ValueType.Float:
                            il.Ldc_R4((float)var.Value);
                            break;

                        case ValueType.Double:
                            il.Ldc_R8((double)var.Value);
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
                        default:
                            throw new Exception("case missing (variable source)");
                    }
                    break;

                case MathExpr var:
                    VisitExpression(var.Left, il, globals, localVars);
                    VisitExpression(var.Right, il, globals, localVars);
                    switch (var.Type)
                    {
                        case MathType.Add:
                            il.Call(typeof(Buildin).GetMethod("Add", new Type[] { var.Left.ResultType, var.Right.ResultType }));
                            break;
                        case MathType.Mul:
                            il.Call(typeof(Buildin).GetMethod("Mul", new Type[] { var.Left.ResultType, var.Right.ResultType }));
                            break;
                    }
                    break;

                case CmpExpr cmp:
                    VisitExpression(cmp.Left, il, globals, localVars);
                    VisitExpression(cmp.Right, il, globals, localVars);

                    string name = null;
                    switch (cmp.Type)
                    {
                        case CmpType.And:
                            name = "And";
                            break;
                        case CmpType.Equal:
                            name = "CmpEq";
                            break;
                        case CmpType.NotEqual:
                            name = "CmpNEq";
                            break;
                        case CmpType.Greater:
                            name = "Greater";
                            break;
                        case CmpType.Less:
                            name = "Less";
                            break;
                        case CmpType.LessOrEqual:
                            name = "LEqual";
                            break;
                        case CmpType.GreaterOrEqual:
                            name = "GEqual";
                            break;
                    }

                    MethodInfo mi = typeof(Buildin).GetMethod(name, new Type[] { cmp.Left.ResultType, cmp.Right.ResultType });
                    if (mi == null)
                        throw new Exception($"overload not found {name} {cmp.Left.ResultType} {cmp.Right.ResultType}");

                    il.Call(mi);
                    break;

                case NegateExpr var:
                    VisitExpression(var.Value, il, globals, localVars);
                    il.Call(typeof(Buildin).GetMethod("Negate", new Type[] { var.Value.ResultType }));
                    break;

                case Call call:
                    VisitCall(call, il, globals, localVars);
                    break;

                case GroupingExpr group:
                    VisitExpression(group.Value, il, globals, localVars);
                    break;

                case If ifStmt:
                    VisitExpression(ifStmt.Condition, il, globals, localVars);

                    //   il.Call(typeof(Buildin).GetMethod("ConvertValueToBool"));

                    var conditionWasTrue = il.DefineLabel("if_end");
                    var conditionWasFalse = il.DefineLabel("if_false");

                    il.Brfalse(conditionWasFalse);
                    EmitBodyIL(ifStmt.TrueStatements, il, globals, localVars);
                    il.Br(conditionWasTrue);

                    il.MarkLabel(conditionWasFalse);
                    if (ifStmt.FalseStatements != null)
                    {
                        EmitBodyIL(ifStmt.FalseStatements, il, globals, localVars);
                    }
                    else
                    {
                        il.Ldnull();
                    }
                    il.MarkLabel(conditionWasTrue);
                    break;

                case LocalVariableDecl variableDecl:
                    VisitExpression(variableDecl.Value, il, globals, localVars);
                    il.Dup(); // Make sure the expression value remains after store
                    var local2 = il.DeclareLocal(variableDecl.ResultType);
                    localVars.Add(variableDecl.VariableName, local2);
                    il.Stloc(local2);
                    break;

                case ObjectRefExpr objectRef:
                    il.Ldstr(objectRef.Name);
                    il.Call(typeof(Buildin).GetMethod("ResolveObjectRef"));
                    break;

                case BranchExpr branch:
                    EmitBodyIL(branch.Body, il, globals, localVars);
                    break;

                case SyncExpr sync:
                    _syncStoreIdx = 0;

                    il.Ldarg(0); // this for Stfld(_currentField)

                    {
                        var numCoroCalls = CountCoroutineCalls(sync.Body);
                        il.Ldc_I4(numCoroCalls);
                        il.Newarr(typeof(object));

                        {
                            EmitBodyIL(sync.Body, il, globals, localVars);
                            il.Pop();
                        }

                        il.Call(typeof(Buildin).GetMethod("WaitAll"));
                    }

                    //
                    Assert.IsTrue(_isInCoroutineFunction);

                    il.Stfld(_currentField); // Store returned IEnumerator

                    if (_labelIdx < _labels.Count)
                    {
                        // Increment generator state
                        il.Ldarg(0);
                        il.Ldc_I4(_labelIdx);
                        il.Stfld(_stateField);

                        // return true
                        il.Ldc_I4(1);
                        il.Ret();

                        il.MarkLabel(_labels[_labelIdx++]);
                    }
                    else
                    {
                        // Increment generator state
                        il.Ldarg(0);
                        il.Ldc_I4(-1);
                        il.Stfld(_stateField);

                        // return false
                        il.Ldc_I4(0);
                        il.Ret();
                    }

                    il.Ldc_I4(1337); // #todo Push dummy value that can be popped by EmitBodyIL...
                    break;

                default:
                    throw new Exception("Missing case " + expr);
            }
        }

        void VisitCall(Call call, GroboIL il, Globals globals, Dictionary<string, GroboIL.Local> localVars)
        {
            if (!globals.Methods.TryGetValue(call.Name, out MethodInfo method))
                throw new Exception($"Function '{call.Name}' not found (at {call.SourceLocation})");

            if (call.IsCoroutine)
            {
                if (call.IsSync)
                {
                    il.Dup();
                    il.Ldc_I4(_syncStoreIdx++);
                }

                if (!call.IsSync && !call.IsBranch)
                {
                    if (_isInCoroutineFunction)
                    {
                        il.Ldarg(0); // this for Stfld(_currentField)
                    }
                }
            }

            for (int i = 0; i < call.Arguments.Count; i++)
            {
                var argument = call.Arguments[i];
                VisitExpression(argument, il, globals, localVars);

                var parameterType = call.Target.ParameterTypes[i];
                if (parameterType == typeof(object) && argument.ResultType == typeof(int))
                {
                    il.Box(typeof(int));
                }
                else if (parameterType == typeof(object) && argument.ResultType == typeof(bool))
                {
                    il.Box(typeof(bool));
                }
            }




            il.Call(method);


            // Exernal methods may be void
            if (method.ReturnType == typeof(void))
            {
                il.Ldnull();
            }
            else if (method.ReturnType != typeof(object))
            {
                if (!method.ReturnType.IsValueType)
                {
                    il.Castclass(typeof(object));
                }
                else
                {
                    il.Box(method.ReturnType);
                }
            }

            if (call.IsCoroutine)
            {
                if (call.IsBranch)
                {
                    il.Call(typeof(Buildin).GetMethod("StartCoroutine"));
                }

                if (call.IsSync)
                {
                    il.Stelem(typeof(object)); // Store coroutine in WaitAll array

                    il.Ldc_I4(1337); // #todo Push dummy value that can be popped by EmitBodyIL...
                }

                if (!call.IsSync && !call.IsBranch)
                {
                    Assert.IsTrue(_isInCoroutineFunction);

                    il.Stfld(_currentField); // Store returned IEnumerator

                    if (_labelIdx < _labels.Count)
                    {
                        // Increment generator state
                        il.Ldarg(0);
                        il.Ldc_I4(_labelIdx);
                        il.Stfld(_stateField);

                        // return true
                        il.Ldc_I4(1);
                        il.Ret();

                        il.MarkLabel(_labels[_labelIdx++]);
                    }
                    else
                    {
                        // Increment generator state
                        il.Ldarg(0);
                        il.Ldc_I4(-1);
                        il.Stfld(_stateField);

                        // return false
                        il.Ldc_I4(0);
                        il.Ret();
                    }

                    il.Ldc_I4(1337); // #todo Push dummy value that can be popped by EmitBodyIL...
                }
            }
        }

        int CountCoroutineCalls(List<Expression> stmts)
        {
            int numCoroCalls = 0;
            for (int i = 0; i < stmts.Count; i++)
            {
                Expression stmt = stmts[i];
                if (stmt is Call call && call.IsCoroutine)
                {
                    ++numCoroCalls;
                }
                if (stmt is SyncExpr)
                {
                    ++numCoroCalls;
                }
            }
            return numCoroCalls;
        }
    }
}