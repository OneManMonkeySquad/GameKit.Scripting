using Unity.Entities;

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
                var ast = Script.Compile(ref script.Script.Value);

                ast.Execute("OnUpdate", Value.FromEntity(entity));
                foreach (var evt in events)
                {
                    ast.Execute(evt.Name.ToString(), Value.FromEntity(entity));
                }
                events.Clear();
            }
        }
    }
}