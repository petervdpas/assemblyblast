using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;
using AssemblyBlast.Interfaces;
using AssemblyBlast.Models;

namespace AssemblyBlast;

/// <summary>
///     Provides functionality to dynamically build a class with properties at runtime via
///     <see cref="System.Reflection.Emit" />. Each generated property is decorated with a
///     <see cref="FieldWithAttributes" /> attribute carrying its UI / storage metadata.
/// </summary>
/// <param name="className">The name of the class to be created dynamically.</param>
public class DynamicClassBuilder(string className) : IDynamicClassBuilder
{
    private readonly List<DynamicPropertyMetadata> _properties = [];

    /// <summary>
    ///     Adds a new property to the dynamic class being constructed.
    /// </summary>
    /// <param name="dynamicProperty">
    ///     The metadata for the property to be added, including its name, type, UI control, and additional attributes.
    /// </param>
    public void AddProperty(DynamicPropertyMetadata dynamicProperty)
    {
        _properties.Add(dynamicProperty);
    }

    /// <summary>
    ///     Builds and returns the dynamic class type with the specified properties.
    /// </summary>
    /// <returns>The <see cref="Type" /> representing the dynamically created class.</returns>
    public Type Build()
    {
        var assemblyName = new AssemblyName("DynamicAssembly");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule("DynamicModule");
        var typeBuilder = moduleBuilder.DefineType(className, TypeAttributes.Public | TypeAttributes.Class);

        foreach (var property in _properties)
        {
            var fieldBuilder = typeBuilder.DefineField($"_{property.Name}", property.Type, FieldAttributes.Private);
            var propertyBuilder =
                typeBuilder.DefineProperty(property.Name, PropertyAttributes.HasDefault, property.Type, null);

            const MethodAttributes getSetAttr =
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;

            var getterMethod =
                typeBuilder.DefineMethod($"get_{property.Name}", getSetAttr, property.Type, Type.EmptyTypes);
            var getterIl = getterMethod.GetILGenerator();
            getterIl.Emit(OpCodes.Ldarg_0);
            getterIl.Emit(OpCodes.Ldfld, fieldBuilder);
            getterIl.Emit(OpCodes.Ret);
            propertyBuilder.SetGetMethod(getterMethod);

            var setterMethod =
                typeBuilder.DefineMethod($"set_{property.Name}", getSetAttr, null, [property.Type]);
            var setterIl = setterMethod.GetILGenerator();
            setterIl.Emit(OpCodes.Ldarg_0);
            setterIl.Emit(OpCodes.Ldarg_1);
            setterIl.Emit(OpCodes.Stfld, fieldBuilder);
            setterIl.Emit(OpCodes.Ret);
            propertyBuilder.SetSetMethod(setterMethod);

            var attributeConstructor = typeof(FieldWithAttributes).GetConstructor([
                typeof(string),
                typeof(string),
                typeof(bool),
                typeof(string),
                typeof(string),
                typeof(bool),
                typeof(bool),
                typeof(string)
            ]) ?? throw new InvalidOperationException("Constructor not found for FieldWithAttributes attribute.");

            var optionsJson = JsonSerializer.Serialize(property.Options ?? []);
            var controlParamsJson =
                JsonSerializer.Serialize(property.ControlParameters ?? new Dictionary<string, object>());
            var dataSetControlsJson =
                JsonSerializer.Serialize(property.DataSetControls ?? new Dictionary<string, object>());

            var attributeBuilder = new CustomAttributeBuilder(
                attributeConstructor,
                [
                    property.ControlType,
                    property.Placeholder,
                    property.IsRequired,
                    optionsJson,
                    controlParamsJson,
                    property.IsKeyField,
                    property.IsDisplayField,
                    dataSetControlsJson
                ]);
            propertyBuilder.SetCustomAttribute(attributeBuilder);
        }

        return typeBuilder.CreateType();
    }
}
