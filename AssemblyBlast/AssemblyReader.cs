using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using AssemblyBlast.Models;

namespace AssemblyBlast;

/// <summary>
///     Reflects over a loaded <see cref="Assembly" /> and produces the canonical
///     <see cref="ClassDefinition" /> / <see cref="EnumDefinition" /> shapes that
///     <see cref="DynamicClassGenerator" /> consumes on the way back. When an
///     <c>&lt;assembly-name&gt;.xml</c> doc file sits next to the assembly, member
///     summaries are extracted and attached to the <c>Summary</c> properties.
/// </summary>
public static class AssemblyReader
{
    /// <summary>
    ///     Reads every public, non-nested class / record / struct / interface in <paramref name="asm" />.
    ///     Compiler-generated types (display classes, anonymous types) and
    ///     <c>static</c> classes (sealed + abstract holders for extension methods,
    ///     fixture data, etc.) are skipped — they're not domain types.
    /// </summary>
    /// <param name="asm">The assembly to inspect.</param>
    /// <returns>One <see cref="ClassDefinition" /> per discovered type.</returns>
    public static List<ClassDefinition> ReadClasses(Assembly asm)
    {
        if (asm is null) throw new ArgumentNullException(nameof(asm));
        var docs = LoadXmlDocs(asm);

        return SafeExportedTypes(asm)
            .Where(t => !t.IsEnum && !t.IsNested && !IsCompilerGenerated(t) && !IsStatic(t))
            .Select(t => ReadClass(t, docs))
            .ToList();
    }

    /// <summary>
    ///     Reads every public, non-nested enum in <paramref name="asm" />.
    /// </summary>
    /// <param name="asm">The assembly to inspect.</param>
    /// <returns>One <see cref="EnumDefinition" /> per discovered enum.</returns>
    public static List<EnumDefinition> ReadEnums(Assembly asm)
    {
        if (asm is null) throw new ArgumentNullException(nameof(asm));
        var docs = LoadXmlDocs(asm);

        return SafeExportedTypes(asm)
            .Where(t => t.IsEnum && !t.IsNested)
            .Select(t => ReadEnum(t, docs))
            .ToList();
    }

    /// <summary>
    ///     Reads a single class-like type into a <see cref="ClassDefinition" />,
    ///     including base type, implemented interfaces, public constructors,
    ///     and instance properties.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <param name="docs">An XML-doc lookup. Pass <c>null</c> to skip summary enrichment.</param>
    /// <returns>The populated definition.</returns>
    public static ClassDefinition ReadClass(Type type, IReadOnlyDictionary<string, string>? docs = null)
    {
        if (type is null) throw new ArgumentNullException(nameof(type));
        docs ??= EmptyDocs;

        var ctors = type
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .ToList();

        var def = new ClassDefinition
        {
            Name = type.Name,
            Namespace = type.Namespace ?? string.Empty,
            Kind = ClassifyKind(type),
            BaseType = ResolveBaseTypeName(type),
            Summary = docs.TryGetValue($"T:{type.FullName}", out var s) ? s : string.Empty,
            Implements = type
                .GetInterfaces()
                .Where(i => i.DeclaringType is null) // top-level only
                .Where(i => !(IsRecordType(type) && IsSelfIEquatable(type, i)))
                .Select(FormatTypeName)
                .ToList(),
            Constructors = ctors
                .Select(c => new ConstructorDefinition
                {
                    Parameters = c.GetParameters()
                        .Select(p => new ParameterDefinition
                        {
                            Name = p.Name ?? string.Empty,
                            Type = FormatParameterType(p),
                        })
                        .ToList(),
                    // BodyLines stays empty — we can't recover it via reflection.
                })
                .ToList(),
            Properties = type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetIndexParameters().Length == 0) // no indexers
                .Select(p => ReadProperty(p, ctors, docs))
                .ToList(),
        };

