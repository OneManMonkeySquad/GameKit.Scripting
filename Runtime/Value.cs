using System;
using System.Runtime.InteropServices;
using Unity.Entities;

namespace GameKit.Scripting.Runtime
{
    public enum ValueType : byte { Null, Bool, Int, Float, Double, Entity, StringIdx }

    [StructLayout(LayoutKind.Explicit)]
    public struct Value
    {
        public static readonly Value Null = new Value { Type = ValueType.Null };

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
        public ValueType Type;

        public static Value FromBool(bool b) => new() { Type = ValueType.Bool, AsBool = b };
        public static Value FromInt(int i) => new() { Type = ValueType.Int, AsInt = i };
        public static Value FromFloat(float f) => new() { Type = ValueType.Float, AsFloat = f };
        public static Value FromDouble(double d) => new() { Type = ValueType.Double, AsDouble = d };
        public static Value FromEntity(Entity e) => new() { Type = ValueType.Entity, AsEntity = e };
        public static Value FromStringIdx(int i) => new() { Type = ValueType.StringIdx, AsInt = i };

        public override string ToString() => Type switch
        {
            ValueType.Null => "null",
            ValueType.Bool => AsBool ? "true" : "false",
            ValueType.Int => AsInt.ToString(),
            ValueType.Float => AsFloat.ToString(),
            ValueType.Double => AsDouble.ToString(),
            ValueType.Entity => AsEntity.ToString(),
            ValueType.StringIdx => AsInt.ToString(),
            _ => throw new Exception("Todo ToString"),
        };

        public static explicit operator int(Value v) => v.Type switch
        {
            ValueType.Int => v.AsInt,
            ValueType.Bool => v.AsBool ? 1 : 0,
            _ => throw new Exception("Invalid cast"),
        };

        public static explicit operator float(Value v) => v.Type switch
        {
            ValueType.Int => v.AsInt,
            ValueType.Float => v.AsFloat,
            _ => throw new Exception("Invalid cast"),
        };

        public static explicit operator double(Value v) => v.Type switch
        {
            ValueType.Int => v.AsInt,
            ValueType.Float => v.AsFloat,
            ValueType.Double => v.AsDouble,
            _ => throw new Exception("Invalid cast"),
        };

        public static explicit operator bool(Value v) => v.Type switch
        {
            ValueType.Int => v.AsInt != 0,
            ValueType.Bool => v.AsBool,
            _ => throw new Exception("Invalid cast"),
        };

        public static explicit operator Entity(Value v) => v.Type switch
        {
            ValueType.Entity => v.AsEntity,
            _ => throw new Exception("Invalid cast"),
        };
    }
}