using NUnit.Framework;
using Shouldly;
using Axon.ArchitectureTests.Framework.Utilities;
using System.Reflection;

namespace Axon.ArchitectureTests.Framework.Tests.Utilities;

[TestFixture]
public sealed class AssemblyAnalyzerTests
{
    [Test]
    public void LoadSolutionAssemblies_ShouldReturnNonEmptyList()
    {
        // Act
        var assemblies = AssemblyAnalyzer.LoadSolutionAssemblies();

        // Assert
        assemblies.ShouldNotBeNull();
        assemblies.ShouldNotBeEmpty();
    }

    [Test]
    public void LoadSolutionAssemblies_ShouldIncludeCurrentAssembly()
    {
        // Act
        var assemblies = AssemblyAnalyzer.LoadSolutionAssemblies();

        // Assert
        assemblies.ShouldContain(Assembly.GetExecutingAssembly());
    }

    [Test]
    public void LoadSolutionAssemblies_ShouldFilterSystemAssemblies()
    {
        // Act
        var assemblies = AssemblyAnalyzer.LoadSolutionAssemblies();

        // Assert
        assemblies.ShouldNotContain(a => a.GetName().Name!.StartsWith("System"));
        assemblies.ShouldNotContain(a => a.GetName().Name!.StartsWith("Microsoft"));
        assemblies.ShouldNotContain(a => a.GetName().Name!.StartsWith("netstandard"));
    }

