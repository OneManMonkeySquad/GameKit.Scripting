# GameKit.Scripting
Custom scripting language running on CLR for Unity.

## Quickstart

```cs
var ast = Script.Compile("print(\"Hello World\")");
ast.Execute("main");
```

```cs
public static class Stdlib
{
    [Scriptable("sin")]
    public static Value Sin(Value val) => Value.FromDouble(math.sin((double)val));
}

var ast = Script.Compile("print(sin(42))");
ast.Execute("main");
```