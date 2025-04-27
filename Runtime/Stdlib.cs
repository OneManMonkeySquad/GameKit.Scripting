using Unity.Mathematics;

namespace GameKit.Scripting.Runtime
{
    public static class Stdlib
    {
        [Scriptable("sin")]
        public static Value Sin(Value val) => Value.FromDouble(math.sin((double)val));
    }
}