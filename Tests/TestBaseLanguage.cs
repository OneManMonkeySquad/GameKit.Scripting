using GameKit.Scripting.Runtime;
using NUnit.Framework;

public class TestBaseLanguage
{
    [Test]
    public void TestPrintHelloWorld()
    {
        Assert.AreEqual("Hello World", Script.Execute("print(\"Hello World\");"));

        Assert.AreEqual("Hello World", Script.Execute("  print(\"Hello World\");  \n  "));

        Assert.AreEqual("S1S2", Script.Execute(
            "print(\"S1\");\n"
            + "print(\"S2\");"));
    }

    [Test]
    public void TestPrintVariable()
    {
        Assert.AreEqual("Hello World", Script.Execute(
            "x = \"Hello World\";\n"
            + "print(x);"));
    }

    [Test]
    public void TestFunc()
    {
        Assert.AreEqual("S1", Script.Execute("derp(); func derp() { print(\"S1\"); }"));

        Assert.AreEqual("S1", Script.ExecuteFunc("func derp() { print(\"S1\"); }", "derp"));
    }

    [Test]
    public void TestFuncReturn()
    {
        Assert.AreEqual("Hello World", Script.Execute(
              "func derp() {\n"
            + "  return \"Hello World\";\n"
            + "}\n"
            + "x = derp();\n"
            + "print(x);"));
    }

    [Test]
    public void TestFuncArgument()
    {
        Assert.AreEqual("Hello World", Script.Execute(
              "func derp(x) {\n"
            + "  print(x);\n"
            + "}\n"
            + "derp(\"Hello World\");"));
    }

    [Test]
    public void TestFuncArguments()
    {
        Assert.AreEqual("Hello World", Script.Execute(
              "func derp(x, y) {\n"
            + "  print(x);\n"
            + "  print(y);\n"
            + "}\n"
            + "derp(\"Hello\", \" World\");"));
    }

    [Test]
    public void TestPrintNumber()
    {
        Assert.AreEqual("42", Script.Execute("print(42);"));
    }

    [Test]
    public void TestAddNumber()
    {
        Assert.AreEqual("42", Script.Execute("print(20 + 22);"));
    }

    [Test]
    public void TestAddNumbers()
    {
        Assert.AreEqual("42", Script.Execute("print(10 + 10 + 22);"));
    }

    [Test]
    public void TestMulNumbers()
    {
        Assert.AreEqual("18", Script.Execute("print(9 * 2);"));
    }

    [Test]
    public void TestAddMulOperatorPrecedence()
    {
        Assert.AreEqual("42", Script.Execute("print(10 * 2 + 22);"));
    }

    [Test]
    public void TestPlusCmpOperatorPrecedence()
    {
        Assert.AreEqual("true", Script.Execute("print(2 + 1 > 2);"));
    }

    [Test]
    public void TestAddStrings()
    {
        Assert.AreEqual("Hello World", Script.Execute("print(\"Hello\" + \" World\");"));
    }

    [Test]
    public void TestIfGr()
    {
        Assert.AreEqual("true", Script.Execute("print(2 > 1); "));
        Assert.AreEqual("false", Script.Execute("print(1 > 2);"));
    }

    [Test]
    public void TestIfEq()
    {
        Assert.AreEqual("true", Script.Execute("print(1 == 1); "));
        Assert.AreEqual("false", Script.Execute("print(1 == 2); "));
    }

    [Test]
    public void TestIfLEq()
    {
        Assert.AreEqual("false", Script.Execute("print(2 <= 1); "));
        Assert.AreEqual("true", Script.Execute("print(1 <= 2);"));
        Assert.AreEqual("true", Script.Execute("print(2 <= 2);"));
    }

    [Test]
    public void TestIfTrue()
    {
        Assert.AreEqual("42", Script.Execute("if true \n{ print(42); }"));
    }

    [Test]
    public void TestIfFalse()
    {
        Assert.AreEqual("", Script.Execute("if false { print(42); }"));
    }

    [Test]
    public void TestIfElse()
    {
        Assert.AreEqual("2", Script.Execute("if false { print(1); } else { print(2); }"));
    }

    [Test]
    public void TestIfChain()
    {
        Assert.AreEqual("2", Script.Execute("if false { print(1); } else { if true { print(2); } else { print(3);  } }"));
    }

    [Test]
    public void TestAnd()
    {
        Assert.AreEqual("42", Script.Execute("if true && true { print(42); }"));
        Assert.AreEqual("", Script.Execute("if true && false { print(42); }"));
        Assert.AreEqual("", Script.Execute("if false && true { print(42); }"));
    }

    [Test]
    public void TestVariableAdd()
    {
        Assert.AreEqual("3", Script.Execute(
              "x = 1;\n"
            + "x = x + 2;\n"
            + "print(x);"));
    }

    [Test]
    public void TestNegateVariable()
    {
        Assert.AreEqual("-1", Script.Execute(
              "x = 1;\n"
            + "print(-x);"));
    }

    [Test]
    public void TestVariableAdd2()
    {
        Assert.AreEqual("4", Script.Execute(
              "x = 1;\n"
            + "x = x + 2 + x;\n"
            + "print(x);"));
    }

    [Test]
    public void TestComment()
    {
        Assert.AreEqual("42", Script.Execute(
              "// Hello\n"
            + "    // Comment ^^\n"
            + "print(42); // Comment"));
    }

    [Test]
    public void TestGrouping()
    {
        Assert.AreEqual("42", Script.Execute("print((5 + 5) * 4 + 2); "));
    }
}
