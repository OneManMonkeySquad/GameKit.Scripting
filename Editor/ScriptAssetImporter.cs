using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using GameKit.Scripting.Runtime;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace GameKit.Scripting
{
    [ScriptedImporter(1, "script", AllowCaching = true)]
    public sealed class ScriptAssetImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var code = File.ReadAllText(ctx.assetPath);

            var asset = AssetDatabase.LoadAssetAtPath<ScriptAsset>(assetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<ScriptAsset>();
                ctx.AddObjectToAsset("Script", asset);
                ctx.SetMainObject(asset);
            }

            asset.LastCompilationFailed = false;

            try
            {
                var methods = new Dictionary<string, MethodInfo>();
                Script.RegisterScriptableFunctions(methods);

                var ast = Script.Parse(code, ctx.assetPath, methods);
                asset.Code = code;
                asset.FileNameHint = ctx.assetPath;

                var cs = Script.CompileAst(ast, methods);
                cs.TryExecuteFunction("on_build");

                asset.LastCompilationFailed = false;
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}