using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AssemblyBlast;
using AssemblyBlast.Models;
using Xunit;

namespace AssemblyBlast.Tests;

/// <summary>
///     Exercises <see cref="AssemblyReader" /> against fixture types declared in
///     this file. Each test uses a tightly-scoped fixture so a failure points at
///     one behavioural rule rather than a tangle of them.
/// </summary>
public class AssemblyReaderTests
{
    [Fact]
    public void ReadClass_PopulatesNameNamespaceAndKindForRecord()
    {
        var def = AssemblyReader.ReadClass(typeof(SimpleRecord));

        Assert.Equal("SimpleRecord", def.Name);
        Assert.Equal("AssemblyBlast.Tests", def.Namespace);
        Assert.Equal("record", def.Kind);
    }

    [Fact]
    public void ReadClass_PopulatesKindForClassStructInterface()
    {
        Assert.Equal("class",     AssemblyReader.ReadClass(typeof(SimpleClass)).Kind);
        Assert.Equal("struct",    AssemblyReader.ReadClass(typeof(SimpleStruct)).Kind);
        Assert.Equal("interface", AssemblyReader.ReadClass(typeof(ISimpleIface)).Kind);
    }

    [Fact]
    public void ReadClass_ResolvesBaseTypeAndInterfaces()
    {
        var def = AssemblyReader.ReadClass(typeof(Concrete));

        Assert.Equal("Base", def.BaseType);
        Assert.Contains("ISimpleIface", def.Implements);
    }

    [Fact]
    public void ReadClass_CapturesPublicConstructorsWithParameters()
    {
        var def = AssemblyReader.ReadClass(typeof(WithCtor));

        Assert.Single(def.Constructors);
        var ctor = def.Constructors[0];
        Assert.Equal(2, ctor.Parameters.Count);
        Assert.Equal("Id",   ctor.Parameters[0].Name);
        Assert.Equal("string", ctor.Parameters[0].Type);
        Assert.Equal("int",    ctor.Parameters[1].Type);
    }

    [Fact]
    public void ReadProperty_GetOnlyWithMatchingCtorParam_IsNotDerived()
    {
        // Classic OOP: private field + public ctor + read-only get-only property.
        // The reader should recognise the ctor wires it up and NOT mark it derived.
        var def = AssemblyReader.ReadClass(typeof(WithCtor));

        var idProp = def.Properties.Single(p => p.Name == "Id");
        Assert.False(idProp.IsDerived, "Id is set via ctor → not derived");
        Assert.Equal("get", idProp.AccessorType);
    }

    [Fact]
    public void ReadProperty_TrulyComputed_IsDerived()
    {
        var def = AssemblyReader.ReadClass(typeof(WithCtor));

        var fullName = def.Properties.Single(p => p.Name == "DisplayName");
        Assert.True(fullName.IsDerived, "DisplayName has no matching ctor param → derived");
    }

    [Fact]
    public void ReadProperty_DetectsCollectionAndUnwrapsElementType()
    {
        var def = AssemblyReader.ReadClass(typeof(WithCollections));

        var tags = def.Properties.Single(p => p.Name == "Tags");
        Assert.True(tags.IsCollection);
        Assert.Equal("string", tags.Type);

        var counts = def.Properties.Single(p => p.Name == "Counts");
        Assert.True(counts.IsCollection);
        Assert.Equal("int", counts.Type);
    }

    [Fact]
    public void ReadProperty_DetectsNullableValueAndReferenceTypes()
    {
        var def = AssemblyReader.ReadClass(typeof(WithNullables));

        Assert.True(def.Properties.Single(p => p.Name == "MaybeInt").IsNullable);
        Assert.True(def.Properties.Single(p => p.Name == "MaybeString").IsNullable);
        Assert.False(def.Properties.Single(p => p.Name == "DefiniteString").IsNullable);
    }

