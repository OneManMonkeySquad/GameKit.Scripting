using System;
using System.Collections.Generic;
using GameKit.Scripting.Runtime;
using Unity.Entities;
using UnityEngine;

namespace GameKit.Scripting.Internal
{
    public static class Buildin
    {
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