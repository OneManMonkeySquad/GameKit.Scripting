using System;
using System.IO;
using GameKit.Scripting.Internal;

namespace GameKit.Scripting.Runtime
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ScriptableAttribute : Attribute
    {
        public readonly string Name;

        public ScriptableAttribute(string name)
        {
            Name = name;
        }
    }

    public static class Script
    {
        public static string Execute(string str) => ExecuteFunc(str, "main");

        public static string ExecuteFunc(string str, string funcName) => Execute(str, funcName, "");

        static string Execute(string str, string funcName, string fileNameHint)
        {
            ILCompiler.Output = "";

            var compiledScript = Compile(str, fileNameHint);
            compiledScript.Execute(funcName);

            return ILCompiler.Output;
        }

        public static CompiledScript CompileFile(string filePath)
        {
            var code = File.ReadAllText(filePath);
            return Compile(code, Path.GetFileName(filePath));
        }

        public static CompiledScript Compile(string str, string fileNameHint)
        {
            var ast = Parse(str, fileNameHint);
            return Compile(ast);
        }

        public static CompiledScript Compile(Ast ast)
        {
            var compiler = new ILCompiler();
            var ca = compiler.Compile(ast);
            return ca;
        }

        public static Ast Parse(string str, string fileNameHint)
        {
            var parser = new Parser();
            var ast = parser.ParseToAst(str, fileNameHint);
            return ast;
        }
    }
}