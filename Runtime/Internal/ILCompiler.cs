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

    public class ILCompiler
    {
        public static string Output;

        public static void Print(Value val)
        {
            Debug.Log(val.ToString());
            if (Output != null)
            {
                Output += val.ToString();
            }
        }

        public static Value Add(Value left, Value right)
        {
            return (left.Type, right.Type) switch
            {
                (ValueType.Int, ValueType.Int) => Value.FromInt(left.AsInt + right.AsInt),
                (ValueType.Float, ValueType.Float) => Value.FromFloat(left.AsFloat + right.AsFloat),
                (ValueType.Float, ValueType.Double) => Value.FromDouble(left.AsFloat + right.AsDouble),
                // (ValueType.StringIdx, ValueType.StringIdx) => Value.FromStringIdx(ctx.StringPool.Store(ctx.StringPool.Get(left.AsStringIdx) + ctx.StringPool.Get(right.AsStringIdx))),
                _ => throw new Exception("Unexpected types for add " + (left.Type, right.Type)),
            };
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

                var method = new DynamicMethod(func.Name, null, parameters, typeof(ILCompiler));

                methods[func.Name] = method;
            }

            methods["print"] = typeof(ILCompiler).GetMethod("Print");

            foreach (var func in ast.Functions)
            {
                var method = methods[func.Name];

                EmitIL(func, (DynamicMethod)method, methods);
            }

            ca.Functions = methods;

            return ca;
        }

        static void LePrint(Value val)
        {

        }

        static void Test()
        {
            LePrint(Value.FromInt(42));
        }

        DynamicMethod EmitIL(DeclareFunc func, DynamicMethod method, Dictionary<string, MethodInfo> methods)
        {
            File.AppendAllText("E:\\il.txt", $"=== {func.Name}({string.Join(',', func.Parameters)}) ===\n");

            using (var il = new GroboIL(method))
            {
                foreach (var stmt in func.Statements)
                {
                    VisitStatement(stmt, il, methods);
                }
                il.Ret();

                File.AppendAllText("E:\\il.txt", il.GetILCode());
            }

            return method;
        }

        void VisitStatement(Statement stmt, GroboIL il, Dictionary<string, MethodInfo> methods)
        {
            switch (stmt)
            {
                case Call call:
                    foreach (var arg in call.Arguments)
                    {
                        VisitExpression(arg, il);
                    }
                    il.Call(methods[call.Name]);
                    break;
            }
        }

        void VisitExpression(Expression expr, GroboIL il)
        {
            switch (expr)
            {
                case ValueExpr var:
                    switch (var.Value.Type)
                    {
                        case ValueType.Int:
                            il.Ldc_I4(var.Value.AsInt);
                            il.Call(typeof(Value).GetMethod("FromInt"));
                            break;
                    }
                    break;
                case VariableExpr var:
                    il.Ldc_I4(42); // #todo
                    il.Call(typeof(Value).GetMethod("FromInt"));
                    break;
                case StringExpr var:
                    il.Ldc_I4(0); // #todo
                    il.Call(typeof(Value).GetMethod("FromStringIdx"));
                    break;
                case AddExpr var:
                    VisitExpression(var.Left, il);
                    VisitExpression(var.Right, il);
                    il.Call(typeof(ILCompiler).GetMethod("Add"));
                    break;
            }
        }
    }
}