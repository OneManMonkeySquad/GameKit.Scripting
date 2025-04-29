using System;
using System.Collections.Generic;
using GameKit.Scripting.Runtime;
using Unity.Entities;
using UnityEngine;

namespace GameKit.Scripting.Internal
{
    public static class Buildin
    {
        static List<string> strings = new();

        [Scriptable("print")]
        public static void Print(Value val)
        {
            var str = val.Type switch
            {
                ValueTypeIdx.Null => "null",
                ValueTypeIdx.Bool => val.AsBool ? "true" : "false",
                ValueTypeIdx.Int => val.AsInt.ToString(),
                ValueTypeIdx.Float => val.AsFloat.ToString(),
                ValueTypeIdx.Double => val.AsDouble.ToString(),
                ValueTypeIdx.Entity => val.AsEntity.ToString(),
                ValueTypeIdx.StringIdx => strings[val.AsInt],
                _ => throw new Exception("Todo ToString"),
            };

            Debug.Log(str);
            if (ILCompiler.Output != null)
            {
                ILCompiler.Output += str;
            }
        }

        public static Value CreateString(string str)
        {
            var idx = strings.Count;
            strings.Add(str);
            return Value.FromStringIdx(idx);
        }

        public static string GetString(Value str)
        {
            if (str.Type != ValueTypeIdx.StringIdx)
                throw new Exception("Unexpected types for GetString " + str.Type);

            return strings[str.AsInt];
        }

        public static bool ConvertValueToBool(Value val)
        {
            return (bool)val;
        }

        public static Value Negate(Value value)
        {
            return value.Type switch
            {
                ValueTypeIdx.Null => Value.Null,
                ValueTypeIdx.Int => Value.FromInt(-value.AsInt),
                ValueTypeIdx.Float => Value.FromFloat(-value.AsFloat),
                ValueTypeIdx.Double => Value.FromDouble(-value.AsDouble),
                _ => throw new Exception("Unexpected types for Negate " + value.Type),
            };
        }

        public static Value Add(Value left, Value right)
        {
            return (left.Type, right.Type) switch
            {
                (ValueTypeIdx.Int, ValueTypeIdx.Int) => Value.FromInt(left.AsInt + right.AsInt),
                (ValueTypeIdx.Float, ValueTypeIdx.Float) => Value.FromFloat(left.AsFloat + right.AsFloat),
                (ValueTypeIdx.Float, ValueTypeIdx.Double) => Value.FromDouble(left.AsFloat + right.AsDouble),
                (ValueTypeIdx.Double, ValueTypeIdx.Float) => Value.FromDouble(left.AsDouble + right.AsFloat),
                (ValueTypeIdx.StringIdx, ValueTypeIdx.StringIdx) => CreateString(strings[left.AsInt] + strings[right.AsInt]),
                (ValueTypeIdx.StringIdx, ValueTypeIdx.Entity) => CreateString(strings[left.AsInt] + right.AsEntity),
                _ => throw new Exception("Unexpected types for Add " + (left.Type, right.Type)),
            };
        }

        public static Value Mul(Value left, Value right)
        {
            return (left.Type, right.Type) switch
            {
                (ValueTypeIdx.Int, ValueTypeIdx.Int) => Value.FromInt(left.AsInt * right.AsInt),
                (ValueTypeIdx.Double, ValueTypeIdx.Int) => Value.FromDouble(left.AsDouble * right.AsInt),
                (ValueTypeIdx.Double, ValueTypeIdx.Float) => Value.FromDouble(left.AsDouble * right.AsFloat),
                _ => throw new Exception("Unexpected types for Mul " + (left.Type, right.Type)),
            };
        }

        public static Value CmpEq(Value left, Value right)
        {
            return (left.Type, right.Type) switch
            {
                (ValueTypeIdx.Null, ValueTypeIdx.Null) => Value.FromBool(true),
                (ValueTypeIdx.Int, ValueTypeIdx.Int) => Value.FromBool(left.AsInt == right.AsInt),
                (ValueTypeIdx.Entity, ValueTypeIdx.Null) => Value.FromBool(left.AsEntity == Entity.Null),
                _ => throw new Exception("Unexpected types for CmpEq " + (left.Type, right.Type)),
            };
        }

        public static Value CmpNEq(Value left, Value right)
        {
            return (left.Type, right.Type) switch
            {
                (ValueTypeIdx.Null, ValueTypeIdx.Null) => Value.FromBool(false),
                (ValueTypeIdx.Int, ValueTypeIdx.Int) => Value.FromBool(left.AsInt != right.AsInt),
                (ValueTypeIdx.Entity, ValueTypeIdx.Null) => Value.FromBool(left.AsEntity != Entity.Null),
                _ => throw new Exception("Unexpected types for CmpEq " + (left.Type, right.Type)),
            };
        }

        public static Value Greater(Value left, Value right)
        {
            return (left.Type, right.Type) switch
            {
                (ValueTypeIdx.Int, ValueTypeIdx.Int) => Value.FromBool(left.AsInt > right.AsInt),
                _ => throw new Exception("Unexpected types for Greater " + (left.Type, right.Type)),
            };
        }

        public static Value LEqual(Value left, Value right)
        {
            return (left.Type, right.Type) switch
            {
                (ValueTypeIdx.Int, ValueTypeIdx.Int) => Value.FromBool(left.AsInt <= right.AsInt),
                _ => throw new Exception("Unexpected types for LEqual " + (left.Type, right.Type)),
            };
        }

        public static Value And(Value left, Value right)
        {
            return (left.Type, right.Type) switch
            {
                (ValueTypeIdx.Bool, ValueTypeIdx.Bool) => Value.FromBool(left.AsBool && right.AsBool),
                _ => throw new Exception("Unexpected types for and " + (left.Type, right.Type)),
            };
        }
    }
}