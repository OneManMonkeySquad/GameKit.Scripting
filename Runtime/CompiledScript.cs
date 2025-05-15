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

        public void SetProperty(string name, object value)
        {
            _properties[name].SetValue(null, value);
        }

        public void CopyStateTo(CompiledScript other)
        {
            foreach (var entry in _properties)
            {
                other.SetProperty(entry.Key, entry.Value.GetValue(null));
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

        public void Execute(string name, object arg0)
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

        public void TryExecute(string name, object arg0)
        {
            if (_functions.TryGetValue(name, out var method))
            {
                method.DynamicInvoke(arg0);
            }
        }
    }
}