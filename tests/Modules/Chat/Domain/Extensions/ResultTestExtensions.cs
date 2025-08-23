using CSharpFunctionalExtensions;

namespace Axon.Modules.Chat.Domain.Tests.Extensions;

/// <summary>
/// Extension methods for testing Result patterns in the Chat Domain.
/// Provides fluent assertions and test utilities for functional programming patterns.
/// </summary>
public static class ResultTestExtensions
{
    /// <summary>
    /// Asserts that a Result is successful and extracts the value for further testing.
    /// </summary>
    public static T ExtractValue<T, TError>(this Result<T, TError> result)
        where TError : class
    {
        result.IsSuccess.ShouldBeTrue($"Expected successful result but got error: {result.Error}");
        return result.Value;
    }

    /// <summary>
    /// Asserts that a Result is a failure and extracts the error for further testing.
    /// </summary>
    public static TError ExtractError<T, TError>(this Result<T, TError> result)
        where TError : class
    {
        result.IsFailure.ShouldBeTrue($"Expected failed result but got success: {result.Value}");
        return result.Error;
    }

    /// <summary>
    /// Asserts that a Result is successful and applies additional assertions to the value.
    /// </summary>
    public static Result<T, TError> ShouldBeSuccessWithValue<T, TError>(
        this Result<T, TError> result, 
        Action<T> valueAssertions)
        where TError : class
    {
        var value = result.ExtractValue();
        valueAssertions(value);
        return result;
    }

    /// <summary>
    /// Asserts that a Result is a failure and applies additional assertions to the error.
    /// </summary>
    public static Result<T, TError> ShouldBeFailureWithError<T, TError>(
        this Result<T, TError> result, 
        Action<TError> errorAssertions)
        where TError : class
    {
        var error = result.ExtractError();
        errorAssertions(error);
        return result;
    }

    /// <summary>
    /// Chains multiple Result operations for testing compound scenarios.
    /// </summary>
    public static Result<TOut, TError> ThenDo<TIn, TOut, TError>(
        this Result<TIn, TError> result,
        Func<TIn, Result<TOut, TError>> nextOperation)
        where TError : class
    {
        return result.IsSuccess 
            ? nextOperation(result.Value) 
            : Result.Failure<TOut, TError>(result.Error);
    }

    /// <summary>
    /// Asserts that all Results in a collection are successful.
    /// </summary>
    public static void AllShouldBeSuccessful<T, TError>(this IEnumerable<Result<T, TError>> results)
        where TError : class
    {
        var resultList = results.ToList();
        var failures = resultList.Where(r => r.IsFailure).ToList();
        
        failures.ShouldBeEmpty($"Expected all results to be successful, but found {failures.Count} failures: " +
            string.Join(", ", failures.Select(f => f.Error?.ToString())));
    }

    /// <summary>
    /// Asserts that at least one Result in a collection is successful.
    /// </summary>
    public static void AtLeastOneShouldBeSuccessful<T, TError>(this IEnumerable<Result<T, TError>> results)
        where TError : class
    {
        var resultList = results.ToList();
        var successes = resultList.Where(r => r.IsSuccess).ToList();
        
        successes.ShouldNotBeEmpty("Expected at least one result to be successful, but all failed");
    }

    /// <summary>
    /// Asserts that all Results in a collection are failures.
    /// </summary>
    public static void AllShouldBeFailures<T, TError>(this IEnumerable<Result<T, TError>> results)
        where TError : class
    {
        var resultList = results.ToList();
        var successes = resultList.Where(r => r.IsSuccess).ToList();
        
        successes.ShouldBeEmpty($"Expected all results to be failures, but found {successes.Count} successes");
    }

    /// <summary>
    /// Extracts all successful values from a collection of Results.
    /// </summary>
    public static List<T> ExtractSuccessfulValues<T, TError>(this IEnumerable<Result<T, TError>> results)
        where TError : class
    {
        return results
            .Where(r => r.IsSuccess)
            .Select(r => r.Value)
            .ToList();
    }

    /// <summary>
    /// Extracts all errors from a collection of Results.
    /// </summary>
    public static List<TError> ExtractErrors<T, TError>(this IEnumerable<Result<T, TError>> results)
        where TError : class
    {
        return results
            .Where(r => r.IsFailure)
            .Select(r => r.Error)
            .ToList();
    }

    /// <summary>
    /// Tests that a Result operation is idempotent (produces the same result when called multiple times).
    /// </summary>
    public static void ShouldBeIdempotent<T, TError>(
        this Func<Result<T, TError>> operation, 
        int iterations = 3)
        where TError : class
    {
        var results = Enumerable.Range(0, iterations)
            .Select(_ => operation())
            .ToList();

        var firstResult = results.First();

        foreach (var result in results.Skip(1))
        {
            if (firstResult.IsSuccess)
            {
                result.IsSuccess.ShouldBeTrue("All iterations should have the same success/failure status");
                
                if (typeof(T).IsValueType || result.Value?.Equals(firstResult.Value) == true)
                {
                    result.Value.ShouldBe(firstResult.Value, "All successful iterations should return the same value");
                }
            }
            else
            {
                result.IsFailure.ShouldBeTrue("All iterations should have the same success/failure status");
            }
        }
    }
}