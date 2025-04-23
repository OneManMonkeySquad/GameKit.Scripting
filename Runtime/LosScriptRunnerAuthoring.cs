using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Los.Runtime;
using System.IO;
using System.Collections.Generic;
using System;

namespace Los.Runtime
{
    public struct BakedScript : IComponentData
    {
        public FixedString64Bytes FileNameHint;
        public BlobString Code;
    }

    public struct LosScriptRunner : IComponentData
    {
        public BlobAssetReference<BakedScript> Script;
        public FixedString32Bytes FunctionName;
    }



    public class LosScriptRunnerAuthoring : MonoBehaviour
    {
        public ScriptAsset Script;
        public string FunctionName;

        public class Baker : Baker<LosScriptRunnerAuthoring>
        {
            public override void Bake(LosScriptRunnerAuthoring authoring)
            {
                // Bake script
                BlobAssetReference<BakedScript> result = new();
                if (authoring.Script != null)
                {
                    var builder = new BlobBuilder(Allocator.Temp);
                    ref BakedScript script = ref builder.ConstructRoot<BakedScript>();

                    script.FileNameHint = authoring.Script.name;
                    builder.AllocateString(ref script.Code, authoring.Script.Code);

                    result = builder.CreateBlobAssetReference<BakedScript>(Allocator.Persistent);
                    builder.Dispose();
                }

                //
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new LosScriptRunner
                {
                    Script = result,
                    FunctionName = authoring.FunctionName,
                });
            }
        }
    }

    public partial struct LosScriptRunnerSystem : ISystem
    {
        class CachedEngine
        {
            public Engine Engine;
            public DateTime LastChangeTime;
        }

        class Singleton : IComponentData
        {
            public Dictionary<string, CachedEngine> Engines = new();
        }

        public void OnCreate(ref SystemState state)
        {
            var ent = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentObject(ent, new Singleton());
        }

        public void OnUpdate(ref SystemState state)
        {
            var ctx = new ExecContext
            {
                EntityManager = state.EntityManager,
            };

            var singleton = SystemAPI.ManagedAPI.GetSingleton<Singleton>();

            foreach (var (foo, entity) in SystemAPI.Query<LosScriptRunner>().WithEntityAccess())
            {
                var engine = Script.Compile(foo.Script.Value.Code.ToString(), foo.Script.Value.FileNameHint.ToString());
                engine.SetVar("entity", Value.FromEntity(entity));
                engine.SetVar("elapsed_time", Value.FromDouble(SystemAPI.Time.ElapsedTime));
                engine.ExecuteFunc(foo.FunctionName.ToString(), ctx);
            }
        }
    }
}