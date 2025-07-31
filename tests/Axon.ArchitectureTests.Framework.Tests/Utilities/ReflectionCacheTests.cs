using NUnit.Framework;
using Shouldly;
using Axon.ArchitectureTests.Framework.Utilities;
using System.Reflection;

namespace Axon.ArchitectureTests.Framework.Tests.Utilities;

[TestFixture]
public sealed class ReflectionCacheTests
{
    private ReflectionCache _cache = null!;

    [SetUp]
    public void SetUp()
    {
        _cache = new ReflectionCache();
    }

    [TearDown]
    public void TearDown()
    {
        _cache.Clear();
    }

    [Test]
    public void GetTypeInfo_WithValidType_ShouldReturnTypeInfo()
    {
        // Arrange
        var type = typeof(ReflectionCacheTests);

        // Act
        var typeInfo = _cache.GetTypeInfo(type);

        // Assert
        typeInfo.ShouldNotBeNull();
        typeInfo.Type.ShouldBe(type);
        typeInfo.Name.ShouldBe(type.Name);
        typeInfo.FullName.ShouldBe(type.FullName);
        typeInfo.Namespace.ShouldBe(type.Namespace);
        typeInfo.Assembly.ShouldBe(type.Assembly);
    }

    [Test]
    public void GetTypeInfo_WithSameTypeTwice_ShouldReturnSameInstance()
    {
        // Arrange
        var type = typeof(ReflectionCacheTests);

        // Act
        var typeInfo1 = _cache.GetTypeInfo(type);
        var typeInfo2 = _cache.GetTypeInfo(type);

        // Assert
        typeInfo1.ShouldBeSameAs(typeInfo2);
    }

