using System;
using System.Threading.Tasks;
using Unity.Mathematics;

namespace GameKit.Scripting.Runtime
{
    public static class Stdlib
    {
        [Scriptable("sin")]
        public static Value Sin(Value val) => Value.FromDouble(math.sin((double)val));

        static async void Test()
        {
            await Test2();
        }

        static async Task<string> Test2()
        {
            return "123";
        }
    }
}