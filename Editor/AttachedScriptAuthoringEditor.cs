using System.Collections.Generic;
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
            script.Script = (ScriptAsset)EditorGUILayout.ObjectField(script.Script, typeof(ScriptAsset), allowSceneObjects: false);
            EditorGUILayout.EndHorizontal();

            script.TransformUsage = (TransformUsageFlags)EditorGUILayout.EnumFlagsField("Transform Usage", script.TransformUsage);

            if (script.Script != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Properties", EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical("box"); // Start boxed section

                if (script.PropertyValues == null)
                {
                    script.PropertyValues = new GameObject[script.Script.PropertyNames.Count];
                }
                else if (script.PropertyValues.Length != script.Script.PropertyNames.Count)
                {
                    // #todo MERGE
                    script.PropertyValues = new GameObject[script.Script.PropertyNames.Count];
                }

                for (int i = 0; i < script.Script.PropertyNames.Count; i++)
                {
                    string prop = script.Script.PropertyNames[i];
                    var newValue = (GameObject)EditorGUILayout.ObjectField(prop, script.PropertyValues[i], typeof(GameObject), allowSceneObjects: true); ;
                    script.PropertyValues[i] = newValue;
                }

                EditorGUILayout.EndVertical(); // End boxed section
            }

            if (GUI.changed)
            {
                EditorUtility.SetDirty(target);
            }
        }
    }
}