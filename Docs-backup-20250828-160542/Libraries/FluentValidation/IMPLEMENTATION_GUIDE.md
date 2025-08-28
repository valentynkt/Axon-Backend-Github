# FluentValidation 12.0.0 Implementation Guide for Axon Backend

## Overview

FluentValidation is a .NET validation library that uses a fluent interface and lambda expressions for building strongly-typed validation rules. This guide covers FluentValidation 12.0.0 integration with the Axon Backend project, focusing on .NET 10 compatibility, Clean Architecture patterns, and CQRS integration with MediatR.

## Package Information

- **Package**: FluentValidation 12.0.0
- **Platform Support**: .NET 10 compatible
- **Dependencies**: 
  - FluentValidation.DependencyInjectionExtensions (for DI integration)
  - FluentValidation.AspNetCore (optional for ASP.NET Core integration)

## Breaking Changes in 12.0.0

### Key Changes from 11.x

1. **InjectValidator Method Removed**: The `InjectValidator` method for implicit child validator injection from ASP.NET Service Provider has been removed
2. **EnsureInstanceNotNull Override Removed**: Ability to override `AbstractValidator.EnsureInstanceNotNull` method has been completely removed
3. **Platform Support Changes**: Several obsolete platforms no longer supported
4. **Serbian Language Updates**: Reorganized Serbian translations (sr-Latn for Latin, sr for Cyrillic)

### Migration Strategy

- Replace `InjectValidator` usage with traditional constructor injection
- Remove any `EnsureInstanceNotNull` overrides
- Update localization keys if using Serbian translations

## Installation

Add the required packages to your project:

```bash
dotnet add package FluentValidation --version 12.0.0
dotnet add package FluentValidation.DependencyInjectionExtensions --version 12.0.0
```

For ASP.NET Core integration (optional):
```bash
dotnet add package FluentValidation.AspNetCore --version 12.0.0
```

## Architecture Integration

### Dependency Injection Setup

Configure FluentValidation in your DI container following Axon Backend's Clean Architecture principles:

```csharp
// src/Api/Program.cs
using FluentValidation;
using FluentValidation.DependencyInjectionExtensions;

var builder = WebApplication.CreateBuilder(args);

// Register all validators from Application assemblies
builder.Services.AddValidatorsFromAssemblyContaining<IApplicationAssemblyMarker>();

// Alternative: Register from specific assemblies per module
builder.Services.AddValidatorsFromAssembly(typeof(Chat.Application.AssemblyReference).Assembly);

var app = builder.Build();
```

### Module-Based Registration

For modular architecture, register validators per module:

```csharp
// src/Modules/Chat/Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddChatModule(this IServiceCollection services)
    {
        // Register validators for this module
        services.AddValidatorsFromAssembly(
            typeof(Chat.Application.AssemblyReference).Assembly, 
            ServiceLifetime.Scoped
        );
        
        return services;
    }
}
```

## CQRS Integration with MediatR

### Validation Pipeline Behavior

Implement centralized validation using MediatR's pipeline behaviors:

```csharp
// src/Shared/Common/Behaviors/ValidationBehavior.cs
using FluentValidation;
using MediatR;

public sealed class ValidationBehavior<TRequest, TResponse> 
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);
        
        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken))
        );

        var failures = validationResults
            .Where(r => r.Errors.Any())
            .SelectMany(r => r.Errors)
            .ToList();

        if (failures.Any())
        {
            throw new ValidationException(failures);
        }

        return await next();
    }
}
```

### Command Validators

Create validators for commands following DDD principles:

```csharp
// src/Modules/Chat/Application/Commands/SendMessage/SendMessageCommandValidator.cs
using FluentValidation;

public sealed class SendMessageCommandValidator : AbstractValidator<SendMessageCommand>
{
    public SendMessageCommandValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty()
            .WithMessage("Message content cannot be empty")
            .MaximumLength(1000)
            .WithMessage("Message content cannot exceed 1000 characters");

        RuleFor(x => x.ConversationId)
            .NotEmpty()
            .WithMessage("Conversation ID is required")
            .Must(BeValidGuid)
            .WithMessage("Conversation ID must be a valid GUID");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");
    }

    private static bool BeValidGuid(Guid guid) => guid != Guid.Empty;
}
```

### Query Validators (When Needed)

For complex queries with filtering/pagination:

```csharp
// src/Modules/Chat/Application/Queries/GetConversations/GetConversationsQueryValidator.cs
public sealed class GetConversationsQueryValidator : AbstractValidator<GetConversationsQuery>
{
    public GetConversationsQueryValidator()
    {
        RuleFor(x => x.PageSize)
            .GreaterThan(0)
            .LessThanOrEqualTo(100)
            .WithMessage("Page size must be between 1 and 100");

        RuleFor(x => x.PageNumber)
            .GreaterThan(0)
            .WithMessage("Page number must be greater than 0");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");
    }
}
```

