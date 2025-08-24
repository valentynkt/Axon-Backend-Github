namespace Axon.Modules.Chat.Domain.Tests.Common;

/// <summary>
/// Test helpers for business rule validation in the Chat Domain.
/// Provides utilities for testing business rule violations, error messages, and rule behavior.
/// </summary>
public static class BusinessRuleTestHelpers
{
    /// <summary>
    /// Asserts that a business rule is broken (returns true from IsBroken()).
    /// </summary>
    public static void AssertRuleIsBroken<TRule>(TRule rule, string? customMessage = null)
        where TRule : IBusinessRule
    {
        rule.IsBroken().ShouldBeTrue(customMessage ?? $"Business rule {typeof(TRule).Name} should be broken but was not");
    }

    /// <summary>
    /// Asserts that a business rule is not broken (returns false from IsBroken()).
    /// </summary>
    public static void AssertRuleIsNotBroken<TRule>(TRule rule, string? customMessage = null)
        where TRule : IBusinessRule
    {
        rule.IsBroken().ShouldBeFalse(customMessage ?? $"Business rule {typeof(TRule).Name} should not be broken but was");
    }

    /// <summary>
    /// Asserts that a business rule has the expected error message.
    /// </summary>
    public static void AssertRuleHasMessage<TRule>(TRule rule, string expectedMessage)
        where TRule : IBusinessRule
    {
        rule.Message.ShouldBe(expectedMessage, 
            $"Business rule {typeof(TRule).Name} should have message '{expectedMessage}' but had '{rule.Message}'");
    }

    /// <summary>
    /// Asserts that a business rule error message contains expected text.
    /// </summary>
    public static void AssertRuleMessageContains<TRule>(TRule rule, string expectedText)
        where TRule : IBusinessRule
    {
        rule.Message.ShouldContain(expectedText, Case.Insensitive,
            $"Business rule {typeof(TRule).Name} message should contain '{expectedText}' but was '{rule.Message}'");
    }

    /// <summary>
    /// Asserts that an operation throws a BusinessRuleException with a specific rule type.
    /// Returns the broken rule for further assertions.
    /// </summary>
    public static void AssertThrowsBusinessRuleException<TRule>(Action operation, string? customMessage = null)
        where TRule : IBusinessRule
    {
        var exception = Should.Throw<BusinessRuleException>(operation, 
            customMessage ?? $"Expected {typeof(TRule).Name} business rule violation");
        
        var expectedRuleCode = typeof(TRule).Name;
        exception.RuleCode.ShouldBe(expectedRuleCode,
            $"Expected broken rule to be {expectedRuleCode} but was {exception.RuleCode}");
    }

    /// <summary>
    /// Asserts that an operation throws a BusinessRuleException with expected message.
    /// </summary>
    public static void AssertThrowsBusinessRuleWithMessage(Action operation, string expectedMessage)
    {
        var exception = Should.Throw<BusinessRuleException>(operation);
        exception.Message.ShouldContain(expectedMessage, Case.Insensitive,
            $"Business rule exception message should contain '{expectedMessage}' but was '{exception.Message}'");
    }

    /// <summary>
    /// Tests multiple scenarios where a business rule should be violated.
    /// </summary>
    public static void AssertRuleFails<TRule>(Func<TRule> ruleFactory, params object[] invalidParameters)
        where TRule : IBusinessRule
    {
        foreach (var parameter in invalidParameters)
        {
            var rule = ruleFactory();
            AssertRuleIsBroken(rule, $"Rule should be broken for parameter: {parameter}");
        }
    }

    /// <summary>
    /// Tests multiple scenarios where a business rule should pass.
    /// </summary>
    public static void AssertRulePasses<TRule>(Func<TRule> ruleFactory, params object[] validParameters)
        where TRule : IBusinessRule
    {
        foreach (var parameter in validParameters)
        {
            var rule = ruleFactory();
            AssertRuleIsNotBroken(rule, $"Rule should not be broken for parameter: {parameter}");
        }
    }

