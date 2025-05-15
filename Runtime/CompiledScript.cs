using System;
using System.Collections.Generic;
using System.Reflection;
using GameKit.Scripting.Internal;
using Unity.Entities;

namespace GameKit.Scripting.Runtime
{
    public class CompiledScript
    {
        Dictionary<string, Delegate> _functions;
        Dictionary<string, FieldInfo> _properties;

        public CompiledScript(Dictionary<string, Delegate> functions, Dictionary<string, FieldInfo> properties)
        {
            _functions = functions;
            _properties = properties;
        }

        public void SetProperty(string name, Value value)
        {
            switch (value.Type)
            {
                case ValueTypeIdx.Null:
                    _properties[name].SetValue(null, null);
                    break;
                case ValueTypeIdx.Bool:
                    _properties[name].SetValue(null, (bool)value);
                    break;
                case ValueTypeIdx.Int:
                    _properties[name].SetValue(null, (int)value);
                    break;
                case ValueTypeIdx.Float:
                    _properties[name].SetValue(null, (float)value);
                    break;
                case ValueTypeIdx.Double:
                    _properties[name].SetValue(null, (double)value);
                    break;
                case ValueTypeIdx.Entity:
                    _properties[name].SetValue(null, (Entity)value);
                    break;
                case ValueTypeIdx.StringIdx:
                    _properties[name].SetValue(null, Buildin.GetString(value));
                    break;
                default:
                    throw new Exception("case missing " + value.Type);
            }


        }

        public void CopyStateTo(CompiledScript other)
        {
            foreach (var entry in _properties)
            {
                other.SetProperty(entry.Key, (Value)entry.Value.GetValue(null));
            }
        }

        public bool HasFunction(string name)
        {
            return _functions.ContainsKey(name);
        }

        public void Execute(string name)
        {
            _functions[name].DynamicInvoke();
        }

        public void Execute(string name, Value arg0)
        {
            _functions[name].DynamicInvoke(arg0);
        }

        public void TryExecute(string name)
        {
            if (_functions.TryGetValue(name, out var method))
            {
                method.DynamicInvoke();
            }
        }

        public void TryExecute(string name, Value arg0)
        {
            if (_functions.TryGetValue(name, out var method))
            {
                method.DynamicInvoke(arg0);
            }
        }
    }
}