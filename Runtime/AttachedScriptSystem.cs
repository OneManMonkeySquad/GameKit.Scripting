using Unity.Entities;

namespace GameKit.Scripting.Runtime
{
    public class AttachedCompiledScript : IComponentData
    {
        public CompiledScript Script;
        public int CodeHash;
    }

    public partial struct AttachedScriptSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer ecb = SystemAPI.GetSingletonRW<BeginInitializationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (attachedScript, entity) in SystemAPI.Query<AttachedScript>().WithNone<AttachedCompiledScript>().WithEntityAccess())
            {
                if (!attachedScript.Script.IsCreated)
                    continue;

                var compiledScript = Script.Compile(attachedScript.Script.Value.Code.ToString(), attachedScript.Script.Value.FileNameHint.ToString());

                ecb.AddComponent(entity, new AttachedCompiledScript
                {
                    Script = compiledScript,
                    CodeHash = attachedScript.Script.Value.CodeHash,
                });
            }

            foreach (var (attachedScript, attachedCompiledScript) in SystemAPI.Query<AttachedScript, AttachedCompiledScript>())
            {
                if (attachedScript.Script.Value.CodeHash != attachedCompiledScript.CodeHash)
                {
                    // Compile new script
                    var compiledScript = Script.Compile(attachedScript.Script.Value.Code.ToString(), attachedScript.Script.Value.FileNameHint.ToString());

                    // Copy old property values to new instance
                    attachedCompiledScript.Script.CopyStateTo(compiledScript);

                    // Swap the script
                    attachedCompiledScript.Script = compiledScript;
                    attachedCompiledScript.CodeHash = attachedScript.Script.Value.CodeHash;
                }
            }

            foreach (var (script, events, entity) in SystemAPI.Query<AttachedCompiledScript, DynamicBuffer<ScriptEvent>>().WithEntityAccess())
            {
                int numEvents = events.Length;
                if (numEvents > 0)
                {
                    foreach (var evt in events)
                    {
                        script.Script.ExecuteFunction(evt.Name.ToString(), entity);
                    }

                    events.RemoveRange(0, numEvents); // Instead of clear, execution could have added more events
                }
            }
        }
    }
}