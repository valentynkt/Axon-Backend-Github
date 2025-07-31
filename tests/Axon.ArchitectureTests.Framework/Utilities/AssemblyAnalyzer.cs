using System.Reflection;

namespace Axon.ArchitectureTests.Framework.Utilities;

/// <summary>
/// Utility for loading and analyzing assemblies for architecture testing.
/// </summary>
public static class AssemblyAnalyzer
{
    /// <summary>
    /// Loads all assemblies from the current solution.
    /// </summary>
    public static IEnumerable<Assembly> LoadSolutionAssemblies()
    {
        var loadedAssemblies = new HashSet<Assembly>();
        var solutionAssemblies = new List<Assembly>();

        // Get all loaded assemblies first
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (IsSolutionAssembly(assembly))
            {
                loadedAssemblies.Add(assembly);
                solutionAssemblies.Add(assembly);
            }
        }

        return solutionAssemblies;
    }

    /// <summary>
    /// Loads assemblies matching specific patterns.
    /// </summary>
    public static IEnumerable<Assembly> LoadAssembliesByPattern(params string[] patterns)
    {
        var assemblies = new List<Assembly>();
        
        foreach (var pattern in patterns)
        {
            var matchingAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => AssemblyNameMatches(a, pattern));
            assemblies.AddRange(matchingAssemblies);
        }

        return assemblies.Distinct();
    }

    /// <summary>
    /// Gets types from assemblies that match specific criteria.
    /// </summary>
    public static IEnumerable<Type> GetTypesWhere(
        IEnumerable<Assembly> assemblies, 
        Func<Type, bool> predicate) 
    {
        return assemblies
            .SelectMany(assembly =>
            {
                try
                {
                    return assembly.GetTypes().Where(predicate);
                }
                catch (ReflectionTypeLoadException ex)
                {
                    // Return only successfully loaded types
                    return ex.Types.Where(t => t != null).Cast<Type>().Where(predicate);
                }
            });
    }

    /// <summary>
    /// Checks if an assembly belongs to the current solution.
    /// </summary>
    private static bool IsSolutionAssembly(Assembly assembly)
    {
        var assemblyName = assembly.GetName().Name;
        
        if (string.IsNullOrEmpty(assemblyName))
            return false;

        // Exclude system assemblies
        if (assemblyName.StartsWith("System", StringComparison.OrdinalIgnoreCase) ||
            assemblyName.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase) ||
            assemblyName.StartsWith("mscorlib", StringComparison.OrdinalIgnoreCase) ||
            assemblyName.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase))
            return false;

        // Include Axon assemblies
        return assemblyName.StartsWith("Axon", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if an assembly name matches a pattern.
    /// </summary>
    private static bool AssemblyNameMatches(Assembly assembly, string pattern)
    {
        var assemblyName = assembly.GetName().Name;
        return assemblyName?.Contains(pattern, StringComparison.OrdinalIgnoreCase) == true;
    }
}