    /// <summary>
    /// Creates a test scenario for validating business rule edge cases.
    /// </summary>
    public static void TestRuleEdgeCases<TRule>(
        Func<object, TRule> ruleFactory,
        object[] validCases,
        object[] invalidCases,
        string? scenarioDescription = null)
        where TRule : IBusinessRule
    {
        var description = scenarioDescription ?? typeof(TRule).Name;

        // Test valid cases
        foreach (var validCase in validCases)
        {
            var rule = ruleFactory(validCase);
            AssertRuleIsNotBroken(rule, $"{description}: Valid case '{validCase}' should pass");
        }

        // Test invalid cases
        foreach (var invalidCase in invalidCases)
        {
            var rule = ruleFactory(invalidCase);
            AssertRuleIsBroken(rule, $"{description}: Invalid case '{invalidCase}' should fail");
        }
    }

    /// <summary>
    /// Tests that a business rule provides consistent results across multiple evaluations.
    /// </summary>
    public static void AssertRuleIsConsistent<TRule>(TRule rule, int iterations = 5)
        where TRule : IBusinessRule
    {
        var firstResult = rule.IsBroken();
        var firstMessage = rule.Message;

        for (int i = 1; i < iterations; i++)
        {
            var currentResult = rule.IsBroken();
            var currentMessage = rule.Message;

            currentResult.ShouldBe(firstResult, 
                $"Business rule {typeof(TRule).Name} should return consistent results across evaluations");
            currentMessage.ShouldBe(firstMessage, 
                $"Business rule {typeof(TRule).Name} should return consistent messages across evaluations");
        }
    }

    /// <summary>
    /// Asserts that a business rule provides meaningful error information.
    /// </summary>
    public static void AssertRuleHasMeaningfulError<TRule>(TRule rule)
        where TRule : IBusinessRule
    {
        if (rule.IsBroken())
        {
            rule.Message.ShouldNotBeNullOrWhiteSpace(
                $"Business rule {typeof(TRule).Name} should provide a meaningful error message when broken");
            
            rule.Message.Length.ShouldBeGreaterThan(10, 
                $"Business rule {typeof(TRule).Name} error message should be descriptive");
        }
    }

    /// <summary>
    /// Factory methods for creating common business rule test scenarios.
    /// </summary>
    public static class TestScenarios
    {
        /// <summary>
        /// Creates test data for string length validation rules.
        /// </summary>
        public static (string[] valid, string[] invalid) CreateStringLengthTestData(
            int maxLength, 
            bool allowEmpty = false)
        {
            var valid = new List<string>
            {
                "Valid string",
                new string('a', maxLength),
                new string('x', maxLength - 1)
            };

            var invalid = new List<string>
            {
                new string('a', maxLength + 1),
                new string('x', maxLength + 100)
            };

            if (allowEmpty)
            {
                valid.Add(string.Empty);
            }
            else
            {
                invalid.Add(string.Empty);
                invalid.Add("   "); // whitespace only
            }

            return (valid.ToArray(), invalid.ToArray());
        }

        /// <summary>
        /// Creates test data for numeric range validation rules.
        /// </summary>
        public static (int[] valid, int[] invalid) CreateRangeTestData(int min, int max)
        {
            var valid = new[] { min, max, (min + max) / 2 };
            var invalid = new[] { min - 1, max + 1, int.MinValue, int.MaxValue };

            return (valid, invalid);
        }

        /// <summary>
        /// Creates test data for collection size validation rules.
        /// </summary>
        public static (int[] valid, int[] invalid) CreateCollectionSizeTestData(int maxSize)
        {
            var valid = new[] { 0, 1, maxSize / 2, maxSize };
            var invalid = new[] { maxSize + 1, maxSize + 100, int.MaxValue };

            return (valid, invalid);
        }
    }
}