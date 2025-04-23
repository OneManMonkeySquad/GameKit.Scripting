using System.IO;
using Los.Runtime;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Los.Editor
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

            asset.Code = code;

            Script.Compile(asset.Code, name);
        }
    }

}