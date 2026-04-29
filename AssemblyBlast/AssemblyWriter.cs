using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AssemblyBlast.Models;

namespace AssemblyBlast;

/// <summary>
///     Mirror of <see cref="AssemblyReader" />. Takes a <see cref="ClassDefinition" />
///     or <see cref="EnumDefinition" /> and produces C# source code, optionally writing
///     it to a folder using a namespace-to-path layout. Closes the loop:
///     <c>Assembly → ClassDefinition → C# source</c>.
/// </summary>
public static class AssemblyWriter
{
    /// <summary>
    ///     Renders a class / record / struct / interface as a complete <c>.cs</c>
    ///     source file with file-scoped namespace, XML-doc summaries, ctor (with
    ///     <c>Property = paramName;</c> assignments derived from a case-insensitive
    ///     match against the property list), and properties with their accessor
    ///     shape (<c>{ get; }</c> / <c>{ get; init; }</c> / <c>{ get; set; }</c>).
    /// </summary>
    public static string WriteClass(ClassDefinition def)
    {
        if (def is null) throw new ArgumentNullException(nameof(def));

        var sb = new StringBuilder();
        var needsCollections = def.Properties.Any(p => p.IsCollection);

        sb.AppendLine("using System;");
        if (needsCollections) sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(def.Namespace))
        {
            sb.Append("namespace ").Append(def.Namespace).AppendLine(";");
            sb.AppendLine();
        }

        EmitSummary(sb, def.Summary, indent: 0);

        sb.Append("public ").Append(def.Kind).Append(' ').Append(def.Name);
        var bases = BuildBaseList(def);
        if (bases.Count > 0)
        {
            sb.Append(" : ").Append(string.Join(", ", bases));
        }
        sb.AppendLine();
        sb.AppendLine("{");

        // Constructor (first one only)
        var ctor = def.Constructors.FirstOrDefault();
        if (ctor is { Parameters: { Count: > 0 } } && def.Kind != "interface")
        {
            sb.Append("    public ").Append(def.Name).Append('(');
            sb.Append(string.Join(", ", ctor.Parameters.Select(p => $"{p.Type} {p.Name}")));
            sb.AppendLine(")");
            sb.AppendLine("    {");

            // Build a case-insensitive lookup of property names so we can resolve
            // ctor-param "line1" → property "Line1" without relying on PascalCase
            // alone (handles "Id" / "id" too).
            var propNames = new HashSet<string>(
                def.Properties.Select(p => p.Name),
                StringComparer.OrdinalIgnoreCase);

            foreach (var p in ctor.Parameters)
            {
                var resolved = def.Properties
                    .FirstOrDefault(prop =>
                        string.Equals(prop.Name, p.Name, StringComparison.OrdinalIgnoreCase))
                    ?.Name
                    ?? Pascal(p.Name);
                sb.Append("        ").Append(resolved).Append(" = ").Append(p.Name).AppendLine(";");
            }

            sb.AppendLine("    }");
            sb.AppendLine();
        }

