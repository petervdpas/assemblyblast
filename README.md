# AssemblyBlast 🧩

[![NuGet](https://img.shields.io/nuget/v/AssemblyBlast.svg)](https://www.nuget.org/packages/AssemblyBlast)
[![NuGet Downloads](https://img.shields.io/nuget/dt/AssemblyBlast.svg)](https://www.nuget.org/packages/AssemblyBlast)
[![License](https://img.shields.io/github/license/petervdpas/AssemblyBlast.svg)](https://opensource.org/licenses/MIT)

![AssemblyBlast](https://raw.githubusercontent.com/petervdpas/AssemblyBlast/master/assets/icon.png)

**AssemblyBlast** is a sibling in the Blast family of NuGet packages. It works in both directions:

- **Forward** — produce .NET types and assemblies at runtime from JSON definitions or programmatic property metadata, using Roslyn for source-to-IL compilation and `System.Reflection.Emit` for in-process type building.
- **Backward** *(since 1.1)* — read existing assemblies into the same `ClassDefinition` / `EnumDefinition` shapes, so a downstream tool (UI generator, schema exporter, doc tool) can describe a DLL the same way it describes a synthetic type.
- **Round-trip** *(since 1.2)* — render those shapes back to C# source with `AssemblyWriter`, optionally writing a folder tree (`Acme.Domain.Crm` → `Acme/Domain/Crm/`). Closes the loop *Assembly → ClassDefinition → C# source*, so a design surface that owns the `ClassDefinition` shapes can regenerate the project's `.cs` files.

It is the codegen pack split out of the legacy `Mnemonics.CodingTools` package. The runtime entity-store and dynamic EF registry layer that used to live alongside it now lives in **EntityBlast**. AssemblyBlast and EntityBlast are independent and neither depends on the other.

## What it gives you

- **`IDynamicClassBuilder`** — define properties at runtime and produce a fresh `Type` via `Reflection.Emit`. Properties are decorated with `FieldWithAttributes` so a downstream UI or store layer can introspect them.
- **`IDynamicClassGenerator`** — feed in a JSON definition (namespaces, classes, properties, constructors, methods) and get back a compiled `.dll` plus the list of namespaces it contains.
- **`AssemblyReader`** *(1.1+)* — reflect over an existing `Assembly` and produce `ClassDefinition[]` / `EnumDefinition[]`, including base type, interfaces, constructors, ctor-fed property detection, nullability / collection unwrapping, `[Flags]` enums, and XML-doc summaries when the sibling `.xml` is present.
- **`AssemblyWriter`** *(1.2+)* — mirror of the reader. Render any `ClassDefinition` / `EnumDefinition` back to C# source, with file-scoped namespaces, ctor body assignments derived from case-insensitive param/property matching, the right accessor shape per property, `[Flags]` and underlying-type emission for enums, and XML-doc summaries preserved. `WriteToFolder` lays out files under a namespace-as-path tree.
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

## Quick start — `AssemblyReader` *(1.1+)*

```csharp
using AssemblyBlast;

var classes = AssemblyReader.ReadClasses(typeof(MyDomainType).Assembly);
var enums   = AssemblyReader.ReadEnums(typeof(MyDomainType).Assembly);

foreach (var c in classes)
{
    Console.WriteLine($"{c.Kind} {c.Namespace}.{c.Name}");
    foreach (var p in c.Properties)
        Console.WriteLine($"  {p.Type}{(p.IsNullable ? "?" : "")} {p.Name}" +
                          $" [key={p.IsKey} required={p.IsRequired} collection={p.IsCollection} derived={p.IsDerived}]");
}
```

What gets surfaced:

- **Kind**: `class` / `record` / `struct` / `interface`. Static classes (sealed + abstract) and compiler-generated types are skipped.
- **Properties**: instance, public-getter properties. The classic OOP shape — private backing field + public ctor + read-only `public T Foo { get; }` — is recognised: properties whose name matches a public-ctor parameter are flagged as ctor-fed (`IsDerived = false`), while expression-bodied / truly computed properties (no matching ctor param) are flagged `IsDerived = true`. Records' positional parameters fall under the same rule. Private setters carry `AccessorVisibility = "private"` so consumers can tell them apart from public setters and from get-only properties.
- **Auto-implemented record interfaces** (`IEquatable<TSelf>` on records) are filtered — they're compiler artefacts, not user-authored.
- **Constructors**: parameter list captured per public ctor, with nullability annotations preserved (`string?`, `int?`) — both reference-type NRT and value-type `Nullable<T>`. Bodies aren't recovered (would require IL decompilation).
- **Enums**: underlying numeric type as a C# keyword (`byte`, `int`, `ulong`…), `[Flags]` attribution, and members with values normalised to `long`.
- **XML-doc summaries**: if `<assembly-name>.xml` sits next to the `.dll`, summaries on types, properties, and enum members are attached.

## Dependency injection

```csharp
builder.Services.AddAssemblyBlast();
```

Registers `IDynamicClassGenerator` as a singleton and a `Func<string, IDynamicClassBuilder>` factory so callers can create per-class builders. `AssemblyReader` is a static helper, no registration needed.

## License

MIT, see [LICENSE.txt](assets/LICENSE.txt).
