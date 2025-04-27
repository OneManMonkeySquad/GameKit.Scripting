using UnityEngine;
using Unity.Entities;
using Unity.Collections;

namespace GameKit.Scripting.Runtime
{
    public struct BakedScript : IComponentData
    {
        public FixedString64Bytes FileNameHint;
        public BlobString Code;
    }

    public struct AttachedScript : IComponentData
    {
        public BlobAssetReference<BakedScript> Script;
    }

    [InternalBufferCapacity(0)]
    public struct QueuedScriptEvent : IBufferElementData
    {
        public FixedString64Bytes Name;
    }

    public class AttachedScriptAuthoring : MonoBehaviour
    {
        public ScriptAsset Script;

        public class Baker : Baker<AttachedScriptAuthoring>
        {
            public override void Bake(AttachedScriptAuthoring authoring)
            {
                // Bake script
                BlobAssetReference<BakedScript> result = new();
                if (authoring.Script != null)
                {
                    DependsOn(authoring.Script);

                    var builder = new BlobBuilder(Allocator.Temp);
                    ref BakedScript script = ref builder.ConstructRoot<BakedScript>();

                    script.FileNameHint = authoring.Script.name;
                    builder.AllocateString(ref script.Code, authoring.Script.Code);

                    result = builder.CreateBlobAssetReference<BakedScript>(Allocator.Persistent);
                    builder.Dispose();
                }

                //
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new AttachedScript
                {
                    Script = result
                });
                var buff = AddBuffer<QueuedScriptEvent>(entity);
                buff.Add(new QueuedScriptEvent { Name = "OnInit" });
            }
        }
    }
}