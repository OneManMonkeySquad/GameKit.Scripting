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
    public struct ScriptEvent : IBufferElementData
    {
        public FixedString32Bytes Name;
    }

    public class AttachedScriptAuthoring : MonoBehaviour
    {
        public ScriptAsset Asset;
        public TransformUsageFlags TransformUsage;

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
                    script.CodeHash = authoring.Asset.Code.GetHashCode();

                    result = builder.CreateBlobAssetReference<BlobScript>(Allocator.Persistent);
                    builder.Dispose();

                    //
                    var cs = Script.Compile(authoring.Asset.Code, authoring.Asset.FileNameHint);
                    cs.TryExecuteFunction("on_bake");
                }

                //
                var entity = GetEntity(authoring.TransformUsage);
                AddComponent(entity, new AttachedScript { Script = result });

                var eventBuff = AddBuffer<ScriptEvent>(entity);
                eventBuff.Add(new ScriptEvent { Name = "on_init" });
            }
        }
    }
}