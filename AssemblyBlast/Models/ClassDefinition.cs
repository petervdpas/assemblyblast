using System.Collections.Generic;

namespace AssemblyBlast.Models;

/// <summary>
///     Represents the definition of a class, including its name, interfaces, properties, constructors, and methods.
/// </summary>
public class ClassDefinition
{
    /// <summary>
    ///     Gets or sets the name of the class.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the namespace the type lives in. Populated by
    ///     <see cref="AssemblyReader" />; ignored by the generator path,
    ///     which derives the namespace from the wrapping <c>RootDefinition</c>.
    /// </summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the kind of type (<c>class</c>, <c>record</c>,
    ///     <c>struct</c>, or <c>interface</c>). Defaults to <c>class</c>
    ///     for back-compat with the generator path.
    /// </summary>
    public string Kind { get; set; } = "class";

    /// <summary>
    ///     Gets or sets the unqualified name of the base type (e.g. <c>"BaseEntity"</c>),
    ///     or empty when the type derives directly from <see cref="object" />.
    /// </summary>
    public string BaseType { get; set; } = string.Empty;

    /// <summary>Gets or sets the XML-doc summary, when one is available.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the list of interfaces implemented by the class.
    /// </summary>
    public List<string> Implements { get; set; } = [];

    /// <summary>
    ///     Gets or sets the list of properties defined in the class.
    /// </summary>
    public List<PropertyDefinition> Properties { get; set; } = [];

    /// <summary>
    ///     Gets or sets the list of constructors defined in the class,
    ///     each represented with its parameters and body lines.
    /// </summary>
    public List<ConstructorDefinition> Constructors { get; set; } = [];

    /// <summary>
    ///     Gets or sets the list of methods defined in the class.
    /// </summary>
    public List<MethodDefinition> Methods { get; set; } = [];
}
