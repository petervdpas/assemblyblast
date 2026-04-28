using System;
using AssemblyBlast.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AssemblyBlast;

/// <summary>
///     Extension methods for registering AssemblyBlast services with a DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Registers AssemblyBlast services:
    ///     <list type="bullet">
    ///         <item><description><see cref="IDynamicClassGenerator" /> as a singleton.</description></item>
    ///         <item>
    ///             <description>
    ///                 <see cref="Func{TIn, TResult}" /> producing an <see cref="IDynamicClassBuilder" /> for a given class
    ///                 name. Use this to create per-class builders without leaking class-name state into a shared singleton.
    ///             </description>
    ///         </item>
    ///     </list>
    /// </summary>
    /// <param name="services">The target <see cref="IServiceCollection" />.</param>
    /// <returns>The same <see cref="IServiceCollection" /> for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddAssemblyBlast();
    ///
    /// // ...elsewhere:
    /// public class MyService(Func&lt;string, IDynamicClassBuilder&gt; builderFor)
    /// {
    ///     public Type BuildPerson() =&gt; builderFor("Person").Build();
    /// }
    /// </code>
    /// </example>
    public static IServiceCollection AddAssemblyBlast(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IDynamicClassGenerator, DynamicClassGenerator>();
        services.TryAddSingleton<Func<string, IDynamicClassBuilder>>(_ => name => new DynamicClassBuilder(name));

        return services;
    }
}
