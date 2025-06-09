using System.Collections;
using GameKit.Scripting.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class TestCoroutines
{
    [UnityTest]
    public IEnumerator TestBranch()
    {
        Script.Execute(""
        + "print(\"start\")\n"
        + "branch { _foo(); }\n"
        + "func _foo() {\n"
        + "    _wait_for_seconds(0.3)\n"
        + "    print(\"coro\")\n"
        + "}\n");

        Assert.AreEqual("start", GameKit.Scripting.Internal.Buildin.Output);
        yield return new WaitForSeconds(0.4f);
        Assert.AreEqual("startcoro", GameKit.Scripting.Internal.Buildin.Output);
    }

    [UnityTest]
    public IEnumerator TestSync()
    {
        Script.Execute(""
        + "branch { _foo(); }\n"
        + "func _foo() {\n"
        + "    print(\"start\")\n"
        + "    sync {\n"
        + "        _print_after_delay()\n"
        + "        _print_after_delay()\n"
        + "    }\n"
        + "}\n"
        + "func _print_after_delay() {"
        + "    _wait_for_seconds(0.3)\n"
        + "    print(\"coro\")\n"
        + "}\n");

        Assert.AreEqual("start", GameKit.Scripting.Internal.Buildin.Output);
        yield return new WaitForSeconds(0.4f);
        Assert.AreEqual("startcorocoro", GameKit.Scripting.Internal.Buildin.Output);
    }
}
