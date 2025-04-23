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
            var ctx = new ExecContext
            {
                EntityManager = state.EntityManager,
            };

            var engine = new ScriptEngine();
            engine.SetVar("elapsed_time", Value.FromDouble(SystemAPI.Time.ElapsedTime));

            foreach (var (script, entity) in SystemAPI.Query<AttachedScript>().WithEntityAccess())
            {
                var ast = Script.Compile(ref script.Script.Value);
                engine.SetVar("entity", Value.FromEntity(entity));
                engine.ExecuteFunc(ast, "OnUpdate", ctx);
            }
        }
    }
}