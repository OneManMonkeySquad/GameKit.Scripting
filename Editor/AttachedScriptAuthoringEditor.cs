using GameKit.Scripting.Runtime;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace GameKit.Scripting
{
    [CustomEditor(typeof(AttachedScriptAuthoring))]
    public class AttachedScriptAuthoringEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var script = (AttachedScriptAuthoring)target;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Script");
            script.Asset = (ScriptAsset)EditorGUILayout.ObjectField(script.Asset, typeof(ScriptAsset), allowSceneObjects: false);
            EditorGUILayout.EndHorizontal();

            script.TransformUsage = (TransformUsageFlags)EditorGUILayout.EnumFlagsField("Transform Usage", script.TransformUsage);

            if (script.Asset != null)
            {
                EditorGUILayout.Space();

                if (script.Asset.LastCompilationFailed)
                {
                    GUIStyle redBoldStyle = new GUIStyle(EditorStyles.label);
                    redBoldStyle.normal.textColor = Color.red;
                    redBoldStyle.fontStyle = FontStyle.Bold;

                    EditorGUILayout.LabelField("Last Compilation failed", redBoldStyle);
                }
            }

            if (GUI.changed)
            {
                EditorUtility.SetDirty(target);
            }
        }
    }
}