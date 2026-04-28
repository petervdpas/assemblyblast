using System.Linq;
using AssemblyBlast;
using AssemblyBlast.Models;
using Xunit;

namespace AssemblyBlast.Tests;

public class DynamicClassBuilderTests
{
    [Fact]
    public void Build_ProducesTypeWithRequestedName()
    {
        var builder = new DynamicClassBuilder("Person");
        builder.AddProperty(new DynamicPropertyMetadata
        {
            Name = "FirstName",
            TypeName = "System.String",
            ControlType = "TextBox"
        });

        var type = builder.Build();

        Assert.Equal("Person", type.Name);
    }

    [Fact]
    public void Build_AddsPropertyWithMatchingType()
    {
        var builder = new DynamicClassBuilder("User");
        builder.AddProperty(new DynamicPropertyMetadata
        {
            Name = "Age",
            TypeName = "System.Int32",
            ControlType = "Numeric",
            IsRequired = true
        });

        var type = builder.Build();
        var property = type.GetProperty("Age");

        Assert.NotNull(property);
        Assert.Equal(typeof(int), property!.PropertyType);
    }

    [Fact]
    public void Build_AttachesFieldWithAttributesCarryingMetadata()
    {
        var builder = new DynamicClassBuilder("Account");
        builder.AddProperty(new DynamicPropertyMetadata
        {
            Name = "Id",
            TypeName = "System.String",
            ControlType = "Hidden",
            IsKeyField = true,
            IsRequired = true,
            Placeholder = "auto"
        });

        var type = builder.Build();
        var attr = type.GetProperty("Id")!
            .GetCustomAttributes(typeof(FieldWithAttributes), inherit: false)
            .Cast<FieldWithAttributes>()
            .Single();

        Assert.Equal("Hidden", attr.ControlType);
        Assert.True(attr.IsKeyField);
        Assert.True(attr.IsRequired);
        Assert.Equal("auto", attr.Placeholder);
    }

    [Fact]
    public void Build_GetterAndSetterRoundTripValues()
    {
        var builder = new DynamicClassBuilder("Point");
        builder.AddProperty(new DynamicPropertyMetadata
        {
            Name = "X",
            TypeName = "System.Int32",
            ControlType = "Numeric"
        });

        var type = builder.Build();
        var instance = System.Activator.CreateInstance(type)!;
        var prop = type.GetProperty("X")!;

        prop.SetValue(instance, 42);

        Assert.Equal(42, prop.GetValue(instance));
    }
}
