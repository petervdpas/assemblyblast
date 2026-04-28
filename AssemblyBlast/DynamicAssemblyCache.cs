using System.Collections.Concurrent;
using System.Reflection;

namespace AssemblyBlast;

/// <summary>
///     Provides a thread-safe caching layer for dynamically loaded assemblies.
///     Loading the same path twice returns the same <see cref="Assembly" /> instance, preserving reference identity.
/// </summary>
public static class DynamicAssemblyCache
{
    private static readonly ConcurrentDictionary<string, Assembly> _cache = new();

    /// <summary>
    ///     Loads an assembly from the specified path, or returns the cached instance if it has already been loaded
    ///     from the same path.
    /// </summary>
    /// <param name="path">The file path of the assembly (.dll) to load.</param>
    /// <returns>
    ///     The loaded <see cref="Assembly" /> instance. Subsequent calls with the same path return the same reference.
    /// </returns>
    public static Assembly LoadOrGet(string path)
    {
        return _cache.GetOrAdd(path, Assembly.LoadFrom);
    }

    /// <summary>
    ///     Removes all entries from the cache. The previously loaded assemblies remain in the host's load context;
    ///     this only clears the lookup map.
    /// </summary>
    public static void Clear()
    {
        _cache.Clear();
    }
}
