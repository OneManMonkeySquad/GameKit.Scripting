using System;
using System.Collections.Generic;
using GameKit.Scripting.Internal;
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

                    if (script.PropertyValuesManaged == null || script.PropertyValuesManaged.Length != script.Asset.PropertyNames.Count)
                    {
                        var newPropertyValuesManaged = new UnityEngine.Object[script.Asset.PropertyNames.Count];
                        var newPropertyValuesPod = new PodValue[script.Asset.PropertyNames.Count];

                        if (script.PropertyValuesManaged != null)
                        {
                            for (int i = 0; i < script.Asset.PropertyNames.Count; ++i)
                            {
                                var newName = script.Asset.PropertyNames[i];

                                var oldIdx = Array.IndexOf(script.PropertyNames, newName);
                                if (oldIdx != -1)
                                {
                                    newPropertyValuesManaged[i] = script.PropertyValuesManaged[i];
                                    newPropertyValuesPod[i] = script.PropertyValuesPod[i];
                                }
                            }
                        }

                        script.PropertyValuesManaged = newPropertyValuesManaged;
                        script.PropertyValuesPod = newPropertyValuesPod;
                        script.PropertyNames = script.Asset.PropertyNames.ToArray();
                        script.PropertyTypeNames = script.Asset.PropertyTypeNames.ToArray();

                        EditorUtility.SetDirty(target);

                        return; // Make sure to init once without the rest breaking stuff
                    }
                    // #todo handle other changes: order, types

                    for (int i = 0; i < script.Asset.PropertyNames.Count; i++)
                    {
                        string prop = script.Asset.PropertyNames[i];
                        string propTypeName = script.Asset.PropertyTypeNames[i];

                        var propertyType = ScriptingTypeCache.ByName(propTypeName);
                        if (!propertyType.IsClass && propertyType != typeof(Entity))
                        {
                            if (propertyType == typeof(int))
                            {
                                var newValue = EditorGUILayout.IntField(prop, !script.PropertyValuesPod[i].IsNull ? script.PropertyValuesPod[i].AsInt : 0);
                                script.PropertyValuesPod[i] = PodValue.FromInt(newValue);
                            }
                            else
                            {
                                Debug.LogError("Missing");
                            }
                        }
                        else
                        {
                            if (propertyType == typeof(Entity))
                            {
                                var newValue = EditorGUILayout.ObjectField(prop, script.PropertyValuesManaged[i], typeof(GameObject), allowSceneObjects: true);
                                script.PropertyValuesManaged[i] = newValue;
                            }
                            else
                            {
                                var newValue = EditorGUILayout.ObjectField(prop, script.PropertyValuesManaged[i], propertyType, allowSceneObjects: true);
                                script.PropertyValuesManaged[i] = newValue;
                            }
                        }
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