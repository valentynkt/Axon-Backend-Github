using System.Reflection;

namespace Axon.ArchitectureTests;

/// <summary>
/// Architecture tests to enforce testing library standards across all test assemblies.
/// Ensures NUnit + Shouldly usage and prohibits XUnit + FluentAssertions.
/// </summary>
[TestFixture]
public class TestingLibraryEnforcementTests
{
    private static Assembly[] _testAssemblies = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // Load all test assemblies for analysis
        _testAssemblies = GetAllTestAssemblies();
    }

    #region Prohibited Libraries Tests

    [Test]
    public void TestAssemblies_ShouldNotReferenceXUnitFramework()
    {
        var result = Types.InAssemblies(_testAssemblies)
            .That()
            .ResideInNamespace("*.Tests")
            .Should()
            .NotHaveDependencyOn("Xunit")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Test assemblies should not reference XUnit framework. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Test]
    public void TestAssemblies_ShouldNotReferenceFluentAssertions()
    {
        var result = Types.InAssemblies(_testAssemblies)
            .That()
            .ResideInNamespace("*.Tests")
            .Should()
            .NotHaveDependencyOn("FluentAssertions")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Test assemblies should not reference FluentAssertions. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Test]
    public void TestTypes_ShouldNotUseXUnitAttributes()
    {
        // Check for common XUnit patterns in method names and attributes
        var xunitPatternViolations = new List<string>();
        
        foreach (var assembly in _testAssemblies)
        {
            var types = assembly.GetTypes().Where(t => t.Namespace?.Contains("Tests") == true);
            
            foreach (var type in types)
            {
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                
                foreach (var method in methods)
                {
                    var attributes = method.GetCustomAttributes(false);
                    var hasXUnitAttributes = attributes.Any(attr => 
                        attr.GetType().FullName?.StartsWith("Xunit.") == true);
                    
                    if (hasXUnitAttributes)
                    {
                        xunitPatternViolations.Add($"{type.FullName}.{method.Name}");
                    }
                }
            }
        }

        xunitPatternViolations.ShouldBeEmpty(
            $"Found XUnit attributes in test methods. Use NUnit attributes instead: {string.Join(", ", xunitPatternViolations)}");
    }

    #endregion

    #region Required Libraries Tests

    [Test]
    public void TestAssemblies_ShouldReferenceNUnit()
    {
        foreach (var assembly in _testAssemblies)
        {
            if (assembly.GetName().Name?.Contains("ArchitectureTests") == true)
                continue; // Skip self-reference check

            var referencesNUnit = assembly.GetReferencedAssemblies()
                .Any(a => a.Name?.StartsWith("NUnit") == true);

            referencesNUnit.ShouldBeTrue(
                $"Test assembly {assembly.GetName().Name} should reference NUnit framework");
        }
    }

    [Test]
    public void TestAssemblies_ShouldReferenceShouldly()
    {
        foreach (var assembly in _testAssemblies)
        {
            if (assembly.GetName().Name?.Contains("ArchitectureTests") == true)
                continue; // Skip self-reference check

            var referencesShouldly = assembly.GetReferencedAssemblies()
                .Any(a => a.Name?.Equals("Shouldly", StringComparison.OrdinalIgnoreCase) == true);

            referencesShouldly.ShouldBeTrue(
                $"Test assembly {assembly.GetName().Name} should reference Shouldly assertion library");
        }
    }

    [Test]
    public void TestMethods_ShouldUseNUnitAttributes()
    {
        var nunitAttributeViolations = new List<string>();

        foreach (var assembly in _testAssemblies)
        {
            if (assembly.GetName().Name?.Contains("ArchitectureTests") == true)
                continue;

            var types = assembly.GetTypes()
                .Where(t => t.Namespace?.Contains("Tests") == true)
                .Where(t => t.Name.EndsWith("Tests"));

            foreach (var type in types)
            {
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(m => m.Name.Contains("Test") || m.Name.Contains("Should"));

                foreach (var method in methods)
                {
                    var attributes = method.GetCustomAttributes(false);
                    var hasNUnitAttribute = attributes.Any(attr =>
                        attr.GetType().FullName?.StartsWith("NUnit.Framework.Test") == true ||
                        attr.GetType().Name == "TestAttribute" ||
                        attr.GetType().Name == "TestCaseAttribute");

                    if (!hasNUnitAttribute && method.Name.Contains("Test"))
                    {
                        nunitAttributeViolations.Add($"{type.FullName}.{method.Name}");
                    }
                }
            }
        }

        nunitAttributeViolations.ShouldBeEmpty(
            $"Test methods should use NUnit attributes ([Test], [TestCase], etc.): {string.Join(", ", nunitAttributeViolations)}");
    }

    #endregion

    #region Code Usage Detection Tests

    [Test]
    public void TestAssemblies_ShouldNotContainFluentAssertionsUsage()
    {
        var fluentAssertionsUsageViolations = new List<string>();

        foreach (var assembly in _testAssemblies)
        {
            var assemblyName = assembly.GetName().Name;
            if (assemblyName?.Contains("ArchitectureTests") == true)
                continue;

            try
            {
                // Check if any types in the assembly have dependencies on FluentAssertions
                var hasFluentAssertionsUsage = assembly.GetReferencedAssemblies()
                    .Any(a => a.Name?.Equals("FluentAssertions", StringComparison.OrdinalIgnoreCase) == true);

                if (hasFluentAssertionsUsage)
                {
                    fluentAssertionsUsageViolations.Add($"{assemblyName} references FluentAssertions");
                }
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"Warning: Could not analyze assembly {assemblyName}: {ex.Message}");
            }
        }

        fluentAssertionsUsageViolations.ShouldBeEmpty(
            $"Found FluentAssertions usage. Use Shouldly instead: {string.Join(", ", fluentAssertionsUsageViolations)}");
    }

    [Test]
    public void TestTypes_ShouldUseShouldlyAssertionSyntax()
    {
        var shouldlyUsageViolations = new List<string>();

        foreach (var assembly in _testAssemblies)
        {
            if (assembly.GetName().Name?.Contains("ArchitectureTests") == true)
                continue;

            var testClasses = assembly.GetTypes()
                .Where(t => t.Namespace?.Contains("Tests") == true)
                .Where(t => t.Name.EndsWith("Tests"));

            foreach (var testClass in testClasses)
            {
                var usesShouldly = assembly.GetReferencedAssemblies()
                    .Any(a => a.Name?.Equals("Shouldly", StringComparison.OrdinalIgnoreCase) == true);

                var hasTestMethods = testClass.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Any(m => m.Name.Contains("Test") || m.Name.Contains("Should"));

                if (hasTestMethods && !usesShouldly)
                {
                    shouldlyUsageViolations.Add(testClass.FullName ?? testClass.Name);
                }
            }
        }

        shouldlyUsageViolations.ShouldBeEmpty(
            $"Test classes should use Shouldly for assertions: {string.Join(", ", shouldlyUsageViolations)}");
    }

    #endregion

    #region Assembly Validation Tests

    [Test]
    public void TestAssemblies_ShouldNotContainProhibitedPackageReferences()
    {
        var prohibitedPackages = new[]
        {
            "xunit",
            "xunit.core", 
            "xunit.assert",
            "xunit.abstractions",
            "xunit.analyzers",
            "xunit.runner.visualstudio",
            "xunit.runner.console",
            "fluentassertions"
        };

        var violations = new List<string>();

        foreach (var assembly in _testAssemblies)
        {
            if (assembly.GetName().Name?.Contains("ArchitectureTests") == true)
                continue;

            var assemblyPackages = assembly.GetReferencedAssemblies()
                .Select(a => a.Name?.ToLowerInvariant())
                .Where(name => !string.IsNullOrEmpty(name));
            
            foreach (var prohibitedPackage in prohibitedPackages)
            {
                var hasProhibitedPackage = assemblyPackages
                    .Any(pkg => pkg?.Equals(prohibitedPackage.ToLowerInvariant()) == true);

                if (hasProhibitedPackage)
                {
                    violations.Add($"{assembly.GetName().Name} references {prohibitedPackage}");
                }
            }
        }

        violations.ShouldBeEmpty(
            $"Found prohibited package references: {string.Join(", ", violations)}");
    }

    [Test]
    public void TestAssemblies_ShouldContainRequiredPackageReferences()
    {
        var requiredPackages = new[] { "nunit", "shouldly" };
        var violations = new List<string>();

        foreach (var assembly in _testAssemblies)
        {
            if (assembly.GetName().Name?.Contains("ArchitectureTests") == true)
                continue;

            var assemblyPackages = assembly.GetReferencedAssemblies()
                .Select(a => a.Name?.ToLowerInvariant())
                .Where(name => !string.IsNullOrEmpty(name))
                .ToList();

            foreach (var requiredPackage in requiredPackages)
            {
                var hasRequiredPackage = assemblyPackages
                    .Any(pkg => pkg?.Equals(requiredPackage) == true);

                if (!hasRequiredPackage)
                {
                    violations.Add($"{assembly.GetName().Name} missing {requiredPackage}");
                }
            }
        }

        violations.ShouldBeEmpty(
            $"Missing required package references: {string.Join(", ", violations)}");
    }

    #endregion

    #region Helper Methods

    private static Assembly[] GetAllTestAssemblies()
    {
        var testAssemblies = new List<Assembly>();
        
        // Get currently loaded assemblies that are test assemblies
        var loadedTestAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic)
            .Where(a => a.GetName().Name?.Contains("Tests") == true)
            .ToList();

        testAssemblies.AddRange(loadedTestAssemblies);

        // Try to load additional test assemblies from the current directory
        var currentAssembly = Assembly.GetExecutingAssembly();
        var assemblyDirectory = Path.GetDirectoryName(currentAssembly.Location);
        
        if (!string.IsNullOrEmpty(assemblyDirectory))
        {
            var testAssemblyFiles = Directory.GetFiles(assemblyDirectory, "*Tests*.dll", SearchOption.AllDirectories)
                .Where(f => !loadedTestAssemblies.Any(a => 
                    string.Equals(Path.GetFileName(a.Location), Path.GetFileName(f), StringComparison.OrdinalIgnoreCase)));

            foreach (var file in testAssemblyFiles)
            {
                try
                {
                    var assembly = Assembly.LoadFrom(file);
                    testAssemblies.Add(assembly);
                }
                catch (Exception ex)
                {
                    TestContext.WriteLine($"Warning: Could not load assembly {file}: {ex.Message}");
                }
            }
        }

        return testAssemblies.Distinct().ToArray();
    }

    #endregion
}