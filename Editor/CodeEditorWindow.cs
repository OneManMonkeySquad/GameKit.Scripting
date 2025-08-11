using UnityEngine;
using UnityEditor;
using GameKit.Scripting.Runtime;
using System;

namespace GameKit.Scripting
{
    public class CodeEditorWindow : EditorWindow
    {
        private AttachedScriptAuthoring targetScript;
        private string tempString;

        public static void OpenWindow(AttachedScriptAuthoring target)
        {
            var window = GetWindow<CodeEditorWindow>("Code Editor");
            window.targetScript = target;
            window.tempString = target.Code;
            window.Show();
        }

        private void OnGUI()
        {
            if (targetScript == null)
            {
                EditorGUILayout.LabelField("No target selected.");
                return;
            }

            // Prevent selecting all text
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.A && e.control)
            {
                e.Use(); // Consume the event
            }

            // Calculate the height of the line to highlight
            float lineHeight = EditorGUIUtility.singleLineHeight;

            //
            tempString = EditorGUILayout.TextArea(tempString, GUILayout.Height(300), GUILayout.ExpandHeight(true));
            Rect textAreaRect = GUILayoutUtility.GetLastRect();

            //
            var parseResult = Script.Parse(tempString, "<code>");
            if (parseResult.Failed)
            {
                var err = parseResult.Errors[0];
                EditorGUILayout.HelpBox($"{err.SourceLoc.Line}: {err.Message}", MessageType.Error);

                // Draw the highlight
                var highlightLine = err.SourceLoc.Line - 1; // Line is 1-based
                Rect highlightRect = new Rect(textAreaRect.x, textAreaRect.y + (highlightLine * lineHeight), textAreaRect.width, lineHeight);
                EditorGUI.DrawRect(highlightRect, new Color(1, 0, 0, 0.2f)); // Highlight
            }
            else
            {
                EditorGUILayout.HelpBox("Code compiles fine", MessageType.Info);
            }

            GUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            {
                EditorGUI.BeginDisabledGroup(parseResult.Failed);
                if (GUILayout.Button("Save"))
                {
                    Undo.RecordObject(targetScript, "Edit String");
                    targetScript.Code = tempString;
                    EditorUtility.SetDirty(targetScript);
                    Close();
                }
                EditorGUI.EndDisabledGroup();

                if (GUILayout.Button("Revert"))
                {
                    tempString = targetScript.Code;
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}