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
            Buildin.Output = "";

            var compiledScript = Compile(str, fileNameHint);
            compiledScript.Execute(funcName);

            return Buildin.Output;
        }

        public static CompiledScript CompileFile(string filePath)
        {
            var code = File.ReadAllText(filePath);
            return Compile(code, Path.GetFileName(filePath));
        }

        public static CompiledScript Compile(string str, string fileNameHint)
        {
            var methods = new Dictionary<string, MethodInfo>();
            RegisterScriptableFunctions(methods);

            var ast = Parse(str, fileNameHint, methods);
            return CompileAst(ast, methods);
        }

        public static Ast Parse(string str, string fileNameHint, Dictionary<string, MethodInfo> methods)
        {
            var parser = new Parser();
            var ast = parser.ParseToAst(str, fileNameHint, methods);
            return ast;
        }

        public static CompiledScript CompileAst(Ast ast, Dictionary<string, MethodInfo> methods)
        {
            var compiler = new ILCompiler();
            var ca = compiler.Compile(ast, methods);
            return ca;
        }

        public static void RegisterScriptableFunctions(Dictionary<string, MethodInfo> methods)
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
    }
}