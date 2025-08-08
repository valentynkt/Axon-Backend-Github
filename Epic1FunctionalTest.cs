using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional.Options;
using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Diagnostics.Errors;

namespace Epic1Test;

/// <summary>
/// Test class to verify Epic 1: Functional Foundation implementation
/// </summary>
public class Epic1FunctionalTest
{
    public static void Main()
    {
        Console.WriteLine("=== Epic 1: Functional Foundation Test ===");
        
        // Test 1: Result<T> basic operations
        TestResult();
        
        // Test 2: Option<T> basic operations
        TestOption();
        
        // Test 3: Unit type
        TestUnit();
        
        // Test 4: Error redirection
        TestError();
        
        // Test 5: Enhanced Where operations
        TestEnhancedWhere();
        
        Console.WriteLine("All Epic 1 tests completed successfully!");
    }
    
    private static void TestResult()
    {
        Console.WriteLine("\n--- Testing Result<T> ---");
        
        // Success case
        var successResult = Result<string>.Success("Hello World");
        Console.WriteLine($"Success result: IsSuccess={successResult.IsSuccess}, Value={successResult.Value}");
        
        // Failure case
        var error = Error.Validation("Test validation error");
        var failureResult = Result<string>.Failure(error);
        Console.WriteLine($"Failure result: IsFailure={failureResult.IsFailure}, Error={failureResult.Error.Message}");
        
        // Map operation
        var mappedResult = successResult.Map(s => s.Length);
        Console.WriteLine($"Mapped result: {mappedResult.Value}");
        
        // Implicit conversion
        Result<string> implicitResult = "Implicit conversion works";
        Console.WriteLine($"Implicit conversion: {implicitResult.Value}");
        
        // Pattern matching
        var matchResult = successResult.Match(
            success: value => $"Got: {value}",
            failure: err => $"Error: {err.Message}"
        );
        Console.WriteLine($"Pattern match: {matchResult}");
    }
    
    private static void TestOption()
    {
        Console.WriteLine("\n--- Testing Option<T> ---");
        
        // Some case
        var someOption = Option<int>.Some(42);
        Console.WriteLine($"Some option: IsSome={someOption.IsSome}, Value={someOption.Value}");
        
        // None case
        var noneOption = Option<int>.None();
        Console.WriteLine($"None option: IsNone={noneOption.IsNone}");
        
        // Pattern matching
        var matchResult = someOption.Match(
            some: value => $"Got: {value}",
            none: () => "Nothing here"
        );
        Console.WriteLine($"Pattern match: {matchResult}");
    }
    
    private static void TestUnit()
    {
        Console.WriteLine("\n--- Testing Unit ---");
        
        var unit1 = Unit.Value;
        var unit2 = new Unit();
        
        Console.WriteLine($"Unit equality: {unit1.Equals(unit2)}");
        Console.WriteLine($"Unit string: {unit1.ToString()}");
        
        // Implicit conversion
        Unit implicitUnit = "anything";
        Console.WriteLine($"Implicit conversion works: {implicitUnit.ToString()}");
        
        // Comparison operators
        Console.WriteLine($"Unit comparison: {unit1 <= unit2}");
    }
    
    private static void TestError()
    {
        Console.WriteLine("\n--- Testing Error redirection ---");
        
        var validationError = Error.Validation("Field is required");
        var notFoundError = Error.NotFound("Resource not found");
        var internalError = Error.Internal("Something went wrong");
        
        Console.WriteLine($"Validation error: {validationError.Type} - {validationError.Message}");
        Console.WriteLine($"NotFound error: {notFoundError.Type} - {notFoundError.Message}");
        Console.WriteLine($"Internal error: {internalError.Type} - {internalError.Message}");
        
        // From exception
        var exception = new ArgumentNullException("testParam");
        var errorFromException = Error.FromException(exception);
        Console.WriteLine($"Error from exception: {errorFromException.Type} - {errorFromException.Message}");
    }
    
    private static void TestEnhancedWhere()
    {
        Console.WriteLine("\n--- Testing Enhanced Where operations ---");
        
        var successResult = Result<int>.Success(42);
        
        // Where with predicate - passing
        var wherePass = successResult.Where(x => x > 30, "Value must be greater than 30");
        Console.WriteLine($"Where pass: IsSuccess={wherePass.IsSuccess}, Value={wherePass.Value}");
        
        // Where with predicate - failing
        var whereFail = successResult.Where(x => x > 50, "Value must be greater than 50");
        Console.WriteLine($"Where fail: IsFailure={whereFail.IsFailure}, Error={whereFail.Error.Message}");
        
        // Where with error factory
        var whereWithFactory = successResult.Where(
            x => x > 100, 
            x => Error.BusinessRule($"Value {x} is not acceptable")
        );
        Console.WriteLine($"Where with factory: IsFailure={whereWithFactory.IsFailure}, Error={whereWithFactory.Error.Message}");
    }
}