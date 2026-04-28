using System;
using AssemblyBlast;
using AssemblyBlast.Interfaces;
using AssemblyBlast.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AssemblyBlast.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAssemblyBlast_RegistersGenerator()
    {
        var services = new ServiceCollection();
        services.AddAssemblyBlast();
        var sp = services.BuildServiceProvider();

        var generator = sp.GetService<IDynamicClassGenerator>();

        Assert.NotNull(generator);
        Assert.IsType<DynamicClassGenerator>(generator);
    }

    [Fact]
    public void AddAssemblyBlast_RegistersBuilderFactory()
    {
        var services = new ServiceCollection();
        services.AddAssemblyBlast();
        var sp = services.BuildServiceProvider();

        var factory = sp.GetService<Func<string, IDynamicClassBuilder>>();
        Assert.NotNull(factory);

        var builder = factory!("MyClass");
        builder.AddProperty(new DynamicPropertyMetadata
        {
            Name = "Foo",
            TypeName = "System.String",
            ControlType = "TextBox"
        });
        var type = builder.Build();

        Assert.Equal("MyClass", type.Name);
    }
}
