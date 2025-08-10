using BuildingBlocks.Core.Diagnostics.Errors;

namespace Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Extensions;

/// <summary>
/// Custom Shouldly assertions for Result pattern testing.
/// Provides fluent assertions for Result&lt;T&gt; and Error types.
/// </summary>
public static class ResultAssertions
{
    #region Result<T> Success Assertions

    /// <summary>
    /// Asserts that a Result is successful.
    /// </summary>
    public static void ShouldBeSuccess<T>(this Result<T> result, string? message = null)
    {
        result.IsSuccess.ShouldBeTrue(
            message ?? $"Expected successful result but got failure: {result.Error?.Message}");
    }

    /// <summary>
    /// Asserts that a Result is successful and returns the value.
    /// </summary>
    public static T ShouldBeSuccessWithValue<T>(this Result<T> result)
    {
        result.ShouldBeSuccess();
        result.Value.ShouldNotBeNull("Successful result should have a value");
        return result.Value;
    }

    /// <summary>
    /// Asserts that a Result is successful with a specific value.
    /// </summary>
    public static void ShouldBeSuccessWith<T>(this Result<T> result, T expectedValue)
    {
        result.ShouldBeSuccess();
        result.Value.ShouldBe(expectedValue);
    }

    /// <summary>
    /// Asserts that a Result is successful and validates the value.
    /// </summary>
    public static T ShouldBeSuccessWith<T>(this Result<T> result, Action<T> valueAssertion)
    {
        result.ShouldBeSuccess();
        result.Value.ShouldNotBeNull();
        valueAssertion(result.Value);
        return result.Value;
    }

    #endregion

    #region Result<T> Failure Assertions

    /// <summary>
    /// Asserts that a Result is a failure.
    /// </summary>
    public static void ShouldBeFailure<T>(this Result<T> result, string? message = null)
    {
        result.IsFailure.ShouldBeTrue(
            message ?? "Expected failure result but got success");
    }

    /// <summary>
    /// Asserts that a Result is a failure with a specific error code.
    /// </summary>
    public static void ShouldBeFailureWithCode<T>(this Result<T> result, string expectedCode)
    {
        result.ShouldBeFailure();
        result.Error.ShouldNotBeNull();
        result.Error.Code.ShouldBe(expectedCode,
            $"Expected error code '{expectedCode}' but got '{result.Error.Code}'");
    }

    /// <summary>
    /// Asserts that a Result is a failure with a specific error type.
    /// </summary>
    public static void ShouldBeFailureWithType<T>(this Result<T> result, ErrorType expectedType)
    {
        result.ShouldBeFailure();
        result.Error.ShouldNotBeNull();
        result.Error.Type.ShouldBe(expectedType,
            $"Expected error type '{expectedType}' but got '{result.Error.Type}'");
    }

    /// <summary>
    /// Asserts that a Result is a failure and validates the error.
    /// </summary>
    public static Error ShouldBeFailureWith<T>(this Result<T> result, Action<Error> errorAssertion)
    {
        result.ShouldBeFailure();
        result.Error.ShouldNotBeNull();
        errorAssertion(result.Error);
        return result.Error;
    }

    /// <summary>
    /// Asserts that a Result is a validation failure.
    /// </summary>
    public static void ShouldBeValidationFailure<T>(this Result<T> result, string? expectedMessage = null)
    {
        result.ShouldBeFailureWithType(ErrorType.Validation);
        
        if (expectedMessage != null)
        {
            result.Error.Message.ShouldContain(expectedMessage);
        }
    }

    /// <summary>
    /// Asserts that a Result is a not found failure.
    /// </summary>
    public static void ShouldBeNotFoundFailure<T>(this Result<T> result, string? expectedMessage = null)
    {
        result.ShouldBeFailureWithType(ErrorType.NotFound);
        
        if (expectedMessage != null)
        {
            result.Error.Message.ShouldContain(expectedMessage);
        }
    }

    /// <summary>
    /// Asserts that a Result is a conflict failure.
    /// </summary>
    public static void ShouldBeConflictFailure<T>(this Result<T> result, string? expectedMessage = null)
    {
        result.ShouldBeFailureWithType(ErrorType.Conflict);
        
        if (expectedMessage != null)
        {
            result.Error.Message.ShouldContain(expectedMessage);
        }
    }

    #endregion

    #region Result (non-generic) Assertions

    /// <summary>
    /// Asserts that a Result is successful.
    /// </summary>
    public static void ShouldBeSuccess(this Result result, string? message = null)
    {
        result.IsSuccess.ShouldBeTrue(
            message ?? $"Expected successful result but got failure: {result.Error?.Message}");
    }

