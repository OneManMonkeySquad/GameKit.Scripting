using Unity.Mathematics;

namespace GameKit.Scripting.Runtime
{
    public static class Stdlib
    {
        [Scriptable("print")]
        public static void Print(Value val)
        {
            var str = val.Type switch
            {
                ValueType.Null => "null",
                ValueType.Bool => val.AsBool ? "true" : "false",
                ValueType.Int => val.AsInt.ToString(),
                ValueType.Float => val.AsFloat.ToString(),
                ValueType.Double => val.AsDouble.ToString(),
                ValueType.Entity => val.AsEntity.ToString(),
                ValueType.StringIdx => strings[val.AsInt],
                _ => throw new Exception("Todo ToString"),
            };

            Debug.Log(str);
            if (ILCompiler.Output != null)
            {
                ILCompiler.Output += str;
            }
        }

        [Scriptable("sin")]
        public static Value Sin(Value val)
        {
            return Value.FromDouble(math.sin((double)val));
        }
    }
}