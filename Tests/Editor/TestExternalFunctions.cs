using System.Collections.Generic;
using System.Reflection;
using GameKit.Scripting.Runtime;
using NUnit.Framework;

public class TestExternalFunctions
{
    public class Class
    { }

    public static void Test(Class cls)
    {
    }

    public static object Test2()
    {
        return new Class();
    }

    [Test]
    public void TestClassUpcast()
    {
        var methods = new Dictionary<string, MethodInfo>
        {
            { "test", typeof(TestExternalFunctions).GetMethod(nameof(Test)) },
            { "test2", typeof(TestExternalFunctions).GetMethod(nameof(Test2)) }
        };

        var result = Script.Parse("test(test2())", "", methods);
        var cs = Script.CompileAst(result, methods);
        cs.Script.ExecuteFunction("main");
    }
}