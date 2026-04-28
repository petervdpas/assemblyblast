using System.Collections.Generic;

namespace AssemblyBlast.Models;

/// <summary>
///     Represents the definition of an enum type, including its underlying numeric type,
///     <c>[Flags]</c> attribution, and the list of declared members.
/// </summary>
public class EnumDefinition
{
    /// <summary>Gets or sets the unqualified name of the enum.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the namespace the enum lives in.</summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the underlying numeric type as a C# keyword
    ///     (<c>byte</c>, <c>sbyte</c>, <c>short</c>, <c>ushort</c>,
    ///     <c>int</c>, <c>uint</c>, <c>long</c>, <c>ulong</c>).
    /// </summary>
    public string UnderlyingType { get; set; } = "int";

    /// <summary>Gets or sets a value indicating whether this enum carries <c>[Flags]</c>.</summary>
    public bool IsFlags { get; set; }

    /// <summary>Gets or sets the XML-doc summary, when one is available alongside the assembly.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Gets or sets the list of declared members.</summary>
    public List<EnumMemberDefinition> Members { get; set; } = [];
}
