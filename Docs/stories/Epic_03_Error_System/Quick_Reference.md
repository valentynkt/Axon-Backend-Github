# Epic 03: Enhanced Error System - Quick Reference ✅ 100% COMPLETE

## 🎉 Implementation Status: ✅ ALL STORIES COMPLETED

**Epic 03 Error System is now 100% complete and production-ready!**

### ✅ Story Completion Status:
- **Story 01**: Core Error Types - ✅ **COMPLETED**
- **Story 02**: Domain Exceptions - ✅ **COMPLETED**  
- **Story 03**: Guard Clauses - ✅ **COMPLETED**
- **Story 04**: Problem Details - ✅ **COMPLETED**
- **Story 05**: Result Integration - ✅ **COMPLETED** 
- **Story 06**: Performance Caching - ✅ **COMPLETED**

## 🚀 Quick Start Commands

```bash
# Navigate to project
cd /Users/valentynkit/Repos/Axon-Backend

# Build the solution
dotnet build

# Run tests for error system
dotnet test --filter "FullyQualifiedName~Error"

# Run API with error handling
dotnet run --project src/Api
```

## 📁 File Locations

### Core Implementation Files
```
src/BuildingBlocks/Core/
├── Functional/
│   ├── Errors/
│   │   ├── Error.cs                 # Enhanced Error record (Story 01)
│   │   ├── ErrorType.cs            # Error categorization (Story 01)
│   │   └── ErrorSeverity.cs        # Severity levels (Story 01)
│   ├── Exceptions/
│   │   ├── DomainException.cs      # Base domain exception (Story 02)
│   │   ├── BusinessRuleException.cs # Business rule violations (Story 02)
│   │   └── ValidationException.cs   # Validation failures (Story 02)
│   └── Guards/
│       ├── Guard.cs                 # Guard clause static class (Story 03)
│       └── GuardClause.cs          # Fluent API (Story 03)
```

### Infrastructure Files
```
src/BuildingBlocks/Infrastructure/
├── Errors/
│   ├── ErrorCache.cs               # Metadata caching (Story 06)
│   ├── ErrorPool.cs                # Object pooling (Story 06)
│   └── ErrorMetrics.cs             # Performance metrics (Story 06)
└── Observability/
    ├── ErrorTelemetry.cs           # OpenTelemetry integration (Story 05)
    └── ErrorLogging.cs             # Structured logging (Story 05)
```

### Web/API Files
```
src/BuildingBlocks/Web/
├── Errors/
│   ├── ProblemDetailsFactory.cs    # RFC 7807 factory (Story 04)
│   └── ErrorMappingMiddleware.cs   # HTTP mapping (Story 04)
└── Extensions/
    └── ResultExtensions.cs         # ToActionResult (Story 05)
```

## 💡 Usage Examples

### Story 01: Creating Errors
```csharp
// Simple error
var error = Error.Validation("USER_001", "Invalid email format");

// Error with metadata
var error = Error.NotFound("USER_404", "User not found")
    .WithMetadata("userId", userId)
    .WithMetadata("timestamp", DateTime.UtcNow);

// Error with severity
var critical = Error.System("SYS_001", "Database connection failed")
    .WithSeverity(ErrorSeverity.Critical);
```

### Story 02: Domain Exceptions
```csharp
// Business rule violation
throw new BusinessRuleException(
    new EmailMustBeUniqueRule(email),
    "USER_002"
);

// Domain validation
throw new ValidationException("Invalid user data")
    .WithError("Email", "Invalid format")
    .WithError("Age", "Must be 18 or older");

// Converting to Result
try 
{
    // domain logic
}
catch (DomainException ex)
{
    return Result<User>.Failure(ex.ToError());
}
```

### Story 03: Guard Clauses
```csharp
// Simple guards
Guard.Against(email).NullOrWhiteSpace();
Guard.Against(age).LessThan(18);
Guard.Against(items).Empty();

// Fluent API
var validEmail = Guard.Against(email)
    .NullOrWhiteSpace()
    .MinLength(5)
    .MaxLength(100)
    .Matches(@"^[^@]+@[^@]+\.[^@]+$")
    .Value;

// Custom validation
Guard.Against(user)
    .Null()
    .Custom(u => u.IsActive, "User must be active");
```

