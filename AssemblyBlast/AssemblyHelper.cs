using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;

namespace AssemblyBlast;

/// <summary>
///     Provides utility methods for resolving <see cref="MetadataReference" /> objects from the current
///     <see cref="AppDomain" /> and for probing already-loaded assemblies.
/// </summary>
public static class AssemblyHelper
{
    /// <summary>
    ///     Returns metadata references for assemblies referenced by AssemblyBlast itself, excluding any already
    ///     present in <paramref name="existingReferences" />.
    /// </summary>
    /// <param name="existingReferences">
    ///     An optional collection of <see cref="MetadataReference" /> objects to exclude.
    ///     If omitted, all referenced assemblies are returned.
    /// </param>
    /// <returns>
    ///     A sequence of <see cref="MetadataReference" /> objects for each non-dynamic referenced assembly that is
    ///     not already in <paramref name="existingReferences" />.
    /// </returns>
    public static IEnumerable<MetadataReference> GetProjectReferences(
        IEnumerable<MetadataReference>? existingReferences = null)
    {
        return BuildReferences(existingReferences);
    }

    /// <summary>
    ///     Returns metadata references for assemblies referenced by AssemblyBlast itself, excluding any already
    ///     present in <paramref name="existingReferences" />.
    /// </summary>
    /// <param name="existingReferences">
    ///     A collection of <see cref="MetadataReference" /> objects representing already-resolved references to exclude.
    /// </param>
    /// <returns>
    ///     A sequence of <see cref="MetadataReference" /> objects for each successfully loaded referenced assembly.
    /// </returns>
    public static IEnumerable<MetadataReference> LoadUtilitiesReferences(
        IEnumerable<MetadataReference>? existingReferences = null)
    {
        return BuildReferences(existingReferences);
    }

    private static IEnumerable<MetadataReference> BuildReferences(IEnumerable<MetadataReference>? existingReferences)
    {
        var utilitiesAssembly = typeof(AssemblyHelper).Assembly;
        var referencedAssemblies = utilitiesAssembly.GetReferencedAssemblies();

        var loadedAssemblyNames = existingReferences?
            .Where(r => !string.IsNullOrEmpty(r.Display))
            .Select(r => AssemblyName.GetAssemblyName(r.Display!).Name)
            .ToHashSet() ?? [];

        return referencedAssemblies
            .Where(a => !loadedAssemblyNames.Contains(a.Name))
            .Select(Assembly.Load)
            .Where(a => !a.IsDynamic)
            .Select(a => MetadataReference.CreateFromFile(a.Location));
    }

    /// <summary>
    ///     Retrieves a metadata reference for the <c>netstandard.dll</c> assembly, required for projects targeting
    ///     .NET Standard.
    /// </summary>
    /// <returns>A <see cref="MetadataReference" /> for <c>netstandard.dll</c>.</returns>
    /// <exception cref="FileNotFoundException">
    ///     Thrown if <c>netstandard.dll</c> is not found in the runtime directory.
    /// </exception>
    public static MetadataReference GetNetStandardReference()
    {
        var netstandardPath = Path.Combine(
            RuntimeEnvironment.GetRuntimeDirectory(),
            "netstandard.dll");

        return File.Exists(netstandardPath)
            ? MetadataReference.CreateFromFile(netstandardPath)
            : throw new FileNotFoundException("Could not find netstandard.dll in the runtime directory.");
    }

    /// <summary>
    ///     Determines whether an assembly with the specified name (and optional version and public key token) is
    ///     already loaded in the current <see cref="AppDomain" />.
    /// </summary>
    /// <param name="assemblyName">The simple assembly name (e.g., "System.Text.Json").</param>
    /// <param name="version">
    ///     Optional. If supplied, the loaded assembly must match this version exactly.
    /// </param>
    /// <param name="publicKeyToken">
    ///     Optional. If supplied, the loaded assembly must match this public key token (compared as a lowercase hex string).
    /// </param>
    /// <returns>
    ///     <c>true</c> if a matching assembly is already loaded; otherwise <c>false</c>.
    /// </returns>
    public static bool IsAssemblyLoaded(string? assemblyName, Version? version = null, string? publicKeyToken = null)
    {
        var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
        return loadedAssemblies.Any(loadedAssembly =>
        {
            var loadedName = loadedAssembly.GetName();
            if (!string.Equals(loadedName.Name, assemblyName, StringComparison.OrdinalIgnoreCase))
                return false;

            if (version != null && loadedName.Version != version)
                return false;

            if (string.IsNullOrEmpty(publicKeyToken)) return true;

            var tokenBytes = loadedName.GetPublicKeyToken();
            var loadedToken = tokenBytes != null
                ? BitConverter.ToString(tokenBytes).Replace("-", "").ToLowerInvariant()
                : null;

            return string.Equals(loadedToken, publicKeyToken, StringComparison.OrdinalIgnoreCase);
        });
    }

    /// <summary>
    ///     Returns the file path of an already-loaded assembly with the given name, if any.
    /// </summary>
    /// <param name="assemblyName">The simple assembly name to look up.</param>
    /// <returns>The assembly's location, or <c>null</c> if no such assembly is loaded.</returns>
    public static string? GetLoadedAssemblyLocation(string assemblyName)
    {
        var assembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => string.Equals(a.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase));

        return assembly?.Location;
    }
}
