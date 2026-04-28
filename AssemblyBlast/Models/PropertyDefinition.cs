using System.Collections.Generic;

namespace AssemblyBlast.Models;

/// <summary>
///     Represents the definition of a property, including its name, type, accessors, and visibility.
/// </summary>
public class PropertyDefinition
{
    /// <summary>
    ///     Gets or sets the name of the property.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the type of the property.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the accessor type for the property.
    /// </summary>
    /// <remarks>
    ///     Defaults to <c>init</c> if no accessor type is specified.
    /// </remarks>
    public string AccessorType { get; set; } = "init";

    /// <summary>
    ///     Gets or sets the visibility of the property accessor (e.g., <c>public</c>, <c>private</c>, etc.).
    /// </summary>
    public string AccessorVisibility { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets a list of attribute indicators (e.g., "IsKeyField", "IsRequired").
    /// </summary>
    public List<string> Attributes { get; set; } = [];

    /// <summary>
    ///     Gets or sets a value indicating whether this property is the type's primary key.
    ///     Populated by <see cref="AssemblyReader" /> when the property carries a
    ///     <c>[Key]</c> / <c>IsKeyField</c> attribute or is named <c>Id</c>.
    /// </summary>
    public bool IsKey { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether this property is non-nullable
    ///     and has no default, i.e. carries the C# <c>required</c> modifier.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the property type is nullable
    ///     (either <see cref="System.Nullable{T}" /> for value types or NRT-annotated
    ///     for reference types).
    /// </summary>
    public bool IsNullable { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the property is a collection
    ///     (<c>T[]</c>, <c>List&lt;T&gt;</c>, <c>IEnumerable&lt;T&gt;</c>, etc.).
    ///     When true, <see cref="Type" /> is the element type, not the wrapper.
    /// </summary>
    public bool IsCollection { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the property is read-only
    ///     (no public setter and not <c>init</c>-settable). Treated as a
    ///     domain-derived attribute by FCDM-style consumers.
    /// </summary>
    public bool IsDerived { get; set; }

    /// <summary>Gets or sets the XML-doc summary, when one is available.</summary>
    public string Summary { get; set; } = string.Empty;
}
