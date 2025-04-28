using GameKit.Scripting.Internal;
using Unity.Entities;

namespace GameKit.Scripting.Runtime
{
    public class AttachedCompiledScript : IComponentData
    {
        public CompiledScript Script;
    }

    public partial struct AttachedScriptSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer ecb = SystemAPI.GetSingletonRW<BeginInitializationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (attached, props, entity) in SystemAPI.Query<AttachedScript, DynamicBuffer<PropertyValue>>().WithNone<AttachedCompiledScript>().WithEntityAccess())
            {
                if (!attached.Script.IsCreated)
                    continue;

                var compiledScript = Script.Compile(ref attached.Script.Value);

                for (int i = 0; i < props.Length; ++i)
                {
                    compiledScript.SetProperty(props[i].Name.ToString(), Value.FromEntity(props[i].Value));
                }

                ecb.AddComponent(entity, new AttachedCompiledScript
                {
                    Script = compiledScript,
                });
            }

            foreach (var (script, events, entity) in SystemAPI.Query<AttachedCompiledScript, DynamicBuffer<QueuedScriptEvent>>().WithEntityAccess())
            {
                int numEvents = events.Length;
                if (numEvents > 0)
                {
                    foreach (var evt in events)
                    {
                        script.Script.Execute(evt.Name.ToString(), Value.FromEntity(entity));
                    }

                    events.RemoveRange(0, numEvents); // Instead of clear, execution could have added more events
                }

                script.Script.Execute("on_update", Value.FromEntity(entity));
            }
        }
    }
}