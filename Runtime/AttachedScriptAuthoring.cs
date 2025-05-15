using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using GameKit.Scripting.Internal;

namespace GameKit.Scripting.Runtime
{
    public struct BlobScript
    {
        public FixedString64Bytes FileNameHint;
        public BlobString Code;
        public int CodeHash;
    }

    public struct AttachedScript : IComponentData
    {
        public BlobAssetReference<BlobScript> Script;
    }

    [InternalBufferCapacity(0)]
    public struct PropertyValue : IBufferElementData
    {
        public FixedString32Bytes Name; // #todo this should be baked into BakedScript
        public UnityObjectRef<Object> ValueManaged;
        public PodValue Value;
    }

    [InternalBufferCapacity(0)]
    public struct ScriptEvent : IBufferElementData
    {
        public FixedString32Bytes Name;
    }

    public class AttachedScriptAuthoring : MonoBehaviour
    {
        public ScriptAsset Asset;
        public TransformUsageFlags TransformUsage;

        public string[] PropertyNames;
        public string[] PropertyTypeNames;
        public Object[] PropertyValuesManaged;
        public PodValue[] PropertyValuesPod;

        public class Baker : Baker<AttachedScriptAuthoring>
        {
            public override void Bake(AttachedScriptAuthoring authoring)
            {
                // Bake script
                BlobAssetReference<BlobScript> result = new();
                if (authoring.Asset != null)
                {
                    DependsOn(authoring.Asset);

                    var builder = new BlobBuilder(Allocator.Temp);
                    ref BlobScript script = ref builder.ConstructRoot<BlobScript>();

                    script.FileNameHint = authoring.Asset.FileNameHint;
                    builder.AllocateString(ref script.Code, authoring.Asset.Code);
                    script.CodeHash = authoring.Asset.Code.GetHashCode(); // #todo include property values

                    result = builder.CreateBlobAssetReference<BlobScript>(Allocator.Persistent);
                    builder.Dispose();

                    //
                    var cs = Script.Compile(authoring.Asset.Code, authoring.Asset.FileNameHint);
                    cs.TryExecute("on_bake");
                }

                //
                var entity = GetEntity(authoring.TransformUsage);
                AddComponent(entity, new AttachedScript { Script = result });

                var eventBuff = AddBuffer<ScriptEvent>(entity);
                eventBuff.Add(new ScriptEvent { Name = "on_init" });

                var propertyBuff = AddBuffer<PropertyValue>(entity);
                if (authoring.PropertyValuesManaged != null)
                {
                    for (int i = 0; i < authoring.PropertyTypeNames.Length; ++i)
                    {
                        var value = new PropertyValue { };
                        value.Name = authoring.PropertyNames[i];

                        var propertyTypeName = authoring.PropertyTypeNames[i];
                        var propertyType = ScriptingTypeCache.ByName(propertyTypeName);
                        if (!propertyType.IsClass && propertyType != typeof(Entity))
                        {
                            if (propertyType == typeof(int))
                            {
                                value.Value = authoring.PropertyValuesPod[i];
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
                                value.Value = PodValue.FromEntity(GetEntity((GameObject)authoring.PropertyValuesManaged[i], TransformUsageFlags.None));
                            }
                            else
                            {
                                value.ValueManaged = authoring.PropertyValuesManaged[i];
                            }
                        }

                        propertyBuff.Add(value);
                    }
                }
            }
        }
    }
}