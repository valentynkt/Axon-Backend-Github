namespace Axon.Tests.Shared.Extensions;

/// <summary>
/// Extension methods for testing Result objects with improved readability
/// </summary>
public static class ResultTestExtensions
{
    /// <summary>
    /// Asserts that the Result is successful
    /// </summary>
    public static void ShouldBeSuccess<T>(this Result<T> result)
    {
        result.IsSuccess.ShouldBeTrue($"Expected result to be successful but it failed with error: {(result.IsFailure ? result.Error.Message : "none")}");
    }

    /// <summary>
    /// Asserts that the Result is successful and returns the value
    /// </summary>
    public static T ShouldBeSuccessWithValue<T>(this Result<T> result)
    {
        result.IsSuccess.ShouldBeTrue($"Expected result to be successful but it failed with error: {(result.IsFailure ? result.Error.Message : "none")}");
        return result.Value;
    }

    /// <summary>
    /// Asserts that the Result is a failure
    /// </summary>
    public static void ShouldBeFailure<T>(this Result<T> result)
    {
        result.IsFailure.ShouldBeTrue($"Expected result to be failure but it was successful with value: {(result.IsSuccess ? result.Value : "none")}");
    }

    /// <summary>
    /// Asserts that the Result is a failure with specific error type
    /// </summary>
    public static void ShouldBeFailureWith<T>(this Result<T> result, ErrorType expectedType)
    {
        result.ShouldBeFailure();
        result.Error.Type.ShouldBe(expectedType);
    }

    /// <summary>
    /// Asserts that the Result is a failure with specific error type and message
    /// </summary>
    public static void ShouldBeFailureWith<T>(this Result<T> result, ErrorType expectedType, string expectedMessage)
    {
        result.ShouldBeFailureWith(expectedType);
        result.Error.Message.ShouldBe(expectedMessage);
    }

    /// <summary>
    /// Asserts that the Result is a validation failure
    /// </summary>
    public static void ShouldBeValidationFailure<T>(this Result<T> result)
    {
        result.ShouldBeFailureWith(ErrorType.Validation);
    }

    /// <summary>
    /// Asserts that the Result is a validation failure with specific message
    /// </summary>
    public static void ShouldBeValidationFailure<T>(this Result<T> result, string expectedMessage)
    {
        result.ShouldBeFailureWith(ErrorType.Validation, expectedMessage);
    }

    /// <summary>
    /// Asserts that the Result is a not found failure
    /// </summary>
    public static void ShouldBeNotFound<T>(this Result<T> result)
    {
        result.ShouldBeFailureWith(ErrorType.NotFound);
    }

    /// <summary>
    /// Asserts that the Result is an external service failure
    /// </summary>
    public static void ShouldBeExternalServiceFailure<T>(this Result<T> result)
    {
        result.ShouldBeFailureWith(ErrorType.ExternalService);
    }

    /// <summary>
    /// Asserts that the Result is successful and the value satisfies a condition
    /// </summary>
    public static void ShouldBeSuccessAnd<T>(this Result<T> result, Action<T> valueAssertion)
    {
        result.IsSuccess.ShouldBeTrue($"Expected result to be successful but it failed with error: {(result.IsFailure ? result.Error.Message : "none")}");
        valueAssertion(result.Value);
    }

    /// <summary>
    /// Extension for non-generic Result
    /// </summary>
    public static void ShouldBeSuccess(this Result result)
    {
        result.IsSuccess.ShouldBeTrue($"Expected result to be successful but it failed with error: {(result.IsFailure ? result.Error.Message : "none")}");
    }

    /// <summary>
    /// Extension for non-generic Result
    /// </summary>
    public static void ShouldBeFailure(this Result result)
    {
        result.IsFailure.ShouldBeTrue("Expected result to be failure but it was successful");
    }

    /// <summary>
    /// Extension for non-generic Result with error type
    /// </summary>
    public static void ShouldBeFailureWith(this Result result, ErrorType expectedType)
    {
        result.ShouldBeFailure();
        result.Error.Type.ShouldBe(expectedType);
    }

    /// <summary>
    /// Extension for non-generic Result with error type and message
    /// </summary>
    public static void ShouldBeFailureWith(this Result result, ErrorType expectedType, string expectedMessage)
    {
        result.ShouldBeFailureWith(expectedType);
        result.Error.Message.ShouldBe(expectedMessage);
    }
}