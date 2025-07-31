using NUnit.Framework;
using Shouldly;
using NSubstitute;
using Axon.ArchitectureTests.Framework.Engine;
using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Configuration;
using System.Reflection;

namespace Axon.ArchitectureTests.Framework.Tests.Engine;

[TestFixture]
public sealed class ArchitectureContextTests
{
    private IArchitectureConfiguration _mockConfiguration = null!;
    private List<Assembly> _testAssemblies = null!;

    [SetUp]
    public void SetUp()
    {
        _mockConfiguration = Substitute.For<IArchitectureConfiguration>();
        _testAssemblies = new List<Assembly> { typeof(ArchitectureContextTests).Assembly };
    }

    [Test]
    public void Constructor_WithValidInputs_ShouldInitializeCorrectly()
    {
        // Act
        var context = new ArchitectureContext(_testAssemblies, _mockConfiguration);

        // Assert
        context.ShouldNotBeNull();
        context.Assemblies.ShouldNotBeEmpty();
        context.Types.ShouldNotBeEmpty();
        context.Configuration.ShouldBe(_mockConfiguration);
    }

    [Test]
    public void Constructor_WithNullAssemblies_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => 
            new ArchitectureContext(null!, _mockConfiguration));
    }

    [Test]
    public void Constructor_WithNullConfiguration_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => 
            new ArchitectureContext(_testAssemblies, null!));
    }

    [Test]
    public void Constructor_WithEmptyAssemblies_ShouldCreateContextWithEmptyTypes()
    {
        // Arrange
        var emptyAssemblies = new List<Assembly>();

        // Act
        var context = new ArchitectureContext(emptyAssemblies, _mockConfiguration);

        // Assert
        context.Assemblies.ShouldBeEmpty();
        context.Types.ShouldBeEmpty();
    }

    [Test]
    public void Types_ShouldContainAllPublicTypesFromAssemblies()
    {
        // Arrange
        var assemblies = new List<Assembly> 
        { 
            typeof(ArchitectureContextTests).Assembly,
            typeof(string).Assembly
        };

        // Act
        var context = new ArchitectureContext(assemblies, _mockConfiguration);

        // Assert
        context.Types.ShouldNotBeEmpty();
        
        // Should contain types from test assembly
        context.Types.ShouldContain(t => t.Name == nameof(ArchitectureContextTests));
        
        // Should contain types from System assembly
        context.Types.ShouldContain(t => t.Name == nameof(String));
    }

    [Test]
    public void Types_ShouldFilterOutCompilerGeneratedTypes()
    {
        // Act
        var context = new ArchitectureContext(_testAssemblies, _mockConfiguration);

        // Assert
        // Compiler generated types should be filtered out
        context.Types.ShouldNotContain(t => t.Name.Contains('<') || t.Name.Contains('>'));
    }

    [Test]
    public void GetTypesByNamespace_WithValidNamespace_ShouldReturnMatchingTypes()
    {
        // Arrange
        var context = new ArchitectureContext(_testAssemblies, _mockConfiguration);
        var expectedNamespace = typeof(ArchitectureContextTests).Namespace!;

        // Act
        var typesInNamespace = context.GetTypesByNamespace(expectedNamespace);

        // Assert
        typesInNamespace.ShouldNotBeEmpty();
        typesInNamespace.ShouldAllBe(t => t.Namespace == expectedNamespace);
        typesInNamespace.ShouldContain(t => t.Name == nameof(ArchitectureContextTests));
    }

    [Test]
    public void GetTypesByNamespace_WithNonExistentNamespace_ShouldReturnEmpty()
    {
        // Arrange
        var context = new ArchitectureContext(_testAssemblies, _mockConfiguration);

        // Act
        var typesInNamespace = context.GetTypesByNamespace("NonExistent.Namespace");

        // Assert
        typesInNamespace.ShouldBeEmpty();
    }

    [Test]
    public void GetTypesByAssembly_WithValidAssembly_ShouldReturnMatchingTypes()
    {
        // Arrange
        var context = new ArchitectureContext(_testAssemblies, _mockConfiguration);
        var targetAssembly = typeof(ArchitectureContextTests).Assembly;

        // Act
        var typesInAssembly = context.GetTypesByAssembly(targetAssembly);

        // Assert
        typesInAssembly.ShouldNotBeEmpty();
        typesInAssembly.ShouldAllBe(t => t.Assembly == targetAssembly);
        typesInAssembly.ShouldContain(t => t.Name == nameof(ArchitectureContextTests));
    }

    [Test]
    public void GetTypesByAssembly_WithNonLoadedAssembly_ShouldReturnEmpty()
    {
        // Arrange
        var context = new ArchitectureContext(_testAssemblies, _mockConfiguration);
        var nonLoadedAssembly = Assembly.LoadFrom(typeof(NUnit.Framework.Assert).Assembly.Location);

        // Act
        var typesInAssembly = context.GetTypesByAssembly(nonLoadedAssembly);

        // Assert
        typesInAssembly.ShouldBeEmpty();
    }

    [Test]
    public void GetTypesByBaseType_WithValidBaseType_ShouldReturnDerivedTypes()
    {
        // Arrange
        var context = new ArchitectureContext(_testAssemblies, _mockConfiguration);

        // Act
        var derivedTypes = context.GetTypesByBaseType(typeof(object));

        // Assert
        derivedTypes.ShouldNotBeEmpty();
        derivedTypes.ShouldAllBe(t => typeof(object).IsAssignableFrom(t));
    }

    [Test]
    public void GetTypesByInterface_WithValidInterface_ShouldReturnImplementingTypes()
    {
        // Arrange
        var assemblies = new List<Assembly> 
        { 
            typeof(IDisposable).Assembly,
            typeof(ArchitectureContextTests).Assembly
        };
        var context = new ArchitectureContext(assemblies, _mockConfiguration);

        // Act
        var implementingTypes = context.GetTypesByInterface(typeof(IDisposable));

        // Assert
        implementingTypes.ShouldNotBeEmpty();
        implementingTypes.ShouldAllBe(t => typeof(IDisposable).IsAssignableFrom(t));
    }

    [Test]
    public void GetTypesByAttribute_WithValidAttribute_ShouldReturnDecoratedTypes()
    {
        // Arrange
        var context = new ArchitectureContext(_testAssemblies, _mockConfiguration);

        // Act
        var decoratedTypes = context.GetTypesByAttribute(typeof(TestFixtureAttribute));

        // Assert
        decoratedTypes.ShouldNotBeEmpty();
        decoratedTypes.ShouldAllBe(t => t.GetCustomAttribute<TestFixtureAttribute>() != null);
        decoratedTypes.ShouldContain(t => t.Name == nameof(ArchitectureContextTests));
    }

    [Test]
    public void Configuration_ShouldReturnInjectedConfiguration()
    {
        // Act
        var context = new ArchitectureContext(_testAssemblies, _mockConfiguration);

        // Assert
        context.Configuration.ShouldBe(_mockConfiguration);
    }

    [Test]
    public void Context_ShouldBeConcurrentSafe()
    {
        // Arrange
        var context = new ArchitectureContext(_testAssemblies, _mockConfiguration);
        var results = new List<bool>();

        // Act
        Parallel.For(0, 100, _ =>
        {
            var types = context.Types.ToList();
            var assemblies = context.Assemblies.ToList();
            results.Add(types.Count > 0 && assemblies.Count > 0);
        });

        // Assert
        results.ShouldAllBe(r => r == true);
    }
}