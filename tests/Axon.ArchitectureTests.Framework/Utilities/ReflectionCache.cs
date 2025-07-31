using System.Collections.Concurrent;
using System.Reflection;

namespace Axon.ArchitectureTests.Framework.Utilities;

/// <summary>
/// Caching utility for expensive reflection operations.
/// </summary>
public sealed class ReflectionCache
{
    private static readonly Lazy<ReflectionCache> _instance = new(() => new ReflectionCache());
    
    private readonly ConcurrentDictionary<Type, TypeInfo> _typeInfoCache = new();
    private readonly ConcurrentDictionary<Type, IReadOnlyList<Type>> _dependencyCache = new();
    private readonly ConcurrentDictionary<Assembly, IReadOnlyList<Type>> _assemblyTypesCache = new();

    /// <summary>
    /// Gets the singleton instance of the reflection cache.
    /// </summary>
    public static ReflectionCache Instance => _instance.Value;

    /// <summary>
    /// Gets cached type information.
    /// </summary>
    public TypeInfo GetTypeInfo(Type type)
    {
        return _typeInfoCache.GetOrAdd(type, t => new TypeInfo(t));
    }

    /// <summary>
    /// Gets cached type dependencies.
    /// </summary>
    public IReadOnlyList<Type> GetTypeDependencies(Type type)
    {
        return _dependencyCache.GetOrAdd(type, AnalyzeTypeDependencies);
    }

    /// <summary>
    /// Gets cached types from an assembly.
    /// </summary>
    public IReadOnlyList<Type> GetAssemblyTypes(Assembly assembly)
    {
        return _assemblyTypesCache.GetOrAdd(assembly, LoadAssemblyTypes);
    }

    /// <summary>
    /// Clears all cached data.
    /// </summary>
    public void ClearCache()
    {
        _typeInfoCache.Clear();
        _dependencyCache.Clear();
        _assemblyTypesCache.Clear();
    }

    private static IReadOnlyList<Type> AnalyzeTypeDependencies(Type type)
    {
        var dependencies = new HashSet<Type>();

        try
        {
            // Base type
            if (type.BaseType != null && !IsSystemType(type.BaseType))
                dependencies.Add(type.BaseType);

            // Interfaces
            foreach (var interfaceType in type.GetInterfaces())
                if (!IsSystemType(interfaceType))
                    dependencies.Add(interfaceType);

            // Generic type arguments
            if (type.IsGenericType)
            {
                foreach (var genericArg in type.GetGenericArguments())
                    if (!IsSystemType(genericArg))
                        dependencies.Add(genericArg);
            }

            // Constructor parameters
            foreach (var constructor in type.GetConstructors())
            {
                foreach (var parameter in constructor.GetParameters())
                    if (!IsSystemType(parameter.ParameterType))
                        dependencies.Add(parameter.ParameterType);
            }

            // Property types
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                if (!IsSystemType(property.PropertyType))
                    dependencies.Add(property.PropertyType);

            // Field types
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (!IsSystemType(field.FieldType))
                    dependencies.Add(field.FieldType);
        }
        catch (Exception)
        {
            // If we can't analyze dependencies, return empty list
        }

        return dependencies.ToList();
    }

    private static IReadOnlyList<Type> LoadAssemblyTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes().ToList();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null).ToList()!;
        }
        catch (Exception)
        {
            return Array.Empty<Type>();
        }
    }

    private static bool IsSystemType(Type type) =>
        type.Namespace?.StartsWith("System", StringComparison.OrdinalIgnoreCase) == true ||
        type.Namespace?.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase) == true;
}

/// <summary>
/// Cached type information.
/// </summary>
public sealed class TypeInfo
{
    public TypeInfo(Type type)
    {
        Type = type;
        IsAbstract = type.IsAbstract;
        IsSealed = type.IsSealed;
        IsInterface = type.IsInterface;
        IsClass = type.IsClass;
        IsGeneric = type.IsGenericType;
        Namespace = type.Namespace ?? string.Empty;
        Name = type.Name;
        FullName = type.FullName ?? type.Name;
    }

    public Type Type { get; }
    public bool IsAbstract { get; }
    public bool IsSealed { get; }
    public bool IsInterface { get; }
    public bool IsClass { get; }
    public bool IsGeneric { get; }
    public string Namespace { get; }
    public string Name { get; }
    public string FullName { get; }
}