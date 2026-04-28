namespace AssemblyBlast.Models;

/// <summary>
///     Represents a single member of an <see cref="EnumDefinition" />.
/// </summary>
public class EnumMemberDefinition
{
    /// <summary>Gets or sets the member name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the member's numeric value. Stored as <see cref="long" /> so
    ///     every defined underlying type fits without overflow except <c>ulong</c>
    ///     values above <see cref="long.MaxValue" />, which the reader truncates.
    /// </summary>
    public long Value { get; set; }

    /// <summary>Gets or sets the XML-doc summary, when one is available.</summary>
    public string Summary { get; set; } = string.Empty;
}
