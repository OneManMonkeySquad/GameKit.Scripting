using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Los.Runtime;
using System.IO;
using System.Collections.Generic;
using System;

public struct LosScriptRunner : IComponentData
{
    public FixedString32Bytes FileName;
    public FixedString32Bytes FunctionName;
}



public class LosScriptRunnerAuthoring : MonoBehaviour
{
    public string FileName;
    public string FunctionName;

    public class Baker : Baker<LosScriptRunnerAuthoring>
    {
        public override void Bake(LosScriptRunnerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new LosScriptRunner
            {
                FileName = authoring.FileName,
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
            var filePath = "E:\\projects\\Factions2\\Assets\\Scripts\\Los\\Tests\\" + foo.FileName.ToString();


            var changeTime = File.GetLastWriteTime(filePath);
            if (!singleton.Engines.TryGetValue(filePath, out CachedEngine cachedEngine)
                || cachedEngine.LastChangeTime != changeTime)
            {
                var newEngine = Script.CompileFile(filePath);
                cachedEngine = new CachedEngine
                {
                    Engine = newEngine,
                    LastChangeTime = changeTime,
                };

                singleton.Engines[filePath] = cachedEngine;
            }

            var engine = cachedEngine.Engine;
            engine.SetVar("entity", Value.FromEntity(entity));
            engine.SetVar("elapsed_time", Value.FromDouble(SystemAPI.Time.ElapsedTime));
            engine.ExecuteFunc(foo.FunctionName.ToString(), ctx);
        }
    }
}