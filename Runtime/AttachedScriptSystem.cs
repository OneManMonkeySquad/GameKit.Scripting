using Unity.Entities;
using UnityEngine;

namespace GameKit.Scripting.Runtime
{
    public partial struct AttachedScriptSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            foreach (var (script, events, entity) in SystemAPI.Query<AttachedScript, DynamicBuffer<QueuedScriptEvent>>().WithEntityAccess())
            {
                if (!script.Script.IsCreated)
                    continue;

                var compiledScript = Script.Compile(ref script.Script.Value);

                compiledScript.Execute("on_update", Value.FromEntity(entity));

                int numEvents = events.Length;
                if (numEvents > 0)
                {
                    foreach (var evt in events)
                    {
                        compiledScript.Execute(evt.Name.ToString(), Value.FromEntity(entity));
                    }

                    events.RemoveRange(0, numEvents); // Instead of clear, execution could have added more events
                }
            }
        }
    }
}