    /// <summary>
    /// Asserts that a Result is a failure.
    /// </summary>
    public static void ShouldBeFailure(this Result result, string? message = null)
    {
        result.IsFailure.ShouldBeTrue(
            message ?? "Expected failure result but got success");
    }

    /// <summary>
    /// Asserts that a Result is a failure with a specific error code.
    /// </summary>
    public static void ShouldBeFailureWithCode(this Result result, string expectedCode)
    {
        result.ShouldBeFailure();
        result.Error.ShouldNotBeNull();
        result.Error.Code.ShouldBe(expectedCode);
    }

    #endregion

    #region Error Assertions

    /// <summary>
    /// Asserts that an Error has specific properties.
    /// </summary>
    public static void ShouldBeError(
        this Error error,
        string expectedCode,
        ErrorType expectedType,
        string? messageContains = null)
    {
        error.Code.ShouldBe(expectedCode);
        error.Type.ShouldBe(expectedType);
        
        if (messageContains != null)
        {
            error.Message.ShouldContain(messageContains);
        }
    }

    /// <summary>
    /// Asserts that an Error is a validation error.
    /// </summary>
    public static void ShouldBeValidationError(this Error error, string expectedCode)
    {
        error.Type.ShouldBe(ErrorType.Validation);
        error.Code.ShouldBe(expectedCode);
    }

    /// <summary>
    /// Asserts that an Error is a not found error.
    /// </summary>
    public static void ShouldBeNotFoundError(this Error error, string? entityName = null)
    {
        error.Type.ShouldBe(ErrorType.NotFound);
        
        if (entityName != null)
        {
            error.Message.ShouldContain(entityName);
        }
    }

    /// <summary>
    /// Asserts that an Error has metadata.
    /// </summary>
    public static void ShouldHaveMetadata(this Error error, string key, object expectedValue)
    {
        error.Metadata.ShouldNotBeNull("Error should have metadata");
        error.Metadata.ShouldContainKey(key);
        error.Metadata[key].ShouldBe(expectedValue);
    }

    #endregion

    #region Chaining Assertions

    /// <summary>
    /// Chains Result assertions for fluent testing.
    /// </summary>
    public static Result<T> And<T>(this Result<T> result)
    {
        return result;
    }

    /// <summary>
    /// Chains Result assertions with value extraction.
    /// </summary>
    public static T ThenValue<T>(this Result<T> result)
    {
        result.ShouldBeSuccess();
        return result.Value;
    }

    /// <summary>
    /// Chains Result assertions with error extraction.
    /// </summary>
    public static Error ThenError<T>(this Result<T> result)
    {
        result.ShouldBeFailure();
        return result.Error;
    }

    #endregion

    #region Async Result Assertions

    /// <summary>
    /// Asserts that an async Result is successful.
    /// </summary>
    public static async Task ShouldBeSuccessAsync<T>(this Task<Result<T>> resultTask)
    {
        var result = await resultTask;
        result.ShouldBeSuccess();
    }

    /// <summary>
    /// Asserts that an async Result is successful with value validation.
    /// </summary>
    public static async Task<T> ShouldBeSuccessWithAsync<T>(
        this Task<Result<T>> resultTask,
        Action<T> valueAssertion)
    {
        var result = await resultTask;
        return result.ShouldBeSuccessWith(valueAssertion);
    }

    /// <summary>
    /// Asserts that an async Result is a failure.
    /// </summary>
    public static async Task ShouldBeFailureAsync<T>(this Task<Result<T>> resultTask)
    {
        var result = await resultTask;
        result.ShouldBeFailure();
    }

    /// <summary>
    /// Asserts that an async Result is a failure with error validation.
    /// </summary>
    public static async Task<Error> ShouldBeFailureWithAsync<T>(
        this Task<Result<T>> resultTask,
        Action<Error> errorAssertion)
    {
        var result = await resultTask;
        return result.ShouldBeFailureWith(errorAssertion);
    }

    #endregion

    #region Result Combination Assertions

    /// <summary>
    /// Asserts that all Results in a collection are successful.
    /// </summary>
    public static void ShouldAllBeSuccess<T>(this IEnumerable<Result<T>> results)
    {
        var resultList = results.ToList();
        var failures = resultList.Where(r => r.IsFailure).ToList();
        
        failures.ShouldBeEmpty(
            $"Expected all results to be successful but {failures.Count} failed: {string.Join(", ", failures.Select(f => f.Error.Message))}");
    }

    /// <summary>
    /// Asserts that at least one Result in a collection is successful.
    /// </summary>
    public static void ShouldHaveAnySuccess<T>(this IEnumerable<Result<T>> results)
    {
        var hasSuccess = results.Any(r => r.IsSuccess);
        hasSuccess.ShouldBeTrue("Expected at least one successful result");
    }

    #endregion
}