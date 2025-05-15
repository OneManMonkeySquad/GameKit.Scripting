using System;
using System.Linq;

namespace GameKit.Scripting.Internal
{
    public static class ScriptingTypeCache
    {
        public static Type ByName(string name)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Reverse())
            {
                var tt = assembly.GetType(name);
                if (tt != null)
                {
                    return tt;
                }
            }

            throw new Exception($"Type '{name}' not found");
        }
    }
}