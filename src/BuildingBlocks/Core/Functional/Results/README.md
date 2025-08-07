# Result<T> Pattern Implementation

This implementation provides a comprehensive Result<T> pattern for the Axon Backend project, inspired by functional programming concepts and best practices from FluentResults and TypeScript Result libraries.

## Overview

The Result<T> pattern is used to represent operations that can either succeed with a value or fail with an error, eliminating the need for exceptions in business logic and providing better type safety.

## Key Features

- **Strong Type Safety**: Results are strongly typed with clear success/failure states
- **Fluent API**: Method chaining for complex operations (Map, Bind, OnSuccess, OnFailure)
- **Async Support**: Full async/await support with extension methods
- **Validation Extensions**: Built-in validation patterns and extensions
- **Error Categorization**: Rich error types with metadata support
- **CQRS Integration**: Designed to work seamlessly with CQRS patterns

## Basic Usage

### Creating Results

```csharp
// Success results
var success = Result.Success();
var successWithValue = Result<string>.Success("Hello World");

// Failure results
var failure = Result.Failure(Error.Validation("Invalid input"));
var failureWithValue = Result<string>.Failure(Error.NotFound("User not found"));

// Using implicit conversions
Result<string> result1 = "Hello World"; // Success
Result<string> result2 = Error.Validation("Invalid"); // Failure
```

### Exception Handling

```csharp
// Wrap potentially throwing operations
var result = Result.Try(() => int.Parse("123"));
var asyncResult = await Result.TryAsync(async () => await SomeAsyncOperation());

// With custom error handling
var customResult = Result.Try(
    () => RiskyOperation(),
    ex => Error.ExternalService("Operation failed", innerException: ex)
);
```

### Method Chaining

```csharp
var result = Result<string>.Success("user@example.com")
    .EnsureNotNullOrWhiteSpace("Email is required")
    .Ensure(email => email.Contains("@"), "Invalid email format")
    .Map(email => email.ToLowerInvariant())
    .Bind(email => ValidateEmailAsync(email))
    .OnSuccess(email => _logger.LogInformation("Valid email: {Email}", email))
    .OnFailure(error => _logger.LogError("Validation failed: {Error}", error));
```

## Repository Pattern Integration

### Repository Interface

```csharp
public interface IUserRepository
{
    Task<Result<User?>> GetByIdAsync(UserId id, CancellationToken cancellationToken = default);
    Task<Result> AddAsync(User user, CancellationToken cancellationToken = default);
    Task<Result> UpdateAsync(User user, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(UserId id, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<User>>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
}
```

### Repository Implementation

```csharp
public class UserRepository : IUserRepository
{
    public async Task<Result<User?>> GetByIdAsync(UserId id, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _context.Users.FindAsync(id.Value, cancellationToken);
            return Result<User?>.Success(user);
        }
        catch (Exception ex)
        {
            return Result<User?>.Failure(Error.Persistence("Failed to retrieve user", innerException: ex));
        }
    }

    public async Task<Result> AddAsync(User user, CancellationToken cancellationToken = default)
    {
        try
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(Error.Persistence("Failed to add user", innerException: ex));
        }
    }
}
```

## CQRS Pattern Integration

### Command Handler

```csharp
public class CreateUserHandler : IRequestHandler<CreateUserCommand, Result<CreateUserResponse>>
{
    private readonly IUserRepository _userRepository;
    
    public async Task<Result<CreateUserResponse>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        // Validate input
        var validationResult = ValidateCreateUserCommand(request);
        if (validationResult.IsFailure)
            return validationResult.Error;

        // Create user
        var userResult = User.Create(request.Email, request.FirstName, request.LastName);
        if (userResult.IsFailure)
            return userResult.Error;

        // Save user
        var saveResult = await _userRepository.AddAsync(userResult.Value, cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error;

        // Return response
        var response = new CreateUserResponse(userResult.Value.Id.Value, userResult.Value.Email);
        return Result<CreateUserResponse>.Success(response);
    }
}
```

### Query Handler

