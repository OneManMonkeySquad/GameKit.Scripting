using System;
using System.Runtime.InteropServices;
using Unity.Entities;

namespace GameKit.Scripting.Runtime
{
    public enum ValueTypeIdx : byte { Null, Bool, Int, Float, Double, Entity, StringIdx }

    [Serializable]
    [StructLayout(LayoutKind.Explicit)]
    public struct Value
    {
        public static readonly Value Null = new Value { Type = ValueTypeIdx.Null };

        [FieldOffset(0)]
        public bool AsBool;
        [FieldOffset(0)]
        public int AsInt;
        [FieldOffset(0)]
        public float AsFloat;
        [FieldOffset(0)]
        public double AsDouble;
        [FieldOffset(0)]
        public Entity AsEntity;

        [FieldOffset(8)]
        public ValueTypeIdx Type;

        public bool IsNull => Type == ValueTypeIdx.Null;

        public static Value FromBool(bool b) => new() { Type = ValueTypeIdx.Bool, AsBool = b };
        public static Value FromInt(int i) => new() { Type = ValueTypeIdx.Int, AsInt = i };
        public static Value FromFloat(float f) => new() { Type = ValueTypeIdx.Float, AsFloat = f };
        public static Value FromDouble(double d) => new() { Type = ValueTypeIdx.Double, AsDouble = d };
        public static Value FromEntity(Entity e) => new() { Type = ValueTypeIdx.Entity, AsEntity = e };
        public static Value FromStringIdx(int i) => new() { Type = ValueTypeIdx.StringIdx, AsInt = i };

        public override string ToString() => Type switch
        {
            ValueTypeIdx.Null => "null",
            ValueTypeIdx.Bool => AsBool ? "true" : "false",
            ValueTypeIdx.Int => AsInt.ToString(),
            ValueTypeIdx.Float => AsFloat.ToString(),
            ValueTypeIdx.Double => AsDouble.ToString(),
            ValueTypeIdx.Entity => AsEntity.ToString(),
            ValueTypeIdx.StringIdx => AsInt.ToString(),
            _ => throw new Exception("Todo ToString"),
        };

        public static explicit operator int(Value v) => v.Type switch
        {
            ValueTypeIdx.Int => v.AsInt,
            ValueTypeIdx.Bool => v.AsBool ? 1 : 0,
            _ => throw new Exception($"Invalid cast {v.Type} -> int"),
        };

        public static explicit operator float(Value v) => v.Type switch
        {
            ValueTypeIdx.Int => v.AsInt,
            ValueTypeIdx.Float => v.AsFloat,
            ValueTypeIdx.Double => (float)v.AsDouble, // #todo emit error if outside range
            _ => throw new Exception($"Invalid cast {v.Type} -> float"),
        };

        public static explicit operator double(Value v) => v.Type switch
        {
            ValueTypeIdx.Int => v.AsInt,
            ValueTypeIdx.Float => v.AsFloat,
            ValueTypeIdx.Double => v.AsDouble,
            _ => throw new Exception($"Invalid cast {v.Type} -> double"),
        };

        public static explicit operator bool(Value v) => v.Type switch
        {
            ValueTypeIdx.Int => v.AsInt != 0,
            ValueTypeIdx.Bool => v.AsBool,
            _ => throw new Exception($"Invalid cast {v.Type} -> bool"),
        };

        public static explicit operator Entity(Value v) => v.Type switch
        {
            ValueTypeIdx.Entity => v.AsEntity,
            ValueTypeIdx.Null => Entity.Null,
            _ => throw new Exception($"Invalid cast {v.Type} -> Entity"),
        };
    }
}