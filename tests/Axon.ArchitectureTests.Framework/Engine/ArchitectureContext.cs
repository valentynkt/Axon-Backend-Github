using System.Reflection;
using Axon.ArchitectureTests.Framework.Contracts;

namespace Axon.ArchitectureTests.Framework.Engine;

/// <summary>
/// Provides context information for architecture rule validation.
/// </summary>
public sealed class ArchitectureContext : IArchitectureContext
{
    private readonly IReadOnlyList<Assembly> _assemblies;
    private readonly Lazy<IReadOnlyList<Type>> _types;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<Type>> _typesByAssembly;

    public ArchitectureContext(
        IEnumerable<Assembly> assemblies, 
        IArchitectureConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        ArgumentNullException.ThrowIfNull(configuration);

        _assemblies = assemblies.ToList();
        Configuration = configuration;
        
        _types = new Lazy<IReadOnlyList<Type>>(() => 
            _assemblies.SelectMany(a => a.GetTypes()).ToList());
            
        _typesByAssembly = _assemblies.ToDictionary(
            a => a.GetName().Name ?? "Unknown",
            a => (IReadOnlyList<Type>)a.GetTypes().ToList());
    }

    /// <inheritdoc />
    public IEnumerable<Assembly> Assemblies => _assemblies;

    /// <inheritdoc />
    public IEnumerable<Type> Types => _types.Value;

    /// <inheritdoc />
    public IArchitectureConfiguration Configuration { get; }

    /// <inheritdoc />
    public IEnumerable<Type> GetTypesByAssembly(string assemblyName)
    {
        return _typesByAssembly.TryGetValue(assemblyName, out var types) 
            ? types 
            : Array.Empty<Type>();
    }

    /// <inheritdoc />
    public IEnumerable<Type> GetTypesByNamespace(string namespacePattern)
    {
        return Types.Where(t => 
            t.Namespace?.Contains(namespacePattern, StringComparison.OrdinalIgnoreCase) == true);
    }
}