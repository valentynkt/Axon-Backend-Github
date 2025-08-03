using NUnit.Framework;
using Shouldly;

namespace Axon.Tests.Shared.TestBase;

/// <summary>
/// Base class for validation testing scenarios across API and Application layers.
/// Consolidates shared validation logic for both FastEndpoints (10K limits) and MediatR (4K limits).
/// </summary>
public abstract class ValidationTestBase : LondonSchoolTestBase
{
    /// <summary>
    /// Standard validation test for input size limits in FastEndpoints (10K character limit).
    /// </summary>
    /// <param name="input">Input string to validate</param>
    /// <param name="maxLength">Maximum allowed length (default: 10000 for API layer)</param>
    protected void ShouldValidateInputLength_Api(string input, int maxLength = 10000)
    {
        if (input.Length > maxLength)
        {
            var result = ValidateInput_Api(input);
            result.IsValid.ShouldBeFalse($"Input with {input.Length} characters should fail API validation (limit: {maxLength})");
            result.ErrorMessage.ShouldContain("length");
        }
        else
        {
            var result = ValidateInput_Api(input);
            result.IsValid.ShouldBeTrue($"Input with {input.Length} characters should pass API validation (limit: {maxLength})");
        }
    }

    /// <summary>
    /// Standard validation test for input size limits in Application layer (4K character limit).
    /// </summary>
    /// <param name="input">Input string to validate</param>
    /// <param name="maxLength">Maximum allowed length (default: 4000 for Application layer)</param>
    protected void ShouldValidateInputLength_Application(string input, int maxLength = 4000)
    {
        if (input.Length > maxLength)
        {
            var result = ValidateInput_Application(input);
            result.IsValid.ShouldBeFalse($"Input with {input.Length} characters should fail Application validation (limit: {maxLength})");
            result.ErrorMessage.ShouldContain("length");
        }
        else
        {
            var result = ValidateInput_Application(input);
            result.IsValid.ShouldBeTrue($"Input with {input.Length} characters should pass Application validation (limit: {maxLength})");
        }
    }

    /// <summary>
    /// Validates input at API layer (FastEndpoints) with 10K limit.
    /// Override in derived classes to implement specific validation logic.
    /// </summary>
    protected virtual ValidationResult ValidateInput_Api(string input)
    {
        // Default implementation - override in derived classes
        return new ValidationResult
        {
            IsValid = input.Length <= 10000,
            ErrorMessage = input.Length > 10000 ? "Input exceeds API length limit of 10000 characters" : null
        };
    }

    /// <summary>
    /// Validates input at Application layer (MediatR) with 4K limit.
    /// Override in derived classes to implement specific validation logic.
    /// </summary>
    protected virtual ValidationResult ValidateInput_Application(string input)
    {
        // Default implementation - override in derived classes
        return new ValidationResult
        {
            IsValid = input.Length <= 4000,
            ErrorMessage = input.Length > 4000 ? "Input exceeds Application length limit of 4000 characters" : null
        };
    }

    /// <summary>
    /// Helper method to generate test strings of specific lengths.
    /// </summary>
    protected static string GenerateStringOfLength(int length, char character = 'a')
    {
        return new string(character, length);
    }

    /// <summary>
    /// Common test cases for boundary value testing.
    /// </summary>
    protected static object[] BoundaryTestCases_Api => new object[]
    {
        new object[] { 9999, true, "Below API limit" },
        new object[] { 10000, true, "At API limit" },
        new object[] { 10001, false, "Above API limit" }
    };

    /// <summary>
    /// Common test cases for boundary value testing.
    /// </summary>
    protected static object[] BoundaryTestCases_Application => new object[]
    {
        new object[] { 3999, true, "Below Application limit" },
        new object[] { 4000, true, "At Application limit" },
        new object[] { 4001, false, "Above Application limit" }
    };
}

/// <summary>
/// Simple validation result container.
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
}