# Gamekit.Scripting

## Quickstart

```cs
var ctx = new ExecContext
{
    EntityManager = state.EntityManager,
};

var engine = new ScriptEngine();
engine.SetVar("elapsed_time", Value.FromDouble(SystemAPI.Time.ElapsedTime));
engine.SetVar("entity", Value.FromEntity(entity));

var ast = Script.Compile("print(\"Hello World\""));
engine.ExecuteFunc(ast, "MyFunc", ctx);
```