## Advanced Validation Patterns

### Asynchronous Validation

For validations requiring external dependencies:

```csharp
public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    private readonly IUserRepository _userRepository;

    public CreateUserCommandValidator(IUserRepository userRepository)
    {
        _userRepository = userRepository;
        
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MustAsync(BeUniqueEmail)
            .WithMessage("Email address is already in use");
    }

    private async Task<bool> BeUniqueEmail(string email, CancellationToken cancellationToken)
    {
        return !await _userRepository.ExistsWithEmailAsync(email, cancellationToken);
    }
}
```

### Conditional Validation

Apply validation rules conditionally:

```csharp
public sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .When(x => !string.IsNullOrEmpty(x.Email))
            .WithMessage("Email must be valid when provided");

        RuleFor(x => x.Password)
            .MinimumLength(8)
            .When(x => x.ChangePassword)
            .WithMessage("Password must be at least 8 characters when changing password");
    }
}
```

### Custom Validators

Create reusable custom validation rules:

```csharp
// src/Shared/Common/Validators/CustomValidators.cs
public static class CustomValidators
{
    public static IRuleBuilder<T, Guid> NotEmptyGuid<T>(this IRuleBuilder<T, Guid> ruleBuilder)
    {
        return ruleBuilder
            .Must(guid => guid != Guid.Empty)
            .WithMessage("'{PropertyName}' must not be empty");
    }

    public static IRuleBuilder<T, string> ValidSlug<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .Matches(@"^[a-z0-9-]+$")
            .WithMessage("'{PropertyName}' must contain only lowercase letters, numbers, and hyphens");
    }
}

// Usage in validators
RuleFor(x => x.Id).NotEmptyGuid();
RuleFor(x => x.Slug).ValidSlug();
```

## Error Handling Integration

### Validation Exception Handler

Create a centralized exception handler for validation errors:

```csharp
// src/Api/Middleware/ValidationExceptionHandler.cs
public sealed class ValidationExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, 
        Exception exception, 
        CancellationToken cancellationToken)
    {
        if (exception is not ValidationException validationException)
        {
            return false;
        }

        var errors = validationException.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray()
            );

        var problemDetails = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed",
            Detail = "One or more validation errors occurred"
        };

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
```

### Result Pattern Integration

For Result-based error handling without exceptions:

```csharp
// src/Shared/Common/Behaviors/ValidationBehavior.cs (Result version)
public sealed class ValidationBehavior<TRequest, TResponse> 
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : class
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);
        
        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken))
        );

        var failures = validationResults
            .Where(r => r.Errors.Any())
            .SelectMany(r => r.Errors)
            .ToList();

        if (failures.Any())
        {
            var errors = failures.Select(f => Error.Validation(f.PropertyName, f.ErrorMessage));
            
            // Assuming TResponse can be Result<T>
            if (typeof(TResponse).IsGenericType && 
                typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
            {
                var resultType = typeof(TResponse).GetGenericArguments()[0];
                var failureMethod = typeof(Result<>).MakeGenericType(resultType)
                    .GetMethod(nameof(Result<object>.Failure));
                
                return (TResponse)failureMethod!.Invoke(null, [errors.First()]);
            }
        }

        return await next();
    }
}
```

## Performance Optimization

### Disable Localization

For performance-critical scenarios, disable localization:

```csharp
// src/Api/Program.cs
ValidatorOptions.Global.LanguageManager.Enabled = false;
```

### Validator Lifetime Management

Use appropriate service lifetimes:

```csharp
// Scoped (recommended for validators with dependencies)
builder.Services.AddValidatorsFromAssembly(assembly, ServiceLifetime.Scoped);

// Singleton (for stateless validators)
builder.Services.AddValidatorsFromAssembly(assembly, ServiceLifetime.Singleton);
```

### Conditional Validator Registration

Filter validators during registration:

```csharp
builder.Services.AddValidatorsFromAssemblyContaining<SendMessageCommand>(
    ServiceLifetime.Scoped,
    filter => filter.ValidatorType.Name.EndsWith("CommandValidator")
);
```

## Error Message Customization

### Custom Error Messages

Override default messages:

```csharp
public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Please provide an email address")
            .EmailAddress()
            .WithMessage("Please provide a valid email address");

        RuleFor(x => x.Age)
            .InclusiveBetween(18, 120)
            .WithMessage("Age must be between {From} and {To} years");
    }
}
```

### Localization Support

Configure localization for multiple languages:

