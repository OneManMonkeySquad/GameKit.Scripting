using System;
using System.Collections.Generic;
using System.Reflection;

namespace GameKit.Scripting.Runtime
{
    public class CompiledScript
    {
        Dictionary<string, Delegate> _functions;

        public CompiledScript(Dictionary<string, Delegate> functions)
        {
            _functions = functions;
        }

        public void CopyStateTo(CompiledScript other)
        {
        }

        public bool HasFunction(string name)
        {
            return _functions.ContainsKey(name);
        }

        public void ExecuteFunction(string name)
        {
            _functions[name].DynamicInvoke();
        }

        public void ExecuteFunction(string name, object arg0)
        {
            _functions[name].DynamicInvoke(arg0);
        }

        public void TryExecuteFunction(string name)
        {
            if (_functions.TryGetValue(name, out var method))
            {
                method.DynamicInvoke();
            }
        }

        public void TryExecuteFunction(string name, object arg0)
        {
            if (_functions.TryGetValue(name, out var method))
            {
                method.DynamicInvoke(arg0);
            }
        }
    }
}