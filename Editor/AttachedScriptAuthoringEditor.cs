using System;
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
                else
                {
                    EditorGUILayout.LabelField("Properties", EditorStyles.boldLabel);
                    EditorGUILayout.BeginVertical("box"); // Start boxed section

                    if (script.PropertyValues == null)
                    {
                        script.PropertyValues = new GameObject[script.Asset.PropertyNames.Count];
                        script.PropertyNames = script.Asset.PropertyNames.ToArray();
                    }
                    else if (script.PropertyValues.Length != script.Asset.PropertyNames.Count)
                    {
                        var newPropertyValues = new GameObject[script.Asset.PropertyNames.Count];

                        for (int i = 0; i < script.Asset.PropertyNames.Count; ++i)
                        {
                            var newName = script.Asset.PropertyNames[i];

                            var oldIdx = Array.IndexOf(script.PropertyNames, newName);
                            if (oldIdx != -1)
                            {
                                newPropertyValues[i] = script.PropertyValues[i];
                            }
                        }

                        script.PropertyValues = newPropertyValues;
                        script.PropertyNames = script.Asset.PropertyNames.ToArray();
                    }
                    // #todo handle other changes: order, types

                    for (int i = 0; i < script.Asset.PropertyNames.Count; i++)
                    {
                        string prop = script.Asset.PropertyNames[i];
                        var newValue = (GameObject)EditorGUILayout.ObjectField(prop, script.PropertyValues[i], typeof(GameObject), allowSceneObjects: true);
                        script.PropertyValues[i] = newValue;
                    }

                    EditorGUILayout.EndVertical(); // End boxed section
                }
            }

            if (GUI.changed)
            {
                EditorUtility.SetDirty(target);
            }
        }
    }
}