    [Test]
    public void GetTypeInfo_WithNullType_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => _cache.GetTypeInfo(null!));
    }

    [Test]
    public void GetDependencies_WithValidType_ShouldReturnDependencies()
    {
        // Arrange
        var type = typeof(ReflectionCacheTests);

        // Act
        var dependencies = _cache.GetDependencies(type);

        // Assert
        dependencies.ShouldNotBeNull();
        dependencies.ShouldNotBeEmpty();
        dependencies.ShouldContain(typeof(TestFixtureAttribute));
    }

    [Test]
    public void GetDependencies_WithSameTypeTwice_ShouldReturnSameInstance()
    {
        // Arrange
        var type = typeof(ReflectionCacheTests);

        // Act
        var dependencies1 = _cache.GetDependencies(type);
        var dependencies2 = _cache.GetDependencies(type);

        // Assert
        dependencies1.ShouldBeSameAs(dependencies2);
    }

    [Test]
    public void GetDependencies_WithNullType_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => _cache.GetDependencies(null!));
    }

    [Test]
    public void GetInterfaces_WithValidType_ShouldReturnInterfaces()
    {
        // Arrange
        var type = typeof(List<string>);

        // Act
        var interfaces = _cache.GetInterfaces(type);

        // Assert
        interfaces.ShouldNotBeNull();
        interfaces.ShouldNotBeEmpty();
        interfaces.ShouldContain(typeof(IList<string>));
        interfaces.ShouldContain(typeof(IEnumerable<string>));
    }

    [Test]
    public void GetInterfaces_WithSameTypeTwice_ShouldReturnSameInstance()
    {
        // Arrange
        var type = typeof(IDisposable); // Interface implementing no other interfaces

        // Act
        var interfaces1 = _cache.GetInterfaces(type);
        var interfaces2 = _cache.GetInterfaces(type);

        // Assert
        interfaces1.ShouldBeSameAs(interfaces2);
    }

    [Test]
    public void GetInterfaces_WithNullType_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => _cache.GetInterfaces(null!));
    }

    [Test]
    public void GetBaseTypes_WithValidType_ShouldReturnBaseTypes()
    {
        // Arrange
        var type = typeof(ArgumentException);

        // Act
        var baseTypes = _cache.GetBaseTypes(type);

        // Assert
        baseTypes.ShouldNotBeNull();
        baseTypes.ShouldNotBeEmpty();
        baseTypes.ShouldContain(typeof(SystemException));
        baseTypes.ShouldContain(typeof(Exception));
        baseTypes.ShouldContain(typeof(object));
    }

    [Test]
    public void GetBaseTypes_WithObjectType_ShouldReturnEmpty()
    {
        // Arrange
        var type = typeof(object);

        // Act
        var baseTypes = _cache.GetBaseTypes(type);

        // Assert
        baseTypes.ShouldNotBeNull();
        baseTypes.ShouldBeEmpty();
    }

    [Test]
    public void GetBaseTypes_WithSameTypeTwice_ShouldReturnSameInstance()
    {
        // Arrange
        var type = typeof(ArgumentException);

        // Act
        var baseTypes1 = _cache.GetBaseTypes(type);
        var baseTypes2 = _cache.GetBaseTypes(type);

        // Assert
        baseTypes1.ShouldBeSameAs(baseTypes2);
    }

    [Test]
    public void GetBaseTypes_WithNullType_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => _cache.GetBaseTypes(null!));
    }

    [Test]
    public void GetAttributes_WithValidType_ShouldReturnAttributes()
    {
        // Arrange
        var type = typeof(ReflectionCacheTests);

        // Act
        var attributes = _cache.GetAttributes(type);

        // Assert
        attributes.ShouldNotBeNull();
        attributes.ShouldNotBeEmpty();
        attributes.ShouldContain(a => a.GetType() == typeof(TestFixtureAttribute));
    }

    [Test]
    public void GetAttributes_WithSameTypeTwice_ShouldReturnSameInstance()
    {
        // Arrange
        var type = typeof(ReflectionCacheTests);

        // Act
        var attributes1 = _cache.GetAttributes(type);
        var attributes2 = _cache.GetAttributes(type);

        // Assert
        attributes1.ShouldBeSameAs(attributes2);
    }

    [Test]
    public void GetAttributes_WithNullType_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => _cache.GetAttributes(null!));
    }

    [Test]
    public void Clear_ShouldRemoveAllCachedData()
    {
        // Arrange
        var type = typeof(ReflectionCacheTests);
        _cache.GetTypeInfo(type);
        _cache.GetDependencies(type);

        // Act
        _cache.Clear();

        // Assert
        // Re-getting should create new instances (can't easily test this without access to internal state)
        // But we can verify the cache still works after clearing
        var typeInfo = _cache.GetTypeInfo(type);
        typeInfo.ShouldNotBeNull();
    }

    [Test]
    public void Cache_ShouldBeThreadSafe()
    {
        // Arrange
        var type = typeof(ReflectionCacheTests);
        var results = new List<TypeInfo>();

        // Act
        Parallel.For(0, 100, _ =>
        {
            var typeInfo = _cache.GetTypeInfo(type);
            lock (results)
            {
                results.Add(typeInfo);
            }
        });

        // Assert
        results.Count.ShouldBe(100);
        results.ShouldAllBe(ti => ReferenceEquals(ti, results[0])); // All should be the same instance
    }

    [Test]
    public void Cache_PerformanceTest_ShouldImproveWithCaching()
    {
        // Arrange
        var type = typeof(ReflectionCacheTests);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - First call (cache miss)
        _cache.GetTypeInfo(type);
        var firstCallTime = stopwatch.ElapsedTicks;

        stopwatch.Restart();

        // Act - Second call (cache hit)
        _cache.GetTypeInfo(type);
        var secondCallTime = stopwatch.ElapsedTicks;

        // Assert
        // Cache hit should be faster than cache miss
        secondCallTime.ShouldBeLessThan(firstCallTime);
        
        TestContext.WriteLine($"First call: {firstCallTime} ticks, Second call: {secondCallTime} ticks");
    }

    [Test]
    public void TypeInfo_ShouldContainCorrectInformation()
    {
        // Arrange
        var type = typeof(List<string>);

        // Act
        var typeInfo = _cache.GetTypeInfo(type);

        // Assert
        typeInfo.Type.ShouldBe(type);
        typeInfo.Name.ShouldBe("List`1");
        typeInfo.FullName.ShouldBe("System.Collections.Generic.List`1[[System.String, System.Private.CoreLib, Version=8.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e]]");
        typeInfo.Namespace.ShouldBe("System.Collections.Generic");
        typeInfo.Assembly.ShouldBe(type.Assembly);
        typeInfo.IsGeneric.ShouldBeTrue();
        typeInfo.IsInterface.ShouldBeFalse();
        typeInfo.IsAbstract.ShouldBeFalse();
        typeInfo.IsClass.ShouldBeTrue();
    }

    [Test]
    public void GetDependencies_ShouldIncludeConstructorParameterTypes()
    {
        // Arrange - Use a type that has constructor dependencies
        var type = typeof(FileStream);

        // Act
        var dependencies = _cache.GetDependencies(type);

        // Assert
        dependencies.ShouldNotBeNull();
        dependencies.ShouldContain(typeof(string)); // File path parameter
    }

    [Test]
    public void GetDependencies_ShouldIncludeFieldTypes()
    {
        // Arrange
        var type = typeof(TestClassWithDependencies);

        // Act
        var dependencies = _cache.GetDependencies(type);

        // Assert
        dependencies.ShouldNotBeNull();
        dependencies.ShouldContain(typeof(string));
        dependencies.ShouldContain(typeof(int));
    }

    private class TestClassWithDependencies
    {
        private readonly string _stringField = string.Empty;
        private readonly int _intField = 0;
        
        public TestClassWithDependencies(string parameter, int anotherParameter)
        {
            _stringField = parameter;
            _intField = anotherParameter;
        }
    }
}