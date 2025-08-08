using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional.Exceptions;
using BuildingBlocks.Core.Functional.Railway;

namespace BuildingBlocks.Test;

/// <summary>
/// Simple test to verify Result pattern components compile and work correctly
/// </summary>
public class TestResultPattern
{
    public static void Main()
    {
        Console.WriteLine("Testing Result Pattern Components...");

        // Test basic Result operations
        TestBasicResult();
        
        // Test exception mapping
        TestExceptionMapping();
        
        // Test railway extensions
        TestRailwayExtensions();
        
        Console.WriteLine("All tests passed!");
    }

    private static void TestBasicResult()
    {
        Console.WriteLine("Testing basic Result operations...");
        
        // Success case
        var successResult = Result<string>.Success("Hello World");
        Console.WriteLine($"Success: {successResult.IsSuccess}, Value: {successResult.Value}");
        
        // Failure case
        var failureResult = Result<string>.Failure(Error.Validation("Test error"));
        Console.WriteLine($"Failure: {failureResult.IsFailure}, Error: {failureResult.Error.Message}");
        
        // Map operation
        var mappedResult = successResult.Map(s => s.Length);
        Console.WriteLine($"Mapped result: {mappedResult.Value}");
        
        // Bind operation
        var boundResult = successResult.Bind(s => 
            s.Length > 5 ? Result<int>.Success(s.Length) : Result<int>.Failure(Error.Validation("Too short")));
        Console.WriteLine($"Bound result: {boundResult.Value}");
    }

    private static void TestExceptionMapping()
    {
        Console.WriteLine("Testing exception mapping...");
        
        // Convert Result to Exception
        var failedResult = Result<string>.Failure(Error.NotFound("Resource not found"));
        var exception = failedResult.ToException();
        Console.WriteLine($"Exception type: {exception?.GetType().Name}, Message: {exception?.Message}");
        
        // Convert Exception back to Result
        var argumentException = new ArgumentException("Invalid argument");
        var resultFromException = argumentException.ToResult<string>();
        Console.WriteLine($"Result from exception - IsFailure: {resultFromException.IsFailure}, Error type: {resultFromException.Error.Type}");
        
        // Try/Execute pattern
        var safeResult = ResultExceptionMapper.TryExecute(() =>
        {
            if (DateTime.Now.Millisecond % 2 == 0)
                return "Success!";
            else
                throw new InvalidOperationException("Random failure");
        });
        
        Console.WriteLine($"Safe execution result: {safeResult.IsSuccess}");
        if (safeResult.IsSuccess)
            Console.WriteLine($"Value: {safeResult.Value}");
        else
            Console.WriteLine($"Error: {safeResult.Error.Message}");
    }

    private static void TestRailwayExtensions()
    {
        Console.WriteLine("Testing railway extensions...");
        
        // Sequence of results
        var results = new[]
        {
            Result<int>.Success(1),
            Result<int>.Success(2),
            Result<int>.Success(3)
        };
        
        var sequenced = results.Sequence();
        Console.WriteLine($"Sequenced results - Success: {sequenced.IsSuccess}, Count: {sequenced.Value.Count}");
        
        // Pipeline operations
        var pipelineResult = Result<int>.Success(5)
            .Pipeline(
                x => Result<int>.Success(x * 2),
                x => Result<int>.Success(x + 1),
                x => x > 10 ? Result<int>.Success(x) : Result<int>.Failure(Error.Validation("Too small"))
            );
        
        Console.WriteLine($"Pipeline result: {pipelineResult.Value}"); // Should be 11: (5 * 2) + 1 = 11
        
        // Traverse operation
        var numbers = new[] { 1, 2, 3, 4, 5 };
        var traversed = numbers.Traverse(n => 
            n % 2 == 0 ? Result<string>.Success($"Even: {n}") : Result<string>.Success($"Odd: {n}"));
        
        Console.WriteLine($"Traversed results - Success: {traversed.IsSuccess}, Count: {traversed.Value.Count}");
        foreach (var item in traversed.Value)
        {
            Console.WriteLine($"  - {item}");
        }
    }
}