        return def;
    }

    /// <summary>
    ///     Reads a single property into a <see cref="PropertyDefinition" />,
    ///     ignoring constructors (which means "set via ctor" can't be inferred —
    ///     get-only properties with a matching ctor param will be flagged as derived).
    ///     Use the overload that takes a constructor list when you have the type
    ///     in hand and want a more accurate read.
    /// </summary>
    /// <param name="prop">The property info.</param>
    /// <param name="docs">An XML-doc lookup. Pass <c>null</c> to skip summary enrichment.</param>
    /// <returns>The populated property definition.</returns>
    public static PropertyDefinition ReadProperty(PropertyInfo prop, IReadOnlyDictionary<string, string>? docs = null) =>
        ReadProperty(prop, [], docs);

    /// <summary>
    ///     Reads a single property into a <see cref="PropertyDefinition" />, using
    ///     the supplied constructor list to distinguish "get-only, set via ctor"
    ///     properties (not derived) from genuine computed/expression-bodied
    ///     properties (derived).
    /// </summary>
    /// <param name="prop">The property info.</param>
    /// <param name="constructors">Public constructors of the declaring type, used to detect ctor-fed properties.</param>
    /// <param name="docs">An XML-doc lookup. Pass <c>null</c> to skip summary enrichment.</param>
    /// <returns>The populated property definition.</returns>
    public static PropertyDefinition ReadProperty(
        PropertyInfo prop,
        IReadOnlyList<ConstructorInfo> constructors,
        IReadOnlyDictionary<string, string>? docs = null)
    {
        if (prop is null) throw new ArgumentNullException(nameof(prop));
        docs ??= EmptyDocs;

        var (elementType, isCollection) = UnwrapCollection(prop.PropertyType);
        var (innerType, isNullable) = UnwrapNullable(elementType);

        var setter = prop.SetMethod;
        var hasAnySetter = setter is not null;
        var hasPublicSetter = setter is { IsPublic: true };
        var isInit = setter is not null && setter.ReturnParameter
            .GetRequiredCustomModifiers()
            .Any(m => m.FullName == "System.Runtime.CompilerServices.IsExternalInit");

        string accessor;
        string accessorVisibility;
        if (!hasAnySetter)
        {
            accessor = "get";
            accessorVisibility = "public";
        }
        else if (isInit)
        {
            accessor = "init";
            accessorVisibility = hasPublicSetter ? "public" : "private";
        }
        else
        {
            accessor = "set";
            accessorVisibility = hasPublicSetter ? "public" : "private";
        }

        var hasKeyAttr = prop.GetCustomAttributes()
            .Any(a => a.GetType().Name is "KeyAttribute" or "IsKeyFieldAttribute");
        var isKey = hasKeyAttr || string.Equals(prop.Name, "Id", StringComparison.Ordinal);
        var isRequired = prop.GetCustomAttributes()
            .Any(a => a.GetType().Name is "RequiredAttribute" or "RequiredMemberAttribute" or "IsRequiredAttribute");

        // A get-only property that matches a ctor parameter (case-insensitive name +
        // assignable type) is set during construction, not computed. Records' positional
        // parameters land here, but so do hand-written `public T Foo { get; } = ctorArg;`
        // patterns and the classic private-field + read-only-property OOP shape.
        var settableViaCtor = constructors.Any(c => c.GetParameters().Any(p =>
            string.Equals(p.Name, prop.Name, StringComparison.OrdinalIgnoreCase) &&
            (p.ParameterType == prop.PropertyType ||
             p.ParameterType.IsAssignableTo(prop.PropertyType))));

        var isDerived = !hasAnySetter && !settableViaCtor;

        var docKey = $"P:{prop.DeclaringType?.FullName}.{prop.Name}";

        return new PropertyDefinition
        {
            Name = prop.Name,
            Type = FormatTypeName(innerType),
            AccessorType = accessor,
            AccessorVisibility = accessorVisibility,
            IsKey = isKey,
            IsRequired = isRequired,
            IsNullable = isNullable || (innerType.IsClass && IsNrtNullable(prop)),
            IsCollection = isCollection,
            IsDerived = isDerived,
            Summary = docs.TryGetValue(docKey, out var s) ? s : string.Empty,
        };
    }

    /// <summary>
    ///     Reads a single enum into an <see cref="EnumDefinition" />, including its
    ///     underlying type, <c>[Flags]</c> attribution, and members with values.
    /// </summary>
    /// <param name="type">The enum type to inspect.</param>
    /// <param name="docs">An XML-doc lookup. Pass <c>null</c> to skip summary enrichment.</param>
    /// <returns>The populated enum definition.</returns>
    public static EnumDefinition ReadEnum(Type type, IReadOnlyDictionary<string, string>? docs = null)
    {
        if (type is null) throw new ArgumentNullException(nameof(type));
        if (!type.IsEnum) throw new ArgumentException("Type is not an enum.", nameof(type));
        docs ??= EmptyDocs;

        var underlying = Enum.GetUnderlyingType(type);

        var def = new EnumDefinition
        {
            Name = type.Name,
            Namespace = type.Namespace ?? string.Empty,
            UnderlyingType = FormatTypeName(underlying),
            IsFlags = type.GetCustomAttribute<FlagsAttribute>() is not null,
            Summary = docs.TryGetValue($"T:{type.FullName}", out var s) ? s : string.Empty,
        };

        foreach (var name in Enum.GetNames(type))
        {
            var raw = Enum.Parse(type, name);
            var asLong = Convert.ToInt64(raw); // truncates ulong > long.MaxValue
            var memberKey = $"F:{type.FullName}.{name}";
            def.Members.Add(new EnumMemberDefinition
            {
                Name = name,
                Value = asLong,
                Summary = docs.TryGetValue(memberKey, out var ms) ? ms : string.Empty,
            });
        }

        return def;
    }

    /// <summary>
    ///     Loads the XML-doc file that ships next to <paramref name="asm" /> (e.g.
    ///     <c>Acme.Domain.xml</c>) and returns a flat map of doc-id to summary text.
    ///     Returns an empty map when the file is missing or unreadable, so callers
    ///     can use the result unconditionally.
    /// </summary>
    /// <param name="asm">The assembly whose sibling doc file should be loaded.</param>
    /// <returns>Doc-id → trimmed summary text.</returns>
    public static IReadOnlyDictionary<string, string> LoadXmlDocs(Assembly asm)
    {
        if (asm is null) return EmptyDocs;
        try
        {
            var location = asm.Location;
            if (string.IsNullOrEmpty(location)) return EmptyDocs;
            var xmlPath = Path.ChangeExtension(location, ".xml");
            if (!File.Exists(xmlPath)) return EmptyDocs;

            var doc = XDocument.Load(xmlPath);
            var members = doc.Root?.Element("members")?.Elements("member") ?? [];
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var m in members)
            {
                var name = m.Attribute("name")?.Value;
                var summary = m.Element("summary")?.Value;
                if (string.IsNullOrEmpty(name) || string.IsNullOrWhiteSpace(summary)) continue;
                map[name] = NormalizeWhitespace(summary);
            }
            return map;
        }
        catch
        {
            return EmptyDocs;
        }
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyDocs =
        new Dictionary<string, string>(0);

    private static IEnumerable<Type> SafeExportedTypes(Assembly asm)
    {
        try
        {
            return asm.GetExportedTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }

    private static bool IsCompilerGenerated(Type t) =>
        t.GetCustomAttributes()
            .Any(a => a.GetType().Name == "CompilerGeneratedAttribute")
        || t.Name.Contains('<');

    // C# `static class` lowers to `[abstract] [sealed]` on the CLR side.
    private static bool IsStatic(Type t) => t.IsClass && t.IsAbstract && t.IsSealed;

    // Records carry a compiler-emitted EqualityContract get-only property.
    private static bool IsRecordType(Type t) =>
        t.GetProperty(
            "EqualityContract",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) is { } ec
        && ec.PropertyType == typeof(Type);

    // Records auto-implement IEquatable<TSelf>. That's a compiler artefact,
    // not a user-authored interface, so it's noise in domain-modelling tools.
    // The same shape on a non-record was hand-written; keep it.
    private static bool IsSelfIEquatable(Type owner, Type iface)
    {
        if (!iface.IsGenericType) return false;
        if (iface.GetGenericTypeDefinition() != typeof(IEquatable<>)) return false;
        return iface.GetGenericArguments()[0] == owner;
    }

    private static string ClassifyKind(Type t)
    {
        if (t.IsInterface) return "interface";
        if (t.IsValueType) return "struct";
        // Records carry a compiler-emitted EqualityContract get-only property.
        var hasEqualityContract = t.GetProperty(
            "EqualityContract",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) is { } ec
            && ec.PropertyType == typeof(Type);
        return hasEqualityContract ? "record" : "class";
    }

    private static string ResolveBaseTypeName(Type t)
    {
        if (t.IsInterface || t.IsValueType) return string.Empty;
        var b = t.BaseType;
        if (b is null || b == typeof(object)) return string.Empty;
        return FormatTypeName(b);
    }

    private static (Type element, bool isCollection) UnwrapCollection(Type t)
    {
        if (t == typeof(string)) return (t, false); // string is IEnumerable<char>, but not a collection
        if (t.IsArray) return (t.GetElementType()!, true);
        if (t.IsGenericType)
        {
            var def = t.GetGenericTypeDefinition();
            var ifaces = new[]
            {
                typeof(IEnumerable<>), typeof(ICollection<>), typeof(IList<>),
                typeof(IReadOnlyCollection<>), typeof(IReadOnlyList<>),
            };
            if (Array.Exists(ifaces, i => i == def))
                return (t.GetGenericArguments()[0], true);

            // Concrete collection types (List<T>, etc.) — pull the IEnumerable<T> arg.
            var ie = t.GetInterfaces()
                .Concat(new[] { t })
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            if (ie is not null) return (ie.GetGenericArguments()[0], true);
        }
        if (typeof(IEnumerable).IsAssignableFrom(t)) return (typeof(object), true);
        return (t, false);
    }

    private static (Type inner, bool wasNullable) UnwrapNullable(Type t)
    {
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>))
            return (t.GetGenericArguments()[0], true);
        return (t, false);
    }

    private static bool IsNrtNullable(PropertyInfo prop)
    {
        // Conservative: trust the NullableContextAttribute / NullableAttribute byte flags.
        // Byte 1 = not-null, 2 = nullable. We only flip on explicit-nullable (2).
        var attr = prop.GetCustomAttributes()
            .FirstOrDefault(a => a.GetType().Name == "NullableAttribute");
        if (attr is null) return false;
        var flagsField = attr.GetType().GetField("NullableFlags");
        if (flagsField?.GetValue(attr) is byte[] flags && flags.Length > 0)
            return flags[0] == 2;
        return false;
    }

    // Format a constructor parameter's type, including nullability annotations:
    // value-type Nullable<T> → "T?", and reference-type NRT-nullable → "T?".
    private static string FormatParameterType(ParameterInfo p)
    {
        var t = p.ParameterType;

        // Value-type nullables (Nullable<int> → "int?")
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>))
            return FormatTypeName(t.GetGenericArguments()[0]) + "?";

        // Reference-type NRT nullables (string? → "string?")
        if (!t.IsValueType && IsNrtNullableParameter(p))
            return FormatTypeName(t) + "?";

        return FormatTypeName(t);
    }

    private static bool IsNrtNullableParameter(ParameterInfo p)
    {
        // Param-level NullableAttribute overrides the surrounding context.
        var paramAttr = p.GetCustomAttributes()
            .FirstOrDefault(a => a.GetType().Name == "NullableAttribute");
        if (paramAttr is not null)
        {
            var flagsField = paramAttr.GetType().GetField("NullableFlags");
            if (flagsField?.GetValue(paramAttr) is byte[] flags && flags.Length > 0)
                return flags[0] == 2;
        }

        // Fall back to NullableContextAttribute on the declaring method,
        // then on the declaring type. Byte 2 = nullable default.
        var ctxAttr = p.Member.GetCustomAttributes()
            .FirstOrDefault(a => a.GetType().Name == "NullableContextAttribute")
            ?? p.Member.DeclaringType?.GetCustomAttributes()
                .FirstOrDefault(a => a.GetType().Name == "NullableContextAttribute");

        if (ctxAttr is not null)
        {
            var flagField = ctxAttr.GetType().GetField("Flag");
            if (flagField?.GetValue(ctxAttr) is byte flag) return flag == 2;
        }

        return false;
    }

    private static string FormatTypeName(Type t)
    {
        if (PrimitiveAlias.TryGetValue(t, out var alias)) return alias;
        if (t.IsGenericType)
        {
            var raw = t.Name;
            var tick = raw.IndexOf('`');
            var stem = tick > 0 ? raw[..tick] : raw;
            var args = string.Join(", ", t.GetGenericArguments().Select(FormatTypeName));
            return $"{stem}<{args}>";
        }
        return t.Name;
    }

    private static readonly Dictionary<Type, string> PrimitiveAlias = new()
    {
        [typeof(byte)] = "byte",
        [typeof(sbyte)] = "sbyte",
        [typeof(short)] = "short",
        [typeof(ushort)] = "ushort",
        [typeof(int)] = "int",
        [typeof(uint)] = "uint",
        [typeof(long)] = "long",
        [typeof(ulong)] = "ulong",
        [typeof(float)] = "float",
        [typeof(double)] = "double",
        [typeof(decimal)] = "decimal",
        [typeof(bool)] = "bool",
        [typeof(string)] = "string",
        [typeof(object)] = "object",
        [typeof(char)] = "char",
    };

    private static string NormalizeWhitespace(string s) =>
        string.Join(' ', s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
