using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssemblyBlast;
using AssemblyBlast.Models;
using Xunit;

namespace AssemblyBlast.Tests;

public class AssemblyWriterTests
{
    [Fact]
    public void WriteClass_RoundTripsFromReader()
    {
        // Read a fixture from this assembly, write it out, and assert the
        // resulting C# contains the expected anchor lines. This proves the
        // round-trip Reader → Writer matches.
        var def = AssemblyReader.ReadClass(typeof(WithCtor));
        var src = AssemblyWriter.WriteClass(def);

        Assert.Contains("namespace AssemblyBlast.Tests;", src);
        Assert.Contains("public class WithCtor", src);
        Assert.Contains("public WithCtor(string Id, int Count)", src);
        Assert.Contains("Id = Id;", src);
        Assert.Contains("Count = Count;", src);
        Assert.Contains("public string Id { get; }", src);
        Assert.Contains("public int Count { get; }", src);
        Assert.Contains("public string DisplayName { get; }", src); // derived
    }

    [Fact]
    public void WriteClass_EmitsInitOnlyAccessorForRecordsAndCtorFedInitProperties()
    {
        var def = new ClassDefinition
        {
            Name = "Customer",
            Namespace = "Acme.Domain",
            Kind = "class",
            Constructors =
            {
                new ConstructorDefinition
                {
                    Parameters =
                    {
                        new ParameterDefinition { Name = "id",   Type = "string"       },
                        new ParameterDefinition { Name = "tier", Type = "CustomerTier" },
                    },
                },
            },
            Properties =
            {
                new PropertyDefinition { Name = "Id",   Type = "string",       AccessorType = "init" },
                new PropertyDefinition { Name = "Tier", Type = "CustomerTier", AccessorType = "init" },
            },
        };

        var src = AssemblyWriter.WriteClass(def);

        Assert.Contains("public Customer(string id, CustomerTier tier)", src);
        Assert.Contains("Id = id;", src);
        Assert.Contains("Tier = tier;", src);
        Assert.Contains("public string Id { get; init; }", src);
        Assert.Contains("public CustomerTier Tier { get; init; }", src);
    }

    [Fact]
    public void WriteClass_PreservesNullabilityAndCollectionShape()
    {
        var def = new ClassDefinition
        {
            Name = "Bag",
            Namespace = "X",
            Kind = "class",
            Properties =
            {
                new PropertyDefinition { Name = "MaybeName", Type = "string", AccessorType = "init", IsNullable = true },
                new PropertyDefinition { Name = "Tags", Type = "string", AccessorType = "init", IsCollection = true },
            },
        };

        var src = AssemblyWriter.WriteClass(def);

        Assert.Contains("public string? MaybeName { get; init; }", src);
        Assert.Contains("public List<string> Tags { get; init; }", src);
        Assert.Contains("using System.Collections.Generic;", src);
    }

    [Fact]
    public void WriteEnum_EmitsFlagsAndUnderlyingTypeWhenNeeded()
    {
        var def = new EnumDefinition
        {
            Name = "AccessRights",
            Namespace = "Acme",
            UnderlyingType = "int",
            IsFlags = true,
            Members =
            {
                new EnumMemberDefinition { Name = "None",  Value = 0 },
                new EnumMemberDefinition { Name = "Read",  Value = 1 },
                new EnumMemberDefinition { Name = "Write", Value = 2 },
            },
        };

        var src = AssemblyWriter.WriteEnum(def);

        Assert.Contains("[Flags]", src);
        Assert.Contains("public enum AccessRights", src);
        Assert.DoesNotContain(": int", src); // int is the default, omit
        Assert.Contains("None = 0,",  src);
        Assert.Contains("Read = 1,",  src);
        Assert.Contains("Write = 2,", src);
    }

    [Fact]
    public void WriteEnum_EmitsExplicitUnderlyingTypeWhenNotInt()
    {
        var def = new EnumDefinition
        {
            Name = "ByteEnum",
            UnderlyingType = "byte",
            Members = { new EnumMemberDefinition { Name = "A", Value = 1 } },
        };

        Assert.Contains(": byte", AssemblyWriter.WriteEnum(def));
    }

    [Fact]
    public void WriteToFolder_CreatesNamespaceFolderLayoutAndReturnsPaths()
    {
        var temp = Path.Combine(Path.GetTempPath(), "AssemblyWriterTests_" + System.Guid.NewGuid().ToString("N"));
        try
        {
            var classes = new[]
            {
                new ClassDefinition { Name = "Foo", Namespace = "Acme.Domain.Crm", Kind = "class" },
                new ClassDefinition { Name = "Bar", Namespace = "Acme.Domain", Kind = "class" },
            };
            var enums = new[]
            {
                new EnumDefinition { Name = "Status", Namespace = "Acme.Domain", UnderlyingType = "int" },
            };

            var paths = AssemblyWriter.WriteToFolder(classes, enums, temp);

            Assert.Equal(3, paths.Count);
            Assert.True(File.Exists(Path.Combine(temp, "Acme", "Domain", "Crm", "Foo.cs")));
            Assert.True(File.Exists(Path.Combine(temp, "Acme", "Domain", "Bar.cs")));
            Assert.True(File.Exists(Path.Combine(temp, "Acme", "Domain", "Status.cs")));
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true);
        }
    }
}
