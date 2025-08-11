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

            script.TransformUsage = (TransformUsageFlags)EditorGUILayout.EnumFlagsField("Transform Usage", script.TransformUsage);

            script.Source = (AttachedScriptAuthoring.SourceType)EditorGUILayout.EnumPopup("Source", script.Source);
            if (script.Source == AttachedScriptAuthoring.SourceType.Inline)
            {
                if (GUILayout.Button("Edit Code..."))
                {
                    CodeEditorWindow.OpenWindow((AttachedScriptAuthoring)target);
                }

                var code = LimitLines(script.Code, 18);

                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField("", code, GUILayout.Height(280));
                EditorGUI.EndDisabledGroup();
            }
            else if (script.Source == AttachedScriptAuthoring.SourceType.File)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("Script");
                script.Asset = (ScriptAsset)EditorGUILayout.ObjectField(script.Asset, typeof(ScriptAsset), allowSceneObjects: false);
                EditorGUILayout.EndHorizontal();
            }

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

        public static string LimitLines(string input, int maxLines)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Split by newlines (handles both \n and \r\n)
            var lines = input.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);

            if (lines.Length <= maxLines)
                return input;

            // Take only the allowed number of lines, then append "<more...>"
            var limited = new System.Text.StringBuilder();
            for (int i = 0; i < maxLines; i++)
            {
                limited.AppendLine(lines[i]);
            }
            limited.Append("<more...>");

            return limited.ToString();
        }
    }
}