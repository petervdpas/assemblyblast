using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using AssemblyBlast.Interfaces;
using AssemblyBlast.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AssemblyBlast;

/// <summary>
///     Provides functionality to dynamically generate C# classes and compile them into an assembly based on JSON input.
///     This class allows for creating namespaces, classes, properties, constructors, and methods as defined in a
///     structured JSON format.
///     Implements <see cref="IDynamicClassGenerator" />.
/// </summary>
public class DynamicClassGenerator : IDynamicClassGenerator
{
    private readonly ILogger<DynamicClassGenerator> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DynamicClassGenerator" /> class with an optional logger.
    /// </summary>
    /// <param name="logger">
    ///     An optional logger for diagnostic and error logging. If <c>null</c>, a no-op logger is used.
    /// </param>
    public DynamicClassGenerator(ILogger<DynamicClassGenerator>? logger = null)
    {
        _logger = logger ?? NullLogger<DynamicClassGenerator>.Instance;
    }

    /// <summary>
    ///     Generates an assembly from JSON definitions, saving it to the specified output path.
    /// </summary>
    /// <param name="json">
    ///     The JSON string that defines namespaces, classes, and properties for code generation.
    ///     Each entry in the JSON defines a namespace containing one or more classes, with each class specifying properties
    ///     and types.
    /// </param>
    /// <param name="outputDllPath">The file path where the generated DLL will be saved.</param>
    /// <returns>
    ///     A tuple containing:
    ///     <list type="bullet">
    ///         <item><term>AssemblyPath</term> - The path to the generated DLL if successful; otherwise, <c>null</c>.</item>
    ///         <item><term>Namespaces</term> - A list of namespaces defined within the generated assembly.</item>
    ///     </list>
    /// </returns>
    /// <remarks>
    ///     The JSON format must follow a structure that includes an array of namespace definitions. Each namespace can
    ///     contain multiple classes, and each class defines properties with types and optional using statements.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown if the JSON input is improperly formatted or missing essential information.</exception>
    /// <exception cref="IOException">Thrown if the DLL could not be saved to the specified path.</exception>
    public (string? AssemblyPath, List<string> Namespaces) GenerateAssemblyFromJson(string json, string outputDllPath)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("JSON content cannot be null or empty.", nameof(json));

        if (string.IsNullOrWhiteSpace(outputDllPath))
            throw new ArgumentException("Output DLL path cannot be null or empty.", nameof(outputDllPath));

        var rootDefinitions = ParseJsonToRootDefinitions(json);
        if (rootDefinitions.Count == 0)
        {
            _logger.LogWarning("No valid definitions found in the provided JSON.");
            return (null, []);
        }

        var allNamespaces = new List<string>();
        var codeBuilder = new StringBuilder();

        var uniqueUsings = new HashSet<string>(rootDefinitions.SelectMany(rd => rd.Usings));

        codeBuilder.AppendLine(string.Join(Environment.NewLine, uniqueUsings.Select(u => $"using {u};")));
        codeBuilder.AppendLine();

        foreach (var rootDefinition in rootDefinitions)
        {
            var namespaceName = rootDefinition.Namespace;
            allNamespaces.Add(namespaceName);
            codeBuilder.Append(GenerateNamespaceSourceCode(rootDefinition.Classes, namespaceName));
        }

        var sourceCode = codeBuilder.ToString();
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location) && File.Exists(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .ToList();

        references.Add(MetadataReference.CreateFromFile(
            typeof(Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfo).Assembly.Location));

        var projectReferences = AssemblyHelper.GetProjectReferences(references).ToList();
        var utilitiesReferences = AssemblyHelper.LoadUtilitiesReferences(projectReferences).ToList();
        var netStandardReference = AssemblyHelper.GetNetStandardReference();

        var compilation = CSharpCompilation.Create(Path.GetFileNameWithoutExtension(outputDllPath))
            .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithOptimizationLevel(OptimizationLevel.Release)
                .WithNullableContextOptions(NullableContextOptions.Enable))
            .AddReferences(netStandardReference)
            .AddReferences(references)
            .AddReferences(projectReferences)
            .AddReferences(utilitiesReferences)
            .AddSyntaxTrees(syntaxTree);

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        if (result.Success)
        {
            ms.Position = 0;
            var dir = Path.GetDirectoryName(outputDllPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            using (var fs = new FileStream(outputDllPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                ms.CopyTo(fs);
            }

            _logger.LogInformation("Assembly successfully generated at {OutputPath}", outputDllPath);
            return (outputDllPath, allNamespaces);
        }

        foreach (var diagnostic in result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
            _logger.LogError("Compilation Error: {Diagnostic}", diagnostic);

        try { if (File.Exists(outputDllPath)) File.Delete(outputDllPath); } catch { /* best effort */ }

        return (null, []);
    }

    private static string GenerateNamespaceSourceCode(List<ClassDefinition> classDefinitions, string namespaceName)
    {
        var codeBuilder = new StringBuilder();

        codeBuilder.AppendLine($"namespace {namespaceName}");
        codeBuilder.AppendLine("{");

        foreach (var classDef in classDefinitions)
        {
            var interfaces = classDef.Implements is { Count: > 0 }
                ? " : " + string.Join(", ", classDef.Implements)
                : string.Empty;

            codeBuilder.AppendLine($"    public class {classDef.Name}{interfaces}");
            codeBuilder.AppendLine("    {");

            foreach (var property in classDef.Properties) codeBuilder.AppendLine(GenerateProperty(property));

            foreach (var constructor in classDef.Constructors)
                codeBuilder.AppendLine(GenerateConstructor(classDef, constructor));

            foreach (var method in classDef.Methods) codeBuilder.AppendLine(GenerateMethod(method));

            codeBuilder.AppendLine("    }");
        }

        codeBuilder.AppendLine("}");
        return codeBuilder.ToString();
    }

    private static string GenerateProperty(PropertyDefinition property)
    {
        var accessorVisibility = string.IsNullOrEmpty(property.AccessorVisibility)
            ? string.Empty
            : property.AccessorVisibility + " ";
        return
            $"        public {property.Type} {property.Name} {{ get; {accessorVisibility}{property.AccessorType}; }}";
    }

    private static string GenerateConstructor(ClassDefinition classDef, ConstructorDefinition constructor)
    {
        var parameters = string.Join(", ", constructor.Parameters.Select(p => $"{p.Type} {p.Name}"));
        var body = string.Join(Environment.NewLine, constructor.BodyLines.Select(line => $"            {line}"));

        return $@"
            public {classDef.Name}({parameters})
            {{
    {body}
            }}";
    }

    private static string GenerateMethod(MethodDefinition method)
    {
        var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"));
        var body = string.Join(Environment.NewLine, method.BodyLines.Select(line => $"            {line}"));

        return $@"
            public {method.ReturnType} {method.Name}({parameters})
            {{
    {body}
            }}";
    }

    private static List<RootDefinition> ParseJsonToRootDefinitions(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<RootDefinition>>(json) ?? [];
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("The provided JSON is malformed.", nameof(json), ex);
        }
    }
}