```csharp
// src/Api/Program.cs
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { "en-US", "es-ES", "fr-FR" };
    options.SetDefaultCulture(supportedCultures[0])
           .AddSupportedCultures(supportedCultures)
           .AddSupportedUICultures(supportedCultures);
});
```

Custom language manager:

```csharp
public class CustomLanguageManager : LanguageManager
{
    public CustomLanguageManager()
    {
        AddTranslation("en", "NotEmptyValidator", "'{PropertyName}' is required");
        AddTranslation("es", "NotEmptyValidator", "'{PropertyName}' es requerido");
        AddTranslation("fr", "NotEmptyValidator", "'{PropertyName}' est requis");
    }
}

// Register custom language manager
ValidatorOptions.Global.LanguageManager = new CustomLanguageManager();
```

## Testing Validators

### Unit Testing Validators

Test validators in isolation:

```csharp
// tests/Modules/Chat/Application.UnitTests/Commands/SendMessage/SendMessageCommandValidatorTests.cs
public sealed class SendMessageCommandValidatorTests
{
    private readonly SendMessageCommandValidator _validator = new();

    [Fact]
    public void Should_Have_Error_When_Content_Is_Empty()
    {
        // Arrange
        var command = new SendMessageCommand(
            ConversationId: Guid.NewGuid(),
            Content: string.Empty,
            UserId: Guid.NewGuid()
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Content)
              .WithErrorMessage("Message content cannot be empty");
    }

    [Fact]
    public void Should_Not_Have_Error_When_Command_Is_Valid()
    {
        // Arrange
        var command = new SendMessageCommand(
            ConversationId: Guid.NewGuid(),
            Content: "Valid message content",
            UserId: Guid.NewGuid()
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
```

### Integration Testing

Test validation behavior in the MediatR pipeline:

```csharp
// tests/Api/IntegrationTests/Chat/SendMessageEndpointTests.cs
public sealed class SendMessageEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SendMessageEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SendMessage_WithInvalidContent_ReturnsValidationError()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new SendMessageRequest
        {
            ConversationId = Guid.NewGuid(),
            Content = string.Empty, // Invalid
            UserId = Guid.NewGuid()
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/messages", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problemDetails!.Errors.Should().ContainKey("Content");
    }
}
```

## Best Practices for Axon Backend

### 1. Validation Placement

- **Commands**: Always validate commands that modify state
- **Queries**: Validate complex queries with filtering/pagination parameters
- **Domain Events**: Consider validating events for data integrity

### 2. Dependency Management

- Keep validators in the Application layer
- Inject domain services for business rule validation
- Avoid direct database access in validators (use repository interfaces)

### 3. Error Handling Strategy

- Use consistent error message formats
- Provide meaningful error codes for client applications
- Consider internationalization from the start

### 4. Performance Considerations

- Use `SetValidator()` for complex object validation instead of nested rules
- Prefer `MustAsync()` over `Must()` for I/O operations
- Cache compiled validators when possible

### 5. Clean Architecture Compliance

- Validators belong in the Application layer
- Domain rules should be validated in domain entities
- Infrastructure concerns (database uniqueness) use async validators

## Common Pitfalls

1. **Over-validation**: Don't duplicate domain validation in validators
2. **Performance**: Avoid synchronous I/O in `Must()` rules
3. **Coupling**: Don't reference infrastructure directly from validators
4. **Error Messages**: Provide user-friendly messages, not technical details
5. **Testing**: Always test validation rules in isolation

## Configuration Example

Complete configuration for Axon Backend:

```csharp
// src/Api/Program.cs
var builder = WebApplication.CreateBuilder(args);

// Register FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<IApplicationAssemblyMarker>();

// Register MediatR with validation behavior
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<IApplicationAssemblyMarker>();
    cfg.AddBehavior<ValidationBehavior<,>>();
});

// Register exception handler
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();

var app = builder.Build();

// Configure middleware
app.UseExceptionHandler();

app.Run();
```

This comprehensive guide provides all necessary information for implementing FluentValidation 12.0.0 in the Axon Backend project, following Clean Architecture principles and integrating seamlessly with the CQRS pattern using MediatR.

## References

- [FluentValidation 12.0 Upgrade Guide](https://docs.fluentvalidation.net/en/latest/upgrading-to-12.html)
- [FluentValidation Documentation](https://docs.fluentvalidation.net/)
- [ASP.NET Core Integration](https://docs.fluentvalidation.net/en/latest/aspnet.html)
- [Dependency Injection Guide](https://docs.fluentvalidation.net/en/latest/di.html)
- [CQRS Validation Patterns](https://code-maze.com/cqrs-mediatr-fluentvalidation/)