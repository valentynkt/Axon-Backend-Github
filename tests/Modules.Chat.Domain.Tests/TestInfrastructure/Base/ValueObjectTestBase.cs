using Axon.Shared.Domain;
using System.Text.Json;

namespace Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Base;

/// <summary>
/// Base class for testing value objects with comprehensive equality and validation testing.
/// Ensures value objects follow DDD patterns and maintain immutability.
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("Domain")]
[Category("ValueObject")]
public abstract class ValueObjectTestBase<TValueObject> : DomainTestBase
    where TValueObject : ValueObject
{
    /// <summary>
    /// Tests the complete equality contract for value objects.
    /// </summary>
    protected void TestEqualityContract(
        TValueObject instance1,
        TValueObject instance2Equal,
        TValueObject instance3Different)
    {
        // Reflexive property
        instance1.Equals(instance1).ShouldBeTrue("Reflexive: x.Equals(x) should be true");
        
        // Symmetric property
        instance1.Equals(instance2Equal).ShouldBeTrue("Symmetric: x.Equals(y) should be true");
        instance2Equal.Equals(instance1).ShouldBeTrue("Symmetric: y.Equals(x) should be true");
        
        // Transitive property
        var instance4Equal = CreateEqualInstance(instance1);
        instance1.Equals(instance2Equal).ShouldBeTrue();
        instance2Equal.Equals(instance4Equal).ShouldBeTrue();
        instance1.Equals(instance4Equal).ShouldBeTrue("Transitive: if x.Equals(y) and y.Equals(z) then x.Equals(z)");
        
        // Consistent property
        for (int i = 0; i < 10; i++)
        {
            instance1.Equals(instance2Equal).ShouldBeTrue($"Consistent: Equals should return same result on iteration {i}");
        }
        
        // Null comparison
        instance1.Equals(null).ShouldBeFalse("Null: x.Equals(null) should be false");
        
        // Different instances
        instance1.Equals(instance3Different).ShouldBeFalse("Different values should not be equal");
        
        // Operator overloads
        (instance1 == instance2Equal).ShouldBeTrue("== operator should work");
        (instance1 != instance3Different).ShouldBeTrue("!= operator should work");
        
        // Hash code consistency
        instance1.GetHashCode().ShouldBe(instance2Equal.GetHashCode(),
            "Equal objects must have equal hash codes");
        
        // Hash code should be consistent
        var hashCode = instance1.GetHashCode();
        for (int i = 0; i < 10; i++)
        {
            instance1.GetHashCode().ShouldBe(hashCode,
                $"Hash code should be consistent on iteration {i}");
        }
    }

    /// <summary>
    /// Override to create an equal instance of the value object.
    /// </summary>
    protected abstract TValueObject CreateEqualInstance(TValueObject original);

    /// <summary>
    /// Tests that value objects are immutable.
    /// </summary>
    protected void TestImmutability(TValueObject instance, Action<TValueObject> attemptMutation)
    {
        var originalHashCode = instance.GetHashCode();
        var originalString = instance.ToString();
        
        // Attempt to mutate (should either throw or have no effect)
        var exception = Catch(() => attemptMutation(instance));
        
        // Verify immutability
        instance.GetHashCode().ShouldBe(originalHashCode,
            "Hash code should not change after attempted mutation");
        instance.ToString().ShouldBe(originalString,
            "String representation should not change after attempted mutation");
    }

    /// <summary>
    /// Tests serialization round-trip for value objects.
    /// </summary>
    protected void TestSerializationRoundTrip(TValueObject instance)
    {
        // JSON serialization
        var json = JsonSerializer.Serialize(instance);
        var deserialized = JsonSerializer.Deserialize<TValueObject>(json);
        
        deserialized.ShouldNotBeNull();
        deserialized.ShouldBe(instance, "Deserialized object should equal original");
        deserialized!.GetHashCode().ShouldBe(instance.GetHashCode(),
            "Hash codes should match after deserialization");
    }

    /// <summary>
    /// Tests validation rules for value object creation.
    /// </summary>
    protected void TestValidation<TFactory>(
        TFactory validFactory,
        TFactory[] invalidFactories,
        Func<TFactory, TValueObject?> createFunc,
        Action<TValueObject>? validAssertion = null,
        Action<Exception>? invalidAssertion = null)
    {
        // Test valid creation
        var validInstance = createFunc(validFactory);
        validInstance.ShouldNotBeNull("Valid factory should create instance");
        validAssertion?.Invoke(validInstance!);
        
        // Test invalid creations
        foreach (var invalidFactory in invalidFactories)
        {
            var exception = Catch(() => createFunc(invalidFactory));
            
            if (exception != null)
            {
                invalidAssertion?.Invoke(exception);
            }
            else
            {
                var result = createFunc(invalidFactory);
                result.ShouldBeNull($"Invalid factory should not create instance: {invalidFactory}");
            }
        }
    }

    /// <summary>
    /// Tests boundary conditions for value object creation.
    /// </summary>
    protected void TestBoundaryConditions(
        params (string Description, Func<TValueObject?> Create, bool ShouldSucceed)[] conditions)
    {
        foreach (var (description, create, shouldSucceed) in conditions)
        {
            TestContext.WriteLine($"Testing boundary: {description}");
            
            var exception = Catch(() => create());
            
            if (shouldSucceed)
            {
                exception.ShouldBeNull($"Boundary test '{description}' should succeed");
                var instance = create();
                instance.ShouldNotBeNull($"Boundary test '{description}' should create instance");
            }
            else
            {
                if (exception == null)
                {
                    var instance = create();
                    instance.ShouldBeNull($"Boundary test '{description}' should fail");
                }
            }
        }
    }

    /// <summary>
    /// Tests that ToString provides meaningful output.
    /// </summary>
    protected void TestToString(TValueObject instance, Action<string> assertion)
    {
        var stringRepresentation = instance.ToString();
        
        stringRepresentation.ShouldNotBeNull("ToString should not return null");
        stringRepresentation.ShouldNotBeEmpty("ToString should not return empty string");
        stringRepresentation.ShouldNotBe(instance.GetType().FullName,
            "ToString should provide custom implementation, not default type name");
        
        assertion(stringRepresentation!);
    }

    /// <summary>
    /// Tests performance of value object creation.
    /// </summary>
    protected void TestCreationPerformance(
        Func<TValueObject> createFunc,
        int iterations = 10000,
        TimeSpan? maxDuration = null)
    {
        var maxTime = maxDuration ?? TimeSpan.FromMilliseconds(100);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        for (int i = 0; i < iterations; i++)
        {
            createFunc();
        }
        
        stopwatch.Stop();
        
        stopwatch.Elapsed.ShouldBeLessThan(maxTime,
            $"Creating {iterations} instances should take less than {maxTime.TotalMilliseconds}ms, but took {stopwatch.ElapsedMilliseconds}ms");
        
        TestContext.WriteLine($"Created {iterations} instances in {stopwatch.ElapsedMilliseconds}ms");
    }

    /// <summary>
    /// Tests that value objects handle null inputs correctly.
    /// </summary>
    protected void TestNullHandling(params Action[] nullCreationAttempts)
    {
        foreach (var attempt in nullCreationAttempts)
        {
            var exception = Catch(attempt);
            
            exception.ShouldNotBeNull("Null inputs should throw exception");
            exception.ShouldBeOfType<ArgumentNullException>("Null inputs should throw ArgumentNullException");
        }
    }

    /// <summary>
    /// Generates test cases for property-based testing.
    /// </summary>
    protected IEnumerable<TValueObject> GenerateTestCases(
        Func<int, TValueObject> generator,
        int count = 100)
    {
        for (int i = 0; i < count; i++)
        {
            yield return generator(i);
        }
    }

    /// <summary>
    /// Tests a property holds for all generated test cases.
    /// </summary>
    protected void TestProperty(
        IEnumerable<TValueObject> testCases,
        Func<TValueObject, bool> property,
        string propertyDescription)
    {
        foreach (var testCase in testCases)
        {
            property(testCase).ShouldBeTrue(
                $"Property '{propertyDescription}' should hold for {testCase}");
        }
    }
}