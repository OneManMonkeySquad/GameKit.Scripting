using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using UnityEditor;

namespace GameKit.Scripting.Runtime
{
    public struct BakedScript : IComponentData
    {
        public FixedString64Bytes FileNameHint;
        public BlobString Code;
        public int CodeHash;
    }

    public struct AttachedScript : IComponentData
    {
        public BlobAssetReference<BakedScript> Script;
    }

    [InternalBufferCapacity(0)]
    public struct PropertyValue : IBufferElementData
    {
        public FixedString32Bytes Name; // #todo this should be baked into BakedScript
        public Value Value;
    }

    [InternalBufferCapacity(0)]
    public struct QueuedScriptEvent : IBufferElementData
    {
        public FixedString32Bytes Name;
    }

    public class AttachedScriptAuthoring : MonoBehaviour
    {
        public ScriptAsset Asset;
        public TransformUsageFlags TransformUsage;

        public string[] PropertyNames;
        public GameObject[] PropertyValues;

        public class Baker : Baker<AttachedScriptAuthoring>
        {
            public override void Bake(AttachedScriptAuthoring authoring)
            {
                // Bake script
                BlobAssetReference<BakedScript> result = new();
                if (authoring.Asset != null)
                {
                    DependsOn(authoring.Asset);

                    var builder = new BlobBuilder(Allocator.Temp);
                    ref BakedScript script = ref builder.ConstructRoot<BakedScript>();

                    script.FileNameHint = authoring.Asset.FileNameHint;
                    builder.AllocateString(ref script.Code, authoring.Asset.Code);
                    script.CodeHash = authoring.Asset.Code.GetHashCode(); // #todo include property values

                    result = builder.CreateBlobAssetReference<BakedScript>(Allocator.Persistent);
                    builder.Dispose();

                    //
                    var cs = Script.Compile(authoring.Asset.Code, authoring.Asset.FileNameHint);
                    if (cs.HasFunction("on_bake"))
                    {
                        cs.Execute("on_bake");
                    }
                }

                //
                var entity = GetEntity(authoring.TransformUsage);
                AddComponent(entity, new AttachedScript
                {
                    Script = result
                });

                var eventBuff = AddBuffer<QueuedScriptEvent>(entity);
                eventBuff.Add(new QueuedScriptEvent { Name = "on_init" });

                var propertyBuff = AddBuffer<PropertyValue>(entity);
                for (int i = 0; i < authoring.PropertyValues.Length; ++i)
                {
                    var value = new PropertyValue { };
                    value.Name = authoring.PropertyNames[i];
                    if (authoring.PropertyValues[i] != null)
                    {
                        value.Value = Value.FromEntity(GetEntity(authoring.PropertyValues[i], TransformUsageFlags.None));
                    }
                    propertyBuff.Add(value);
                }
            }
        }
    }
}