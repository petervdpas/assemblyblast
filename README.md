# AssemblyBlast

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](assets/LICENSE.txt)

**AssemblyBlast** is a sibling in the Blast family of NuGet packages. It produces .NET types and assemblies at runtime from JSON definitions or programmatic property metadata, using Roslyn for source-to-IL compilation and `System.Reflection.Emit` for in-process type building.

It is the codegen pack split out of the legacy `Mnemonics.CodingTools` package. The runtime entity-store and dynamic EF registry layer that used to live alongside it now lives in **EntityBlast**. AssemblyBlast and EntityBlast are independent and neither depends on the other.

## What it gives you

- **`IDynamicClassBuilder`** — define properties at runtime and produce a fresh `Type` via `Reflection.Emit`. Properties are decorated with `FieldWithAttributes` so a downstream UI or store layer can introspect them.
- **`IDynamicClassGenerator`** — feed in a JSON definition (namespaces, classes, properties, constructors, methods) and get back a compiled `.dll` plus the list of namespaces it contains.
- **`DynamicAssemblyCache`** — load a generated assembly once per path; subsequent loads return the cached `Assembly` so reference identity is preserved.
- **`AssemblyHelper`** — utilities for collecting `MetadataReference`s and probing the current `AppDomain`.

## Install

```bash
dotnet add package AssemblyBlast
```

## Quick start — `IDynamicClassBuilder`

```csharp
var builder = new DynamicClassBuilder("Person");
builder.AddProperty(new DynamicPropertyMetadata
{
    Name = "FirstName",
    TypeName = "System.String",
    ControlType = "TextBox",
    IsRequired = true
});
var personType = builder.Build();
```

## Quick start — `IDynamicClassGenerator`

```csharp
var json = """
[
  {
    "Namespace": "Generated",
    "Classes": [
      {
        "Name": "User",
        "Properties": [
          { "Name": "Id",       "Type": "string" },
          { "Name": "Username", "Type": "string" }
        ]
      }
    ]
  }
]
""";

var generator = new DynamicClassGenerator();
var (dllPath, namespaces) = generator.GenerateAssemblyFromJson(json, "Generated/User.dll");

var assembly = DynamicAssemblyCache.LoadOrGet(dllPath!);
var userType = assembly.GetType("Generated.User");
```

## Dependency injection

```csharp
builder.Services.AddAssemblyBlast();
```

Registers `IDynamicClassGenerator` as a singleton and a `Func<string, IDynamicClassBuilder>` factory so callers can create per-class builders.

## License

MIT, see [LICENSE.txt](assets/LICENSE.txt).