    [Fact]
    public void ReadProperty_PrivateSetterIsNotDerived_AndCarriesPrivateVisibility()
    {
        var def = AssemblyReader.ReadClass(typeof(WithPrivateSetter));
        var p = def.Properties.Single(x => x.Name == "Counter");

        Assert.False(p.IsDerived);
        Assert.Equal("set", p.AccessorType);
        Assert.Equal("private", p.AccessorVisibility);
    }

    [Fact]
    public void ReadEnum_ReadsMembersAndDetectsFlags()
    {
        var def = AssemblyReader.ReadEnum(typeof(SampleFlags));

        Assert.Equal("SampleFlags", def.Name);
        Assert.True(def.IsFlags);
        Assert.Equal("int", def.UnderlyingType);
        Assert.Equal(4, def.Members.Count);
        Assert.Equal("Read",  def.Members[1].Name);
        Assert.Equal(1,       def.Members[1].Value);
        Assert.Equal("Admin", def.Members[3].Name);
        Assert.Equal(7,       def.Members[3].Value);
    }

    [Fact]
    public void ReadEnums_ReadsByteUnderlyingType()
    {
        var def = AssemblyReader.ReadEnum(typeof(ByteEnum));
        Assert.Equal("byte", def.UnderlyingType);
        Assert.False(def.IsFlags);
    }

    [Fact]
    public void ReadEnum_ThrowsOnNonEnumType() =>
        Assert.Throws<ArgumentException>(() => AssemblyReader.ReadEnum(typeof(SimpleClass)));

    [Fact]
    public void ReadClasses_FindsTopLevelTypesFromAssembly()
    {
        // The test assembly contains all the fixture types in this file.
        var classes = AssemblyReader.ReadClasses(Assembly.GetExecutingAssembly());

        Assert.Contains(classes, c => c.Name == nameof(SimpleClass));
        Assert.Contains(classes, c => c.Name == nameof(SimpleRecord));
        Assert.Contains(classes, c => c.Name == nameof(WithCtor));
        // Enums must not leak into the class list.
        Assert.DoesNotContain(classes, c => c.Name == nameof(SampleFlags));
    }

    [Fact]
    public void ReadEnums_FindsOnlyEnumsFromAssembly()
    {
        var enums = AssemblyReader.ReadEnums(Assembly.GetExecutingAssembly());

        Assert.Contains(enums, e => e.Name == nameof(SampleFlags));
        Assert.Contains(enums, e => e.Name == nameof(ByteEnum));
        Assert.DoesNotContain(enums, e => e.Name == nameof(SimpleClass));
    }
}

// ── Fixtures ────────────────────────────────────────────────

public sealed record SimpleRecord(string Id, string Name);

public class SimpleClass
{
    public string Name { get; set; } = string.Empty;
}

public struct SimpleStruct
{
    public int Count;
}

public interface ISimpleIface
{
    string Label { get; }
}

public class Base { public string Tag { get; set; } = ""; }

public class Concrete : Base, ISimpleIface
{
    public string Label => Tag;
}

/// <summary>Classic OOP shape: private fields, public ctor, read-only properties.</summary>
public class WithCtor
{
    private readonly string _id;
    private readonly int _count;

    public WithCtor(string Id, int Count)
    {
        _id = Id;
        _count = Count;
    }

    public string Id => _id;
    public int Count => _count;

    /// <summary>Truly computed — no ctor param matches.</summary>
    public string DisplayName => $"{_id}#{_count}";
}

public class WithCollections
{
    public List<string> Tags { get; set; } = [];
    public int[] Counts { get; set; } = [];
}

#nullable enable
public class WithNullables
{
    public int? MaybeInt { get; set; }
    public string? MaybeString { get; set; }
    public string DefiniteString { get; set; } = "";
}
#nullable restore

public class WithPrivateSetter
{
    public int Counter { get; private set; }
    public void Bump() => Counter++;
}

[Flags]
public enum SampleFlags
{
    None = 0,
    Read = 1,
    Write = 2,
    Admin = 7,
}

public enum ByteEnum : byte
{
    A = 1,
    B = 2,
}