```csharp
public class GetUserHandler : IRequestHandler<GetUserQuery, Result<UserDto>>
{
    private readonly IUserRepository _userRepository;
    
    public async Task<Result<UserDto>> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        var userResult = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (userResult.IsFailure)
            return userResult.Error;
            
        if (userResult.Value is null)
            return Error.NotFound($"User with ID {request.UserId} not found");
            
        var userDto = new UserDto(userResult.Value.Id.Value, userResult.Value.Email, userResult.Value.FullName);
        return Result<UserDto>.Success(userDto);
    }
}
```

## Async Operations

```csharp
// Chain async operations
var result = await GetUserAsync(userId)
    .BindAsync(user => UpdateUserEmailAsync(user, newEmail))
    .MapAsync(user => MapToUserDto(user))
    .OnSuccessAsync(dto => LogUserUpdateAsync(dto))
    .OnFailureAsync(error => LogErrorAsync(error));

// Combine multiple async results
var combinedResult = await Result.CombineAsync(
    ValidateUserAsync(request.UserId),
    CheckPermissionsAsync(request.UserId, Permission.UpdateUser),
    ValidateEmailFormatAsync(request.NewEmail)
);
```

## Validation Extensions

```csharp
var validatedResult = Result<string>.Success(userInput)
    .EnsureNotNullOrWhiteSpace("Input cannot be empty")
    .EnsureMinLength(3, "Input must be at least 3 characters")
    .EnsureMaxLength(100, "Input cannot exceed 100 characters")
    .Ensure(input => !input.Contains("<"), "Input cannot contain HTML")
    .ValidateDataAnnotations(); // Uses System.ComponentModel.DataAnnotations
```

## Error Handling Patterns

### Error Types

```csharp
// Different error types for different scenarios
Error.Validation("Email format is invalid");
Error.NotFound("User not found");
Error.Conflict("User already exists");
Error.BusinessRule("Cannot delete user with active orders");
Error.Persistence("Database connection failed");
Error.ExternalService("Payment service unavailable");
Error.Unauthorized("Invalid credentials");
Error.Forbidden("Insufficient permissions");
```

### Error Metadata

```csharp
var error = Error.Validation("Invalid age")
    .WithMetadata("Field", "Age")
    .WithMetadata("MinValue", 0)
    .WithMetadata("MaxValue", 120)
    .WithMetadata("ActualValue", request.Age);
```

## Performance Considerations

- Results are implemented as `readonly record struct` for minimal memory allocation
- Method chaining is optimized to avoid unnecessary allocations
- Async operations use proper ConfigureAwait patterns
- Error objects are cached where appropriate

## Integration with Existing Code

The Result pattern is designed to integrate seamlessly with:

- **MediatR**: Commands and queries return `Result<T>`
- **Repository Pattern**: All repository methods return `Result<T>`
- **Domain Services**: Business logic operations return `Result<T>`
- **API Controllers**: Results can be easily mapped to HTTP responses
- **Validation**: FluentValidation and Data Annotations integration

## Best Practices

1. **Use Result<T> for all operations that can fail**: Don't throw exceptions for expected failures
2. **Chain operations when possible**: Use Map/Bind for transformation chains
3. **Handle errors at boundaries**: Convert Results to appropriate responses at API/UI boundaries
4. **Use specific error types**: Choose appropriate ErrorType for better error handling
5. **Include context in errors**: Use metadata to provide debugging information
6. **Validate early**: Use validation extensions to catch issues early
7. **Log appropriately**: Use OnSuccess/OnFailure for logging without breaking chains

## Migration from Existing Code

To migrate existing code to use the Result pattern:

1. Replace throwing methods with Result-returning methods
2. Update repository interfaces to return `Result<T>`
3. Update command/query handlers to return `Result<T>`
4. Use Result.Try() to wrap existing throwing operations
5. Update error handling to use pattern matching or Match() method

## Examples

See the example implementations in the Chat module for comprehensive usage patterns with domain entities, repositories, and CQRS handlers.