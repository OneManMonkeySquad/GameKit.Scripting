using System;
using System.IO;
using System.Linq;
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

            asset.LastCompilationFailed = true;

            try
            {
                var ast = Script.Parse(code, ctx.assetPath);
                asset.Code = code;
                asset.FileNameHint = ctx.assetPath;
                asset.PropertyNames = ast.Properties.Select(p => p.Name).ToList();
                asset.PropertyTypeNames = ast.Properties.Select(p => p.TypeName).ToList();

                asset.LastCompilationFailed = false;

                var cs = Script.Compile(ast);
                cs.TryExecute("on_build");
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }
    }
}