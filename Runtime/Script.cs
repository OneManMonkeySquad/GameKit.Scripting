using System.IO;

namespace GameKit.Scripting.Runtime
{
    public static class Script
    {
        public static string Execute(string str, ExecContext ctx = new ExecContext()) => ExecuteFunc(str, "main", ctx);

        public static string ExecuteFunc(string str, string funcName, ExecContext ctx = new ExecContext()) => Execute(str, funcName, "<string>", ctx);

        static string Execute(string str, string funcName, string fileNameHint, ExecContext ctx = new ExecContext())
        {
            ScriptEngine.Output = "";

            var ast = Compile(str, fileNameHint);

            var engine = new ScriptEngine();
            engine.ExecuteFunc(ast, funcName, ctx);

            return ScriptEngine.Output;
        }

        public static Ast CompileFile(string filePath)
        {
            var code = File.ReadAllText(filePath);
            return Compile(code, Path.GetFileName(filePath));
        }

        public static Ast Compile(ref BakedScript script) => Compile(script.Code.ToString(), script.FileNameHint.ToString());


        public static Ast Compile(string str, string fileNameHint)
        {
            var parser = new Parser();
            var ast = parser.ParseToAst(str, fileNameHint);

            return ast;
        }
    }
}