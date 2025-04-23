using Unity.Entities;

namespace Los.Runtime
{
    public partial struct AttachedScriptSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            var ctx = new ExecContext
            {
                EntityManager = state.EntityManager,
            };

            foreach (var (script, entity) in SystemAPI.Query<AttachedScript>().WithEntityAccess())
            {
                var engine = Script.Compile(ref script.Script.Value);
                engine.SetVar("entity", Value.FromEntity(entity));
                engine.SetVar("elapsed_time", Value.FromDouble(SystemAPI.Time.ElapsedTime));
                engine.ExecuteFunc("OnUpdate", ctx);
            }
        }
    }
}