# GameKit.Scripting
Custom scripting language running on CLR for Unity. Functional, simple, dynamically typed. Syntax inspired by Go.

## Features
- Console errors point to proper source location
- Easy binding of static C# functions
- Script properties with editor support (f.i. for references to other Entities)
- Runtime script reload
- Instant compile times

## Quickstart

```cs
var ast = Script.Compile("print(\"Hello World\")");
ast.Execute("main");
```

```cs
var ast = Script.Compile("func my_function() { print(\"Hello World\"); }");
ast.Execute("my_function");
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