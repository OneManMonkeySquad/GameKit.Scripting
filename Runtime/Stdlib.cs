using System.Collections;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

namespace GameKit.Scripting.Runtime
{
    public static class Stdlib
    {
        [Scriptable("sin")]
        public static Value Sin(Value val) => Value.FromDouble(math.sin((double)val));

        static IEnumerator Test()
        {
            Color c = Color.red;
            for (float alpha = 1f; alpha >= 0; alpha -= 0.1f)
            {
                c.a = alpha;
                yield return null;
            }
        }
    }
}