        // Properties
        for (var i = 0; i < def.Properties.Count; i++)
        {
            var p = def.Properties[i];
            EmitSummary(sb, p.Summary, indent: 4);

            sb.Append("    public ");

            var canEmitRequired = !p.IsDerived
                && p.AccessorType != "get"
                && p.IsRequired;
            if (canEmitRequired) sb.Append("required ");

            var typeText = p.IsCollection ? $"List<{p.Type}>" : p.Type;
            if (p.IsNullable) typeText += "?";
            sb.Append(typeText).Append(' ').Append(p.Name).Append(" { get;");

            switch (p.AccessorType)
            {
                case "set":  sb.Append(" set;"); break;
                case "init": sb.Append(" init;"); break;
                case "get":  /* read-only, no extra accessor */ break;
                default:
                    // Legacy/unspecified — fall back to the derived flag.
                    if (!p.IsDerived) sb.Append(" set;");
                    break;
            }
            sb.AppendLine(" }");

            if (i < def.Properties.Count - 1) sb.AppendLine();
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    ///     Renders an enum as a complete <c>.cs</c> source file with file-scoped
    ///     namespace, optional <c>[Flags]</c>, optional underlying type (omitted
    ///     when <see cref="EnumDefinition.UnderlyingType" /> is <c>int</c>), and
    ///     XML-doc summaries on members when present.
    /// </summary>
    public static string WriteEnum(EnumDefinition def)
    {
        if (def is null) throw new ArgumentNullException(nameof(def));

        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(def.Namespace))
        {
            sb.Append("namespace ").Append(def.Namespace).AppendLine(";");
            sb.AppendLine();
        }

        EmitSummary(sb, def.Summary, indent: 0);

        if (def.IsFlags) sb.AppendLine("[Flags]");

        sb.Append("public enum ").Append(def.Name);
        if (!string.IsNullOrEmpty(def.UnderlyingType) &&
            !string.Equals(def.UnderlyingType, "int", StringComparison.Ordinal))
        {
            sb.Append(" : ").Append(def.UnderlyingType);
        }
        sb.AppendLine();
        sb.AppendLine("{");

        for (var i = 0; i < def.Members.Count; i++)
        {
            var m = def.Members[i];
            EmitSummary(sb, m.Summary, indent: 4);
            sb.Append("    ").Append(m.Name).Append(" = ").Append(m.Value).AppendLine(",");
            if (i < def.Members.Count - 1 && !string.IsNullOrEmpty(def.Members[i + 1].Summary))
                sb.AppendLine();
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    ///     Writes every supplied class and enum to disk under <paramref name="rootPath" />,
    ///     mapping namespaces to folders (<c>Acme.Domain.Crm</c> →
    ///     <c>{root}/Acme/Domain/Crm/</c>) and naming each file <c>{Name}.cs</c>.
    ///     Returns the list of file paths written.
    /// </summary>
    public static IReadOnlyList<string> WriteToFolder(
        IEnumerable<ClassDefinition>? classes,
        IEnumerable<EnumDefinition>? enums,
        string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath)) throw new ArgumentException("rootPath is required.", nameof(rootPath));

        Directory.CreateDirectory(rootPath);
        var written = new List<string>();

        foreach (var c in classes ?? Enumerable.Empty<ClassDefinition>())
        {
            var path = ResolveFilePath(rootPath, c.Namespace, c.Name);
            File.WriteAllText(path, WriteClass(c));
            written.Add(path);
        }
        foreach (var e in enums ?? Enumerable.Empty<EnumDefinition>())
        {
            var path = ResolveFilePath(rootPath, e.Namespace, e.Name);
            File.WriteAllText(path, WriteEnum(e));
            written.Add(path);
        }
        return written;
    }

    private static string ResolveFilePath(string root, string ns, string name)
    {
        var folder = string.IsNullOrEmpty(ns)
            ? root
            : Path.Combine(new[] { root }.Concat(ns.Split('.')).ToArray());
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, $"{name}.cs");
    }

    private static List<string> BuildBaseList(ClassDefinition def)
    {
        var list = new List<string>();
        if (!string.IsNullOrEmpty(def.BaseType)) list.Add(def.BaseType);
        list.AddRange(def.Implements ?? new List<string>());
        return list;
    }

    private static void EmitSummary(StringBuilder sb, string? summary, int indent)
    {
        if (string.IsNullOrWhiteSpace(summary)) return;
        var pad = new string(' ', indent);
        sb.Append(pad).Append("/// <summary>").Append(summary).AppendLine("</summary>");
    }

    private static string Pascal(string s)
    {
        if (string.IsNullOrEmpty(s)) return s ?? string.Empty;
        return char.ToUpperInvariant(s[0]) + s[1..];
    }
}
