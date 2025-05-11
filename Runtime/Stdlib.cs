using GameKit.Scripting.Internal;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace GameKit.Scripting.Runtime
{
    public static class Stdlib
    {
        [Scriptable("panic")]
        public static void Panic(Value str)
        {
            Debug.LogError("PANIC: " + Buildin.GetString(str));
#if UNITY_EDITOR
            Debug.Break();
#else
            Application.Quit();
#endif
        }

        [Scriptable("sin")]
        public static Value Sin(Value val) => Value.FromDouble(math.sin((double)val));

        /// <summary>
        /// Call a scripting function the next frame.
        /// </summary>
        [Scriptable("queue_event")]
        public static void QueueEvent(Value ent, Value name)
        {
            var buff = World.DefaultGameObjectInjectionWorld.EntityManager.GetBuffer<ScriptEvent>((Entity)ent);
            buff.Add(new ScriptEvent { Name = Buildin.GetString(name) });
        }
    }
}