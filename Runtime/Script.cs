using System.IO;

namespace GameKit.Scripting.Runtime
{
    public static class Script
    {
        public static string Execute(string str, ExecContext ctx = new ExecContext()) => ExecuteFunc(str, "main", ctx);

        public static string ExecuteFunc(string str, string funcName, ExecContext ctx = new ExecContext()) => Execute(str, funcName, "<string>", ctx);

        static string Execute(string str, string funcName, string fileNameHint, ExecContext ctx = new ExecContext())
        {
            ILCompiler.Output = "";

            var ast = Compile(str, fileNameHint);
            ast.Execute(funcName);

            return ILCompiler.Output;
        }

        public static CompiledScript CompileFile(string filePath)
        {
            var code = File.ReadAllText(filePath);
            return Compile(code, Path.GetFileName(filePath));
        }

        public static CompiledScript Compile(ref BakedScript script) => Compile(script.Code.ToString(), script.FileNameHint.ToString());


        public static CompiledScript Compile(string str, string fileNameHint)
        {
            var parser = new Parser();
            var ast = parser.ParseToAst(str, fileNameHint);

            var compiler = new ILCompiler();
            var ca = compiler.Compile(ast);
            return ca;
        }
    }
}