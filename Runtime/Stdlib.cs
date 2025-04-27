using GameKit.Scripting.Internal;
using Unity.Entities;
using Unity.Mathematics;

namespace GameKit.Scripting.Runtime
{
    public static class Stdlib
    {
        [Scriptable("sin")]
        public static Value Sin(Value val) => Value.FromDouble(math.sin((double)val));

        /// <summary>
        /// Call a scripting function the next frame.
        /// </summary>
        [Scriptable("queue_event")]
        public static void QueueEvent(Value ent, Value name)
        {
            var buff = World.DefaultGameObjectInjectionWorld.EntityManager.GetBuffer<QueuedScriptEvent>((Entity)ent);
            buff.Add(new QueuedScriptEvent { Name = Buildin.GetString(name) });
        }
    }
}