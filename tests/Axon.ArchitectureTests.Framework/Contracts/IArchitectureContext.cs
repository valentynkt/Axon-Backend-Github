using System.Reflection;

namespace Axon.ArchitectureTests.Framework.Contracts;

/// <summary>
/// Provides context information for architecture rule validation.
/// </summary>
public interface IArchitectureContext
{
    /// <summary>
    /// Gets the assemblies to analyze.
    /// </summary>
    IEnumerable<Assembly> Assemblies { get; }

    /// <summary>
    /// Gets the types from all loaded assemblies.
    /// </summary>
    IEnumerable<Type> Types { get; }

    /// <summary>
    /// Gets the configuration for the architecture tests.
    /// </summary>
    IArchitectureConfiguration Configuration { get; }

    /// <summary>
    /// Gets types by assembly.
    /// </summary>
    /// <param name="assemblyName">The assembly name.</param>
    /// <returns>Types from the specified assembly.</returns>
    IEnumerable<Type> GetTypesByAssembly(string assemblyName);

    /// <summary>
    /// Gets types by namespace pattern.
    /// </summary>
    /// <param name="namespacePattern">The namespace pattern.</param>
    /// <returns>Types matching the namespace pattern.</returns>
    IEnumerable<Type> GetTypesByNamespace(string namespacePattern);
}