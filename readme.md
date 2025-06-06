# GameKit.Scripting
Custom scripting language for **Unity DOTS** compiled to .NET CLR code. Functional, simple, dynamically typed. Syntax inspired by Go.

Why? C# ISystems are nice and all but something gets lost between the boilerplate that ECS requires and the long script reload times.
Think Skyrim Papyrus scripts or Unreal blueprints. Quest/spell/item/trigger scripting. Not for code that runs every frame or on more than a few objects.

Why not Lua/JavaScript/...? Compiling directly to the CLR is really elegant - it's fast and it allows interacting with existing C# code without any
hassle. This tool is specifically crafted for DOTS and baking - thus allowing convinient DOTS-specific workflows.

## Features
- **Runtime script reload and instant compile times**
- Console errors point to proper source location
- Easy binding of static C# functions
- Script properties with editor support (f.i. for references to other Entities)

## Quickstart
*Install package from git URL* in the Unity *Package Manager*: *https://github.com/OneManMonkeySquad/GameKit.Scripting.git*

Create *test.script* file in *Assets* with the following content:
```go
func on_init(entity) {
    print("on_init " + entity)
}
```
Add *AttachedScriptAuthoring* component to an authoring GameObject inside a subscene. Set *Script* field to your script. Run game.

## Ideas
- Load scripts from StreamingAssets
- Allow multiple scripts on single Entity
- Assign scripts to objects at runtime... somehow (tags, ids, names, ...)
- Have a special syntax to send messages to other objects
- Build/bake time code execution

## Inspired by
https://craftinginterpreters.com
https://media.gdcvault.com/gdc2017/Presentations/Reed_Dan_NetworkingScriptedWeapons.pdf
https://skookumscript.com