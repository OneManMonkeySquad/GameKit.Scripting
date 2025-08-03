using GameKit.Scripting.Internal;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace GameKit.Scripting.Runtime
{
    public static class Stdlib
    {
        [Scriptable("panic")]
        public static void Panic(object str)
        {
            Debug.LogError("PANIC: " + str);
#if UNITY_EDITOR
            Debug.Break();
#else
            Application.Quit();
#endif
        }

        [Scriptable("sin")]
        public static float Sin(float val) => (float)math.sin(val);

        /// <summary>
        /// Call a scripting function the next frame.
        /// </summary>
        [Scriptable("queue_event")]
        public static void QueueEvent(Entity ent, string name)
        {
            var buff = World.DefaultGameObjectInjectionWorld.EntityManager.GetBuffer<ScriptEvent>(ent);
            buff.Add(new ScriptEvent { Name = name });
        }
    }
}