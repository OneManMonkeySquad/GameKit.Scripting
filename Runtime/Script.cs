
using System.IO;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Los.Runtime
{
    public static class Script
    {
        static string _output;

        public static string Execute(string str, ExecContext ctx = new ExecContext()) => ExecuteFunc(str, "main", ctx);

        public static string ExecuteFunc(string str, string funcName, ExecContext ctx = new ExecContext()) => Execute(str, funcName, "<string>", ctx);

        static string Execute(string str, string funcName, string fileNameHint, ExecContext ctx = new ExecContext())
        {
            _output = "";

            var engine = Compile(str, fileNameHint);
            engine.ExecuteFunc(funcName, ctx);

            return _output;
        }

        public static Engine CompileFile(string filePath)
        {
            var code = File.ReadAllText(filePath);
            return Compile(code, Path.GetFileName(filePath));
        }

        public static Engine Compile(ref BakedScript script) => Compile(script.Code.ToString(), script.FileNameHint.ToString());


        public static Engine Compile(string str, string fileNameHint)
        {
            var parser = new Parser();
            var ast = parser.ParseToAst(str, fileNameHint);

            var engine = new Engine(ast);
            RegisterBuildinFunctions(engine);

            return engine;
        }

        static void RegisterBuildinFunctions(Engine engine)
        {
            engine.RegisterAction("print", Print);
            engine.RegisterFunc("str", str);
            engine.RegisterFunc("sin", sin);
            engine.RegisterFunc("as_float", as_float);
            engine.RegisterFunc("has_component", has_component);
        }

        static void Print(ExecContext ctx, Value obj)
        {
            Debug.Log(obj.ToString(ctx));
            _output += obj.ToString(ctx);
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
    }
}