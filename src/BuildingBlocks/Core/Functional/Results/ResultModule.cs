// This file serves as a module index for the Results namespace
// It provides global using statements and type aliases for convenience

namespace BuildingBlocks.Core.Functional.Results;

/// <summary>
/// Module marker and utilities for the Results namespace.
/// Provides convenient factory methods and common patterns.
/// </summary>
public static class ResultModule
{
    /// <summary>
    /// Creates a successful result with value
    /// </summary>
    public static Result<T> Ok<T>(T value) => Result<T>.Success(value);
    
    /// <summary>
    /// Creates a successful result
    /// </summary>
    public static Result Ok() => Result.Success();
    
    /// <summary>
    /// Creates a failed result with error
    /// </summary>
    public static Result<T> Fail<T>(Error error) => Result<T>.Failure(error);
    
    /// <summary>
    /// Creates a failed result with error
    /// </summary>
    public static Result Fail(Error error) => Result.Failure(error);
    
    /// <summary>
    /// Creates a failed result with validation error
    /// </summary>
    public static Result<T> FailValidation<T>(string message, string code = "VALIDATION_ERROR") => 
        Result<T>.Failure(Error.Validation(message, code));
    
    /// <summary>
    /// Creates a failed result with validation error
    /// </summary>
    public static Result FailValidation(string message, string code = "VALIDATION_ERROR") => 
        Result.Failure(Error.Validation(message, code));
    
    /// <summary>
    /// Creates a failed result with not found error
    /// </summary>
    public static Result<T> FailNotFound<T>(string message, string code = "NOT_FOUND") => 
        Result<T>.Failure(Error.NotFound(message, code));
    
    /// <summary>
    /// Creates a failed result with not found error
    /// </summary>
    public static Result FailNotFound(string message, string code = "NOT_FOUND") => 
        Result.Failure(Error.NotFound(message, code));
}

/// <summary>
/// Common Result type aliases for convenience
/// </summary>
public static class ResultTypes
{
    /// <summary>
    /// Type alias for Result&lt;string&gt;
    /// </summary>
    public static class StringResult
    {
        public static Result<string> Success(string value) => Result<string>.Success(value);
        public static Result<string> Failure(Error error) => Result<string>.Failure(error);
    }
    
    /// <summary>
    /// Type alias for Result&lt;int&gt;
    /// </summary>
    public static class IntResult  
    {
        public static Result<int> Success(int value) => Result<int>.Success(value);
        public static Result<int> Failure(Error error) => Result<int>.Failure(error);
    }
    
    /// <summary>
    /// Type alias for Result&lt;bool&gt;
    /// </summary>
    public static class BoolResult
    {
        public static Result<bool> Success(bool value) => Result<bool>.Success(value);
        public static Result<bool> Failure(Error error) => Result<bool>.Failure(error);
        public static Result<bool> True() => Result<bool>.Success(true);
        public static Result<bool> False() => Result<bool>.Success(false);
    }
}