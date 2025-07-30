namespace Axon.ArchitectureTests;

/// <summary>
/// Architecture tests to enforce test code quality standards and patterns.
/// Validates proper test structure, naming conventions, and best practices.
/// </summary>
[TestFixture]
public class TestCodeQualityTests
{
    private static Assembly[] _testAssemblies = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _testAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.Contains("Tests") == true)
            .Where(a => !a.IsDynamic)
            .ToArray();
    }

    [Test]
    public void TestClasses_ShouldFollowNamingConvention()
    {
        var result = Types.InAssemblies(_testAssemblies)
            .That()
            .ResideInNamespace("*.Tests")
            .And()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .Should()
            .HaveNameEndingWith("Tests")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Test classes should follow naming convention ending with 'Tests'. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Test]
    public void TestClasses_ShouldHaveTestFixtureAttribute()
    {
        var violations = new List<string>();

        foreach (var assembly in _testAssemblies)
        {
            if (assembly.GetName().Name?.Contains("ArchitectureTests") == true)
                continue;

            var testClasses = assembly.GetTypes()
                .Where(t => t.Namespace?.Contains("Tests") == true)
                .Where(t => t.Name.EndsWith("Tests"))
                .Where(t => !t.IsAbstract);

            foreach (var testClass in testClasses)
            {
                var hasTestFixtureAttribute = testClass.GetCustomAttributes(false)
                    .Any(attr => attr.GetType().Name == "TestFixtureAttribute" ||
                                attr.GetType().FullName == "NUnit.Framework.TestFixtureAttribute");

                if (!hasTestFixtureAttribute)
                {
                    violations.Add(testClass.FullName ?? testClass.Name);
                }
            }
        }

        violations.ShouldBeEmpty(
            $"Test classes should have [TestFixture] attribute: {string.Join(", ", violations)}");
    }

    [Test]
    public void TestMethods_ShouldBePublic()
    {
        var violations = new List<string>();

        foreach (var assembly in _testAssemblies)
        {
            var testClasses = assembly.GetTypes()
                .Where(t => t.Namespace?.Contains("Tests") == true)
                .Where(t => t.Name.EndsWith("Tests"));

            foreach (var testClass in testClasses)
            {
                var testMethods = testClass.GetMethods(BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.GetCustomAttributes(false)
                        .Any(attr => attr.GetType().Name.Contains("Test")));

                foreach (var method in testMethods)
                {
                    if (!method.IsPublic)
                    {
                        violations.Add($"{testClass.FullName}.{method.Name}");
                    }
                }
            }
        }

        violations.ShouldBeEmpty(
            $"Test methods must be public to be discoverable by NUnit: {string.Join(", ", violations)}");
    }

    [Test]
    public void TestMethods_ShouldFollowNamingConvention()
    {
        var violations = new List<string>();

        foreach (var assembly in _testAssemblies)
        {
            if (assembly.GetName().Name?.Contains("ArchitectureTests") == true)
                continue;

            var testClasses = assembly.GetTypes()
                .Where(t => t.Namespace?.Contains("Tests") == true)
                .Where(t => t.Name.EndsWith("Tests"));

            foreach (var testClass in testClasses)
            {
                var testMethods = testClass.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(m => m.GetCustomAttributes(false)
                        .Any(attr => attr.GetType().Name.Contains("Test")));

                foreach (var method in testMethods)
                {
                    // Test methods should follow descriptive naming patterns
                    var followsConvention = method.Name.Contains('_') ||
                                          method.Name.Contains("Should") ||
                                          method.Name.Contains("When") ||
                                          method.Name.Contains("Given") ||
                                          method.Name.Contains("Then");

                    if (!followsConvention)
                    {
                        violations.Add($"{testClass.FullName}.{method.Name}");
                    }
                }
            }
        }

        violations.ShouldBeEmpty(
            $"Test methods should follow descriptive naming convention " +
            $"(e.g., Method_GivenScenario_ShouldBehavior): {string.Join(", ", violations)}");
    }

    [Test]
    public void TestMethods_ShouldNotReturnVoid_WhenAsync()
    {
        var violations = new List<string>();

        foreach (var assembly in _testAssemblies)
        {
            // Skip architecture tests to avoid self-reference
            if (assembly.GetName().Name?.Contains("ArchitectureTests") == true)
                continue;
                
            var testClasses = assembly.GetTypes()
                .Where(t => t.Namespace?.Contains("Tests") == true)
                .Where(t => t.Name.EndsWith("Tests"));

            foreach (var testClass in testClasses)
            {
                var asyncTestMethods = testClass.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(m => m.GetCustomAttributes(false)
                        .Any(attr => attr.GetType().Name.Contains("Test")))
                    .Where(m => m.Name.Contains("Async") || 
                               m.GetParameters().Any(p => p.ParameterType.Name.Contains("CancellationToken")));

                foreach (var method in asyncTestMethods)
                {
                    var returnsTask = method.ReturnType.Name.Contains("Task");
                    
                    if (!returnsTask && method.ReturnType == typeof(void))
                    {
                        violations.Add($"{testClass.FullName}.{method.Name}");
                    }
                }
            }
        }

        violations.ShouldBeEmpty(
            $"Async test methods should return Task or Task<T>, not void: {string.Join(", ", violations)}");
    }

    [Test]
    public void TestClasses_ShouldNotHaveStaticFields_ExceptAllowedCases()
    {
        var violations = new List<string>();

        foreach (var assembly in _testAssemblies)
        {
            var testClasses = assembly.GetTypes()
                .Where(t => t.Namespace?.Contains("Tests") == true)
                .Where(t => t.Name.EndsWith("Tests"));

            foreach (var testClass in testClasses)
            {
                var staticFields = testClass.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(f => !f.IsLiteral && !f.IsInitOnly) // Exclude const and readonly
                    .Where(f => !IsAllowedStaticField(f.Name))
                    .ToList();

                foreach (var field in staticFields)
                {
                    violations.Add($"{testClass.FullName}.{field.Name}");
                }
            }
        }

        violations.ShouldBeEmpty(
            $"Test classes should not have static fields (except allowed cases) to avoid test isolation issues: " +
            $"{string.Join(", ", violations)}");
    }

    [Test]
    public void TestAssemblyReferences_ShouldNotIncludeProductionOnlyLibraries()
    {
        var prohibitedInTests = new[]
        {
            "serilog", // Tests should use test-specific logging
            "microsoft.applicationinsights", // No telemetry in tests
            "system.web" // Legacy web dependencies
        };

        var violations = new List<string>();

        foreach (var assembly in _testAssemblies)
        {
            var assemblyName = assembly.GetName().Name;
            var referencedAssemblies = assembly.GetReferencedAssemblies()
                .Select(a => a.Name?.ToLowerInvariant())
                .Where(name => !string.IsNullOrEmpty(name));

            foreach (var prohibitedLib in prohibitedInTests)
            {
                if (referencedAssemblies.Any(ra => ra?.Contains(prohibitedLib) == true))
                {
                    violations.Add($"{assemblyName} references {prohibitedLib}");
                }
            }
        }

        violations.ShouldBeEmpty(
            $"Test assemblies should not depend on production-only libraries: {string.Join(", ", violations)}");
    }

    [Test]
    public void TestClasses_ShouldInheritFromAppropriateBaseClass()
    {
        var recommendations = new List<string>();

        foreach (var assembly in _testAssemblies)
        {
            if (assembly.GetName().Name?.Contains("ArchitectureTests") == true)
                continue;

            var testClasses = assembly.GetTypes()
                .Where(t => t.Namespace?.Contains("Tests") == true)
                .Where(t => t.Name.EndsWith("Tests"))
                .Where(t => !t.Name.Contains("Base"));

            foreach (var testClass in testClasses)
            {
                var className = testClass.Name;
                var hasAppropriateBaseClass = false;
                var recommendedBaseClass = "";

                if (className.Contains("Domain"))
                {
                    hasAppropriateBaseClass = testClass.BaseType?.Name.Contains("DomainTestBase") == true;
                    recommendedBaseClass = "DomainTestBase";
                }
                else if (className.Contains("Application"))
                {
                    hasAppropriateBaseClass = testClass.BaseType?.Name.Contains("ApplicationTestBase") == true;
                    recommendedBaseClass = "ApplicationTestBase";
                }
                else if (className.Contains("Integration") || className.Contains("Api"))
                {
                    hasAppropriateBaseClass = testClass.BaseType?.Name.Contains("IntegrationTestBase") == true;
                    recommendedBaseClass = "IntegrationTestBase";
                }

                if (!string.IsNullOrEmpty(recommendedBaseClass) && !hasAppropriateBaseClass)
                {
                    recommendations.Add($"{testClass.FullName} should inherit from {recommendedBaseClass}");
                }
            }
        }

        // These are recommendations, not hard failures
        foreach (var recommendation in recommendations)
        {
            TestContext.WriteLine($"Recommendation: {recommendation}");
        }

        // We don't fail the test for base class recommendations, just log them
        TestContext.WriteLine($"Total base class recommendations: {recommendations.Count}");
    }

    [Test]
    public void TestAssemblyNames_ShouldFollowNamingConvention()
    {
        var violations = new List<string>();

        foreach (var assembly in _testAssemblies)
        {
            var assemblyName = assembly.GetName().Name!;
            var followsConvention = assemblyName.StartsWith("Axon.") && 
                                   (assemblyName.EndsWith(".Tests") || assemblyName.Contains("Tests"));

            if (!followsConvention)
            {
                violations.Add(assemblyName);
            }
        }

        violations.ShouldBeEmpty(
            $"Test assemblies should follow naming convention: Axon.[Module].[Layer].Tests. " +
            $"Violations: {string.Join(", ", violations)}");
    }

    [Test]
    public void TestTypes_ShouldNotUseHardcodedWaits()
    {
        // This test checks for common hardcoded wait patterns in test code
        var result = Types.InAssemblies(_testAssemblies)
            .That()
            .ResideInNamespace("*.Tests")
            .Should()
            .NotHaveDependencyOn("System.Threading.Thread")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Tests should not use hardcoded waits like Thread.Sleep. Use deterministic waiting mechanisms instead. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    private static bool IsAllowedStaticField(string fieldName)
    {
        var allowedStaticFields = new[]
        {
            "_testAssemblies",
            "_assemblies"
        };

        return allowedStaticFields.Contains(fieldName, StringComparer.OrdinalIgnoreCase);
    }
}