# Gamekit.Scripting

## Quickstart

Use LosScriptRunnerAuthoring or:
```cs
    var ctx = new ExecContext
    {
        EntityManager = state.EntityManager,
    };

    var engine = Script.CompileFile(filePath);
    engine.SetVar("entity", Value.FromEntity(entity));
    engine.SetVar("elapsed_time", Value.FromDouble(SystemAPI.Time.ElapsedTime));
    engine.ExecuteFunc(foo.FunctionName.ToString(), ctx);
```