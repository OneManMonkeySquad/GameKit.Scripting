using System;
using System.Collections;
using GameKit.Scripting.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace GameKit.Scripting.Internal
{
    public static class Buildin
    {
        public static object ResolveObjectRef(string name)
        {
            {
                var eq = World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntityQuery(typeof(NamedEntity));
                var nea = eq.ToComponentDataArray<NamedEntity>(Allocator.Temp);
                var ents = eq.ToEntityArray(Allocator.Temp);

                Entity result = Entity.Null;
                for (int i = 0; i < nea.Length; ++i)
                {
                    if (nea[i].Name == name)
                    {
                        result = ents[i];
                        break;
                    }
                }

                nea.Dispose();
                ents.Dispose();

                if (result != Entity.Null)
                    return result;
            }

            {
                if (NamedScriptableObjects.Instance != null)
                {
                    foreach (var so in NamedScriptableObjects.Instance.Foo)
                    {
                        if (so.name == name)
                            return so;
                    }
                }
            }

            throw new Exception($"ObjectRef '{name}' no found");
        }

        public static object StartCoroutine(object e)
        {
            NamedScriptableObjects.Instance.StartCoroutine((IEnumerator)e);
            return null;
        }

        public static IEnumerator WaitAll(object[] yieldables)
        {
            var coroutines = new Coroutine[yieldables.Length];
            for (int i = 0; i < coroutines.Length; i++)
            {
                coroutines[i] = NamedScriptableObjects.Instance.StartCoroutine((IEnumerator)yieldables[i]);
            }

            foreach (var coroutine in coroutines)
            {
                yield return coroutine;
            }
        }

        [Scriptable("_wait")]
        public static IEnumerator Wait()
        {
            yield return null;
        }

        [Scriptable("_wait_for_seconds")]
        public static IEnumerator WaitForSeconds(object time)
        {
            Debug.Log("WaitForSeconds " + time);

            yield return new WaitForSeconds((float)time);
        }

        [Scriptable("float3")]
        public static object Float3(object x, object y, object z)
        {
            return new float3((float)x, (float)y, (float)z);
        }

        [Scriptable("frame_number")]
        public static object FrameNumber()
        {
            return Time.frameCount;
        }

        [Scriptable("print")]
        public static object Print(object val)
        {
            var str = val switch
            {
                bool b => b ? "true" : "false",
                int i => i.ToString(),
                float f => f.ToString(),
                double d => d.ToString(),
                Entity e => e.ToString(),
                string s => s,
                null => "null",
                _ => throw new Exception("Todo ToString"),
            };

            Debug.Log(str);
            if (ILCompiler.Output != null)
            {
                ILCompiler.Output += str;
            }

            return str;
        }

        public static bool ConvertValueToBool(object val)
        {
            return (bool)val;
        }

        public static object Negate(object value)
        {
            return value switch
            {
                int i => -i,
                float f => -f,
                double d => -d,
                _ => throw new Exception("Unexpected types for Negate " + value.GetType()),
            };
        }

        public static object Add(object left, object right)
        {
            return (left, right) switch
            {
                (int l, int r) => l + r,
                (int l, string r) => l + r,
                (float l, float r) => l + r,
                (float l, double r) => l + r,
                (double l, float r) => l + r,
                (double l, int r) => l + r,
                (string l, int r) => l + r,
                (string l, string r) => l + r,
                (string l, Entity r) => l + r,
                _ => throw new Exception("Unexpected types for Add " + (left.GetType(), right.GetType())),
            };
        }

        public static object Mul(object left, object right)
        {
            return (left, right) switch
            {
                (int l, int r) => l * r,
                (double l, int r) => l * r,
                (double l, float r) => l * r,
                _ => throw new Exception("Unexpected types for Mul " + (left.GetType(), right.GetType())),
            };
        }

        public static object CmpEq(object left, object right)
        {
            return (left, right) switch
            {
                (null, null) => true,
                (int l, int r) => l == r,
                (Entity l, Entity r) => l == r,
                _ => throw new Exception("Unexpected types for CmpEq " + (left.GetType(), right.GetType())),
            };
        }

        public static object CmpNEq(object left, object right)
        {
            return (left, right) switch
            {
                (null, null) => false,
                (int l, int r) => l != r,
                (Entity l, Entity r) => l != r,
                _ => throw new Exception("Unexpected types for CmpEq " + (left.GetType(), right.GetType())),
            };
        }

        public static object Greater(object left, object right)
        {
            return (left, right) switch
            {
                (int l, int r) => l > r,
                _ => throw new Exception("Unexpected types for Greater " + (left.GetType(), right.GetType())),
            };
        }

        public static object LEqual(object left, object right)
        {
            return (left, right) switch
            {
                (int l, int r) => l <= r,
                _ => throw new Exception("Unexpected types for LEqual " + (left.GetType(), right.GetType())),
            };
        }

        public static object And(object left, object right)
        {
            return (left, right) switch
            {
                (bool l, bool r) => l && r,
                _ => throw new Exception("Unexpected types for and " + (left.GetType(), right.GetType())),
            };
        }
    }
}