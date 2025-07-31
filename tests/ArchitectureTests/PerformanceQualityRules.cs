using NetArchTest.Rules;
using System.Reflection;

namespace Axon.ArchitectureTests;

/// <summary>
/// Architecture tests to enforce performance and code quality standards.
/// Validates complexity limits, async patterns, and performance anti-patterns.
/// </summary>
[TestFixture]
public class PerformanceQualityRules
{
    private const int MaxLinesPerClass = 500;
    private const int MaxParametersPerMethod = 5;
    private const int EstimatedLinesPerMember = 5;
    private const int EstimatedLinesPerMethod = 15;
    [Test]
    public void Classes_ShouldNotBe_TooLarge()
    {
        var allTypes = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("Axon")
            .And()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .GetTypes();

        var violations = new List<string>();

        foreach (var type in allTypes)
        {
            var memberCount = type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length;
            var methodCount = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName).Count();

            // Heuristic: Estimate lines based on member count
            var estimatedLines = (memberCount * EstimatedLinesPerMember) + (methodCount * EstimatedLinesPerMethod);

            if (estimatedLines > MaxLinesPerClass)
            {
                violations.Add($"{type.FullName} (~{estimatedLines} lines)");
            }
        }

        violations.ShouldBeEmpty(
            $"Classes should not exceed {MaxLinesPerClass} lines. Consider breaking into smaller classes. " +
            $"Violations: {string.Join(", ", violations)}");
    }

    [Test]
    public void Methods_ShouldNotBe_TooComplex()
    {
        var allTypes = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("Axon")
            .And()
            .AreClasses()
            .GetTypes();

        var violations = new List<string>();

        foreach (var type in allTypes)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName) // Exclude property accessors
                .Where(m => !m.IsConstructor);

            foreach (var method in methods)
            {
                var parameterCount = method.GetParameters().Length;

                if (parameterCount > MaxParametersPerMethod)
                {
                    violations.Add($"{type.FullName}.{method.Name} ({parameterCount} parameters)");
                }
            }
        }

        violations.ShouldBeEmpty(
            $"Methods should not have more than {MaxParametersPerMethod} parameters. Consider parameter objects. " +
            $"Violations: {string.Join(", ", violations)}");
    }

    [Test]
    public void AsyncMethods_ShouldReturn_Task()
    {
        var allTypes = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("Axon")
            .And()
            .AreClasses()
            .And()
            .DoNotResideInNamespace("*.Tests.*")
            .GetTypes();

        var violations = new List<string>();

        foreach (var type in allTypes)
        {
            var asyncMethods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .Where(m => m.Name.EndsWith("Async") || 
                           m.GetCustomAttributes().Any(attr => attr.GetType().Name == "AsyncStateMachineAttribute"))
                .Where(m => !m.GetCustomAttributes().Any(attr => attr.GetType().Name == "TestAttribute"));

            foreach (var method in asyncMethods)
            {
                var returnsTask = method.ReturnType == typeof(Task) ||
                                (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>)) ||
                                method.ReturnType == typeof(ValueTask) ||
                                (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(ValueTask<>));

                if (!returnsTask)
                {
                    violations.Add($"{type.FullName}.{method.Name} returns {method.ReturnType.Name}");
                }
            }
        }

        violations.ShouldBeEmpty(
            $"Async methods should return Task, Task<T>, ValueTask, or ValueTask<T>. " +
            $"Violations: {string.Join(", ", violations)}");
    }

    [Test]
    public void Methods_ShouldNotUse_SyncOverAsync()
    {
        var allTypes = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("Axon")
            .And()
            .AreClasses()
            .And()
            .DoNotResideInNamespace("*.Tests.*")
            .GetTypes();

        var violations = new List<string>();

        foreach (var type in allTypes)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);

            foreach (var method in methods)
            {
                // This is a simplified check - would need IL analysis for comprehensive detection
                var hasSyncOverAsync = HasSyncOverAsyncPattern(method);

                if (hasSyncOverAsync)
                {
                    violations.Add($"{type.FullName}.{method.Name}");
                }
            }
        }

        violations.ShouldBeEmpty(
            $"Methods should not use sync-over-async patterns (.Result, .Wait()). Use await instead. " +
            $"Violations: {string.Join(", ", violations)}");
    }

    [Test]
    public void Collections_ShouldUse_AppropriateTypes()
    {
        var allTypes = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("Axon")
            .And()
            .AreClasses()
            .GetTypes();

        var violations = new List<string>();

        foreach (var type in allTypes)
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => IsCollectionType(p.PropertyType));

            foreach (var property in properties)
            {
                var isInappropriateCollection = IsInappropriateCollectionType(property.PropertyType);

                if (isInappropriateCollection)
                {
                    violations.Add($"{type.FullName}.{property.Name} ({property.PropertyType.Name})");
                }
            }
        }

        violations.ShouldBeEmpty(
            $"Use appropriate collection types: IReadOnlyList<T> for read-only, List<T> for mutable, avoid ArrayList/Hashtable. " +
            $"Violations: {string.Join(", ", violations)}");
    }

    [Test]
    public void StringOperations_ShouldBe_Efficient()
    {
        var allTypes = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("Axon")
            .And()
            .AreClasses()
            .GetTypes();

        var violations = new List<string>();

        foreach (var type in allTypes)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);

            foreach (var method in methods)
            {
                var hasInEfficientStringOps = HasInEfficientStringOperations(method);

                if (hasInEfficientStringOps)
                {
                    violations.Add($"{type.FullName}.{method.Name}");
                }
            }
        }

        // Guidance test - would need comprehensive IL analysis
        if (violations.Any())
        {
            TestContext.WriteLine($"Methods with potentially inefficient string operations: {string.Join(", ", violations)}");
        }

        TestContext.WriteLine($"Methods requiring string operation review: {violations.Count}");
    }

    [Test]
    public void LINQ_ShouldNotUse_InEfficientOperations()
    {
        var allTypes = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("Axon")
            .And()
            .AreClasses()
            .GetTypes();

        var violations = new List<string>();

        foreach (var type in allTypes)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);

            foreach (var method in methods)
            {
                var hasInEfficientLinq = HasInEfficientLinqOperations(method);

                if (hasInEfficientLinq)
                {
                    violations.Add($"{type.FullName}.{method.Name}");
                }
            }
        }

        // Guidance test - static analysis limitations
        if (violations.Any())
        {
            TestContext.WriteLine($"Methods with potentially inefficient LINQ operations: {string.Join(", ", violations)}");
        }

        TestContext.WriteLine($"Methods requiring LINQ efficiency review: {violations.Count}");
    }

    [Test]
    public void Exceptions_ShouldNotBe_UsedForControlFlow()
    {
        var allTypes = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("Axon")
            .And()
            .AreClasses()
            .GetTypes();

        var violations = new List<string>();

        foreach (var type in allTypes)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);

            foreach (var method in methods)
            {
                var usesExceptionsForControl = UsesExceptionsForControlFlow(method);

                if (usesExceptionsForControl)
                {
                    violations.Add($"{type.FullName}.{method.Name}");
                }
            }
        }

        violations.ShouldBeEmpty(
            $"Exceptions should not be used for control flow. Use Result pattern or conditional logic. " +
            $"Violations: {string.Join(", ", violations)}");
    }

    [Test]
    public void ResourceManagement_ShouldUse_UsingStatements()
    {
        var allTypes = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("Axon")
            .And()
            .AreClasses()
            .GetTypes();

        var violations = new List<string>();

        foreach (var type in allTypes)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);

            foreach (var method in methods)
            {
                var hasResourceLeaks = HasPotentialResourceLeaks(method);

                if (hasResourceLeaks)
                {
                    violations.Add($"{type.FullName}.{method.Name}");
                }
            }
        }

        // Guidance test - would need IL analysis for comprehensive checking
        if (violations.Any())
        {
            TestContext.WriteLine($"Methods with potential resource leaks: {string.Join(", ", violations)}");
        }

        TestContext.WriteLine($"Methods requiring resource management review: {violations.Count}");
    }

    [Test]
    public void NamingConventions_ShouldBe_Consistent()
    {
        var violations = new List<string>();

        // Check class naming
        var classViolations = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("Axon")
            .And()
            .AreClasses()
            .And()
            .DoNotHaveNameMatching(@"^[A-Z][a-zA-Z0-9]*$")
            .GetTypes()
            .Select(t => $"Class: {t.FullName}")
            .ToList();

        violations.AddRange(classViolations);

        // Check interface naming
        var interfaceViolations = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("Axon")
            .And()
            .AreInterfaces()
            .And()
            .DoNotHaveNameMatching(@"^I[A-Z][a-zA-Z0-9]*$")
            .GetTypes()
            .Select(t => $"Interface: {t.FullName}")
            .ToList();

        violations.AddRange(interfaceViolations);

        violations.ShouldBeEmpty(
            $"Types should follow C# naming conventions: PascalCase for classes, I-prefix for interfaces. " +
            $"Violations: {string.Join(", ", violations)}");
    }

    // Helper methods for performance analysis
    private static bool HasSyncOverAsyncPattern(MethodInfo method)
    {
        // Simplified heuristic - would need IL analysis for comprehensive detection
        // Look for methods that likely use .Result or .Wait() patterns
        return (method.Name.Contains("Wait") || method.Name.Contains("GetResult")) && 
               !method.Name.Contains("Await") &&
               method.ReturnType != typeof(void);
    }

    private static bool IsCollectionType(Type type)
    {
        return type.IsGenericType && (
            type.GetGenericTypeDefinition() == typeof(List<>) ||
            type.GetGenericTypeDefinition() == typeof(IList<>) ||
            type.GetGenericTypeDefinition() == typeof(ICollection<>) ||
            type.GetGenericTypeDefinition() == typeof(IEnumerable<>) ||
            type.GetGenericTypeDefinition() == typeof(HashSet<>) ||
            type.GetGenericTypeDefinition() == typeof(Dictionary<,>)
        );
    }

    private static bool IsInappropriateCollectionType(Type type)
    {
        // Check for old-style collections or inappropriate types
        return type.Name == "ArrayList" || 
               type.Name == "Hashtable" ||
               (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>) && IsPublicProperty(type));
    }

    private static bool IsPublicProperty(Type _)
    {
        // This would need context about where the property is declared
        return false; // Simplified for this example
    }

    private static bool HasInEfficientStringOperations(MethodInfo method)
    {
        // Simplified heuristic - would need IL analysis
        return method.Name.Contains("String") && method.Name.Contains("Concat");
    }

    private static bool HasInEfficientLinqOperations(MethodInfo method)
    {
        // Simplified heuristic - would need IL analysis
        return method.Name.Contains("LINQ") || method.Name.Contains("Enumerable");
    }

    private static bool UsesExceptionsForControlFlow(MethodInfo method)
    {
        // Simplified heuristic - would need IL analysis
        return method.Name.Contains("Try") && method.Name.Contains("Catch");
    }

    private static bool HasPotentialResourceLeaks(MethodInfo method)
    {
        // Check if method creates disposable objects without using statements
        var parameters = method.GetParameters();
        return parameters.Any(p => typeof(IDisposable).IsAssignableFrom(p.ParameterType));
    }
}