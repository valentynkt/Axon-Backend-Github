namespace Axon.Modules.Chat.Domain.Tests.Common;

/// <summary>
/// Base class for testing value objects in the Chat Domain.
/// Provides specialized utilities for testing value object equality, immutability, and validation.
/// </summary>
/// <typeparam name="TValueObject">The value object type being tested</typeparam>
public abstract class ValueObjectTestBase<TValueObject> : DomainTestBase
    where TValueObject : struct
{
    /// <summary>
    /// Asserts that two value objects are equal using both Equals() and == operator.
    /// </summary>
    protected void AssertValueObjectsEqual(TValueObject first, TValueObject second)
    {
        first.Equals(second).ShouldBeTrue("Value objects should be equal using Equals()");
        first.ShouldBe(second, "Value objects should be equal using ShouldBe()");
        
        // Test operator overloads if available
        if (HasEqualityOperator())
        {
            var equalityMethod = typeof(TValueObject).GetMethod("op_Equality", new[] { typeof(TValueObject), typeof(TValueObject) });
            var inequalityMethod = typeof(TValueObject).GetMethod("op_Inequality", new[] { typeof(TValueObject), typeof(TValueObject) });
            
            if (equalityMethod != null)
            {
                var equalResult = (bool)equalityMethod.Invoke(null, new object[] { first, second })!;
                equalResult.ShouldBeTrue("Value objects should be equal using == operator");
            }
            
            if (inequalityMethod != null)
            {
                var inequalResult = (bool)inequalityMethod.Invoke(null, new object[] { first, second })!;
                inequalResult.ShouldBeFalse("Value objects should not be unequal using != operator");
            }
        }
    }

    /// <summary>
    /// Asserts that two value objects are not equal.
    /// </summary>
    protected void AssertValueObjectsNotEqual(TValueObject first, TValueObject second)
    {
        first.Equals(second).ShouldBeFalse("Value objects should not be equal using Equals()");
        first.ShouldNotBe(second, "Value objects should not be equal using ShouldNotBe()");
        
        // Test operator overloads if available
        if (HasEqualityOperator())
        {
            var equalityMethod = typeof(TValueObject).GetMethod("op_Equality", new[] { typeof(TValueObject), typeof(TValueObject) });
            var inequalityMethod = typeof(TValueObject).GetMethod("op_Inequality", new[] { typeof(TValueObject), typeof(TValueObject) });
            
            if (equalityMethod != null)
            {
                var equalResult = (bool)equalityMethod.Invoke(null, new object[] { first, second })!;
                equalResult.ShouldBeFalse("Value objects should not be equal using == operator");
            }
            
            if (inequalityMethod != null)
            {
                var inequalResult = (bool)inequalityMethod.Invoke(null, new object[] { first, second })!;
                inequalResult.ShouldBeTrue("Value objects should be unequal using != operator");
            }
        }
    }

    /// <summary>
    /// Asserts that a value object has consistent hash codes for equal instances.
    /// </summary>
    protected void AssertConsistentHashCodes(TValueObject first, TValueObject second)
    {
        if (first.Equals(second))
        {
            first.GetHashCode().ShouldBe(second.GetHashCode(), 
                "Equal value objects must have the same hash code");
        }
    }

    /// <summary>
    /// Tests the reflexive property of equality (x.Equals(x) should be true).
    /// </summary>
    protected void AssertReflexiveEquality(TValueObject valueObject)
    {
        valueObject.Equals(valueObject).ShouldBeTrue("Value object should equal itself (reflexive)");
        
        if (HasEqualityOperator())
        {
            var equalityMethod = typeof(TValueObject).GetMethod("op_Equality", new[] { typeof(TValueObject), typeof(TValueObject) });
            if (equalityMethod != null)
            {
                var equalResult = (bool)equalityMethod.Invoke(null, new object[] { valueObject, valueObject })!;
                equalResult.ShouldBeTrue("Value object should equal itself using == operator");
            }
        }
    }

    /// <summary>
    /// Tests the symmetric property of equality (x.Equals(y) == y.Equals(x)).
    /// </summary>
    protected void AssertSymmetricEquality(TValueObject first, TValueObject second)
    {
        var firstEqualsSecond = first.Equals(second);
        var secondEqualsFirst = second.Equals(first);
        
        firstEqualsSecond.ShouldBe(secondEqualsFirst, 
            "Equality should be symmetric: first.Equals(second) == second.Equals(first)");
    }

    /// <summary>
    /// Tests the transitive property of equality (if x.Equals(y) and y.Equals(z), then x.Equals(z)).
    /// </summary>
    protected void AssertTransitiveEquality(TValueObject first, TValueObject second, TValueObject third)
    {
        if (first.Equals(second) && second.Equals(third))
        {
            first.Equals(third).ShouldBeTrue(
                "Equality should be transitive: if first equals second and second equals third, then first should equal third");
        }
    }

    /// <summary>
    /// Performs comprehensive equality testing for the value object.
    /// </summary>
    protected void AssertValueObjectEqualityContract(TValueObject valueObject)
    {
        // Reflexive
        AssertReflexiveEquality(valueObject);
        
        // Hash code consistency
        var hashCode1 = valueObject.GetHashCode();
        var hashCode2 = valueObject.GetHashCode();
        hashCode1.ShouldBe(hashCode2, "Hash code should be consistent across multiple calls");
        
        // ToString should not throw
        Should.NotThrow(() => valueObject.ToString());
    }

    /// <summary>
    /// Asserts that a value object's ToString() method returns a meaningful representation.
    /// </summary>
    protected void AssertMeaningfulToString(TValueObject valueObject, string? expectedContent = null)
    {
        var toString = valueObject.ToString();
        
        toString.ShouldNotBeNull("ToString() should not return null");
        toString.ShouldNotBeEmpty("ToString() should not return empty string");
        
        if (expectedContent != null)
        {
            toString.ShouldContain(expectedContent, Case.Insensitive, $"ToString() should contain '{expectedContent}'");
        }
    }

    /// <summary>
    /// Checks if the value object type has equality operators defined.
    /// </summary>
    private static bool HasEqualityOperator()
    {
        var type = typeof(TValueObject);
        return type.GetMethod("op_Equality", new[] { type, type }) != null;
    }

    /// <summary>
    /// Creates multiple instances of the value object with the same value for equality testing.
    /// Override in derived classes to provide specific test instances.
    /// </summary>
    protected virtual TValueObject[] CreateEqualInstances()
    {
        // Default implementation - override in derived classes
        return Array.Empty<TValueObject>();
    }

    /// <summary>
    /// Creates multiple instances of the value object with different values for inequality testing.
    /// Override in derived classes to provide specific test instances.
    /// </summary>
    protected virtual TValueObject[] CreateUnequalInstances()
    {
        // Default implementation - override in derived classes
        return Array.Empty<TValueObject>();
    }
}