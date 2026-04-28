using System;
using System.IO;
using AssemblyBlast;
using Xunit;

namespace AssemblyBlast.Tests;

public class DynamicClassGeneratorTests : IDisposable
{
    private readonly string _outputDir;

    public DynamicClassGeneratorTests()
    {
        _outputDir = Path.Combine(Path.GetTempPath(), "AssemblyBlastTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_outputDir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_outputDir)) Directory.Delete(_outputDir, recursive: true); }
        catch { /* best effort */ }
    }

    [Fact]
    public void GenerateAssemblyFromJson_EmitsAssemblyContainingDeclaredType()
    {
        const string json = """
        [
          {
            "Namespace": "Generated.Models",
            "Classes": [
              {
                "Name": "User",
                "Properties": [
                  { "Name": "Id",       "Type": "string", "AccessorType": "set" },
                  { "Name": "Username", "Type": "string", "AccessorType": "set" }
                ]
              }
            ]
          }
        ]
        """;

        var dllPath = Path.Combine(_outputDir, "User.dll");
        var generator = new DynamicClassGenerator();

        var (assemblyPath, namespaces) = generator.GenerateAssemblyFromJson(json, dllPath);

        Assert.NotNull(assemblyPath);
        Assert.True(File.Exists(assemblyPath));
        Assert.Contains("Generated.Models", namespaces);

        var assembly = DynamicAssemblyCache.LoadOrGet(assemblyPath!);
        var userType = assembly.GetType("Generated.Models.User");
        Assert.NotNull(userType);
        Assert.NotNull(userType!.GetProperty("Id"));
        Assert.NotNull(userType.GetProperty("Username"));
    }

    [Fact]
    public void GenerateAssemblyFromJson_RejectsEmptyJson()
    {
        var generator = new DynamicClassGenerator();
        Assert.Throws<ArgumentException>(() => generator.GenerateAssemblyFromJson("", "out.dll"));
    }

    [Fact]
    public void GenerateAssemblyFromJson_RejectsMalformedJson()
    {
        var generator = new DynamicClassGenerator();
        Assert.Throws<ArgumentException>(() => generator.GenerateAssemblyFromJson("not-json", Path.Combine(_outputDir, "x.dll")));
    }

    [Fact]
    public void DynamicAssemblyCache_ReturnsSameInstanceForSamePath()
    {
        const string json = """
        [
          { "Namespace": "Cached", "Classes": [ { "Name": "Item", "Properties": [ { "Name": "Id", "Type": "string", "AccessorType": "set" } ] } ] }
        ]
        """;
        var dllPath = Path.Combine(_outputDir, "Cached.dll");
        var (assemblyPath, _) = new DynamicClassGenerator().GenerateAssemblyFromJson(json, dllPath);
        Assert.NotNull(assemblyPath);

        var first = DynamicAssemblyCache.LoadOrGet(assemblyPath!);
        var second = DynamicAssemblyCache.LoadOrGet(assemblyPath!);

        Assert.Same(first, second);
    }
}