### Story 04: Problem Details
```csharp
// In FastEndpoints
public override async Task OnValidationFailedAsync()
{
    await SendAsync(
        ProblemDetailsFactory.CreateValidation(ValidationFailures),
        StatusCodes.Status400BadRequest
    );
}

// Manual creation
var problem = new ProblemDetails
{
    Type = "https://api.example.com/errors/validation",
    Title = "Validation Error",
    Status = 400,
    Detail = error.Message,
    Instance = HttpContext.Request.Path
};
```

### Story 05: Result with Observability
```csharp
// Automatic tracing
var result = await Mediator.Send(command)
    .TraceError("CreateUser") // Adds OpenTelemetry span
    .LogError(logger);         // Structured logging

// Convert to API response
return result.ToActionResult(
    onSuccess: user => Ok(user),
    onFailure: error => Problem(error)
);

// Metrics collection
result.RecordMetrics("user_creation");
```

### Story 06: Performance Optimizations
```csharp
// Cached error metadata
var error = ErrorCache.GetOrCreate(
    "USER_001",
    () => Error.Validation("USER_001", "Invalid email")
);

// Pooled error objects
using var pooled = ErrorPool.Rent();
var error = pooled.Value
    .WithCode("USER_002")
    .WithMessage("User not found");
    
// Return to pool automatically on dispose
```

## 🧪 Testing Patterns

### Unit Testing Errors
```csharp
[Fact]
public void Should_Create_Validation_Error()
{
    // Arrange & Act
    var error = Error.Validation("TEST_001", "Test message");
    
    // Assert
    error.Type.Should().Be(ErrorType.Validation);
    error.Code.Should().Be("TEST_001");
    error.Message.Should().Be("Test message");
}
```

### Testing Guard Clauses
```csharp
[Fact]
public void Should_Throw_When_Null()
{
    // Arrange
    string? value = null;
    
    // Act & Assert
    var act = () => Guard.Against(value).Null();
    act.Should().Throw<ArgumentNullException>();
}
```

### Integration Testing
```csharp
[Fact]
public async Task Should_Return_Problem_Details_On_Error()
{
    // Arrange
    var client = Factory.CreateClient();
    
    // Act
    var response = await client.PostAsJsonAsync("/users", invalidUser);
    
    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
    problem.Type.Should().Contain("validation");
}
```

## 📊 Monitoring Queries

### Grafana Dashboard Queries
```promql
# Error rate by type
sum(rate(errors_total[5m])) by (error_type)

# Critical errors
errors_total{severity="critical"}

# Error resolution time
histogram_quantile(0.95, 
  rate(error_resolution_duration_seconds_bucket[5m])
)
```

### Application Insights KQL
```kql
// Top 10 errors
traces
| where severityLevel >= 3
| summarize count() by message
| top 10 by count_

// Error trends
traces
| where severityLevel >= 3
| summarize count() by bin(timestamp, 1h)
| render timechart
```

## 🔗 Related Documentation

- [Epic 03 Full Specification](../../../Docs/Technical/Architecture/BuildingBlocks/Epic_03_Enhanced_Error_System.md)
- [Implementation Plan](Epic_03_Implementation_Plan.md)
- Individual Story Documents:
  - [Story 01: Core Error Types](Story_01_Core_Error_Types.md)
  - [Story 02: Domain Exceptions](Story_02_Domain_Exceptions.md)
  - [Story 03: Guard Clauses](Story_03_Guard_Clauses.md)
  - [Story 04: Problem Details](Story_04_Problem_Details.md)
  - [Story 05: Result Integration](Story_05_Result_Integration.md)
  - [Story 06: Performance & Caching](Story_06_Performance_Caching.md)

## 🆘 Support

- **Slack Channel:** #axon-error-system
- **Tech Lead:** [Contact via Teams/Slack]
- **Architecture Team:** [Weekly office hours]

---

*Last Updated: Generated for Epic 03 Implementation*