    [Test]
    public void LoadSolutionAssemblies_ShouldIncludeAxonAssemblies()
    {
        // Act
        var assemblies = AssemblyAnalyzer.LoadSolutionAssemblies();

        // Assert
        assemblies.ShouldContain(a => a.GetName().Name!.Contains("Axon", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public void LoadAssembliesFromDirectory_WithValidDirectory_ShouldReturnAssemblies()
    {
        // Arrange
        var currentDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

        // Act
        var assemblies = AssemblyAnalyzer.LoadAssembliesFromDirectory(currentDirectory);

        // Assert
        assemblies.ShouldNotBeNull();
        assemblies.ShouldNotBeEmpty();
        assemblies.ShouldContain(Assembly.GetExecutingAssembly());
    }

    [Test]
    public void LoadAssembliesFromDirectory_WithNonExistentDirectory_ShouldReturnEmpty()
    {
        // Arrange
        var nonExistentDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        // Act
        var assemblies = AssemblyAnalyzer.LoadAssembliesFromDirectory(nonExistentDirectory);

        // Assert
        assemblies.ShouldNotBeNull();
        assemblies.ShouldBeEmpty();
    }

    [Test]
    public void LoadAssembliesFromDirectory_WithEmptyDirectory_ShouldReturnEmpty()
    {
        // Arrange
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDirectory);

        try
        {
            // Act
            var assemblies = AssemblyAnalyzer.LoadAssembliesFromDirectory(tempDirectory);

            // Assert
            assemblies.ShouldNotBeNull();
            assemblies.ShouldBeEmpty();
        }
        finally
        {
            Directory.Delete(tempDirectory);
        }
    }

    [Test]
    public void LoadAssemblyByName_WithValidName_ShouldReturnAssembly()
    {
        // Arrange
        var assemblyName = Assembly.GetExecutingAssembly().GetName().Name!;

        // Act
        var assembly = AssemblyAnalyzer.LoadAssemblyByName(assemblyName);

        // Assert
        assembly.ShouldNotBeNull();
        assembly.GetName().Name.ShouldBe(assemblyName);
    }

    [Test]
    public void LoadAssemblyByName_WithInvalidName_ShouldReturnNull()
    {
        // Arrange
        const string invalidAssemblyName = "NonExistent.Assembly.Name";

        // Act
        var assembly = AssemblyAnalyzer.LoadAssemblyByName(invalidAssemblyName);

        // Assert
        assembly.ShouldBeNull();
    }

    [Test]
    public void LoadAssemblyByName_WithNullOrEmptyName_ShouldReturnNull()
    {
        // Act & Assert
        AssemblyAnalyzer.LoadAssemblyByName(null!).ShouldBeNull();
        AssemblyAnalyzer.LoadAssemblyByName(string.Empty).ShouldBeNull();
        AssemblyAnalyzer.LoadAssemblyByName("   ").ShouldBeNull();
    }

    [Test]
    public void GetAllTypes_WithValidAssembly_ShouldReturnTypes()
    {
        // Arrange
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        var types = AssemblyAnalyzer.GetAllTypes(assembly);

        // Assert
        types.ShouldNotBeNull();
        types.ShouldNotBeEmpty();
        types.ShouldContain(typeof(AssemblyAnalyzerTests));
    }

    [Test]
    public void GetAllTypes_WithNullAssembly_ShouldReturnEmpty()
    {
        // Act
        var types = AssemblyAnalyzer.GetAllTypes(null!);

        // Assert
        types.ShouldNotBeNull();
        types.ShouldBeEmpty();
    }

    [Test]
    public void GetAllTypes_ShouldFilterCompilerGeneratedTypes()
    {
        // Arrange
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        var types = AssemblyAnalyzer.GetAllTypes(assembly);

        // Assert
        types.ShouldNotContain(t => t.Name.Contains('<') || t.Name.Contains('>'));
        types.ShouldNotContain(t => t.Name.StartsWith('<'));
    }

    [Test]
    public void GetTypeDependencies_WithValidType_ShouldReturnDependencies()
    {
        // Arrange
        var type = typeof(AssemblyAnalyzerTests);

        // Act
        var dependencies = AssemblyAnalyzer.GetTypeDependencies(type);

        // Assert
        dependencies.ShouldNotBeNull();
        dependencies.ShouldNotBeEmpty();
        dependencies.ShouldContain(typeof(TestFixtureAttribute));
    }

    [Test]
    public void GetTypeDependencies_WithNullType_ShouldReturnEmpty()
    {
        // Act
        var dependencies = AssemblyAnalyzer.GetTypeDependencies(null!);

        // Assert
        dependencies.ShouldNotBeNull();
        dependencies.ShouldBeEmpty();
    }

    [Test]
    public void IsSystemAssembly_WithSystemAssembly_ShouldReturnTrue()
    {
        // Arrange
        var systemAssembly = typeof(string).Assembly;

        // Act
        var isSystem = AssemblyAnalyzer.IsSystemAssembly(systemAssembly);

        // Assert
        isSystem.ShouldBeTrue();
    }

    [Test]
    public void IsSystemAssembly_WithUserAssembly_ShouldReturnFalse()
    {
        // Arrange
        var userAssembly = Assembly.GetExecutingAssembly();

        // Act
        var isSystem = AssemblyAnalyzer.IsSystemAssembly(userAssembly);

        // Assert
        isSystem.ShouldBeFalse();
    }

    [Test]
    public void IsSystemAssembly_WithNullAssembly_ShouldReturnFalse()
    {
        // Act
        var isSystem = AssemblyAnalyzer.IsSystemAssembly(null!);

        // Assert
        isSystem.ShouldBeFalse();
    }

    [Test]
    public void LoadSolutionAssemblies_ShouldBeDeterministic()
    {
        // Act
        var assemblies1 = AssemblyAnalyzer.LoadSolutionAssemblies();
        var assemblies2 = AssemblyAnalyzer.LoadSolutionAssemblies();

        // Assert
        assemblies1.Count.ShouldBe(assemblies2.Count);
        var names1 = assemblies1.Select(a => a.GetName().Name).OrderBy(n => n).ToList();
        var names2 = assemblies2.Select(a => a.GetName().Name).OrderBy(n => n).ToList();
        names1.ShouldBe(names2);
    }

    [Test]
    public void LoadSolutionAssemblies_PerformanceTest_ShouldCompleteWithinReasonableTime()
    {
        // Arrange
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var assemblies = AssemblyAnalyzer.LoadSolutionAssemblies();

        // Assert
        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(5000); // Should complete within 5 seconds
        assemblies.ShouldNotBeEmpty();
        
        TestContext.WriteLine($"Loaded {assemblies.Count()} assemblies in {stopwatch.ElapsedMilliseconds}ms");
    }
}