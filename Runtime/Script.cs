using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using GameKit.Scripting.Internal;
using UnityEditor;
using UnityEngine;

namespace GameKit.Scripting.Runtime
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ScriptableAttribute : Attribute
    {
        public readonly string Name;

        /// <summary>
        /// Mark function as exposed to scripting. Note that if no name is provided the
        /// methods name is converted to snake-case.
        /// </summary>
        public ScriptableAttribute(string name = null)
        {
            Name = name;
        }
    }

    public struct CompileResult
    {
        public bool Failed => Script == null;

        public CompiledScript Script;

        public List<ParserException> Errors;
    }

    public static class Script
    {
        public static string Execute(string code) => ExecuteFunc(code, "main");

        public static string ExecuteFile(string filePath)
        {
            var code = File.ReadAllText(filePath);
            return ExecuteFunc(code, "main");
        }

        public static string ExecuteFunc(string code, string funcName) => Execute(code, funcName, "");

        static string Execute(string code, string funcName, string fileNameHint)
        {
            Buildin.Output = "";

            var result = Compile(code, fileNameHint);
            if (result.Failed)
                return null; // #todo

            result.Script.ExecuteFunction(funcName);

            return Buildin.Output;
        }

        public static CompileResult CompileFile(string filePath)
        {
            var code = File.ReadAllText(filePath);
            return Compile(code, Path.GetFileName(filePath));
        }

        public static CompileResult Compile(string code, string fileNameHint)
        {
            var methods = new Dictionary<string, MethodInfo>();
            RegisterScriptableFunctions(methods);

            var parseResult = Parse(code, fileNameHint, methods);
            return CompileAst(parseResult, methods);
        }

        public static ParserResult Parse(string code, string fileNameHint)
        {
            var methods = new Dictionary<string, MethodInfo>();
            RegisterScriptableFunctions(methods);

            var parser = new Parser();
            var result = parser.ParseToAst(code, fileNameHint, methods);
            return result;
        }

        public static ParserResult Parse(string code, string fileNameHint, Dictionary<string, MethodInfo> methods)
        {
            var parser = new Parser();
            var result = parser.ParseToAst(code, fileNameHint, methods);
            return result;
        }

        public static CompileResult CompileAst(ParserResult result, Dictionary<string, MethodInfo> methods)
        {
            if (result.Failed)
                return new CompileResult { Errors = result.Errors };

            var compiler = new ILCompiler();
            var ca = compiler.Compile(result.Ast, methods);
            return new CompileResult { Script = ca };
        }

        public static void RegisterScriptableFunctions(Dictionary<string, MethodInfo> methods)
        {
#if UNITY_EDITOR
            var taggedMethods = TypeCache.GetMethodsWithAttribute<ScriptableAttribute>();
            foreach (var taggedMethod in taggedMethods)
            {
                var name = taggedMethod.GetCustomAttribute<ScriptableAttribute>().Name;
                if (name == null)
                {
                    name = ConvertStringToSnakeCase(taggedMethod.Name);
                }

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

        /// <summary>
        /// GetCake => get_cake
        /// </summary>
        static string ConvertStringToSnakeCase(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            var result = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsUpper(c))
                {
                    if (i > 0)
                    {
                        result.Append('_');
                    }
                    result.Append(char.ToLower(c));
                }
                else
                {
                    result.Append(c);
                }
            }
            return result.ToString();
        }
    }
}