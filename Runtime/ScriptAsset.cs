using System.Collections.Generic;
using UnityEngine;

namespace GameKit.Scripting.Runtime
{
    [Icon("Packages/com.onemanmonkeysquad.gamekit.scripting/Editor/Resources/Script.png")]
    public sealed class ScriptAsset : ScriptableObject
    {
        public string Code;
        public string FileNameHint;
        public bool LastCompilationFailed;
    }
}