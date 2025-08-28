# FRD S1 - API Contracts (FINAL REFINED)

**Stage**: S1 - API Contracts Only  
**Layer**: API Layer (`src/Api`)  
**Dependencies**: None (Foundation)  
**Document Version**: 4.0 (Final Refined)  
**Date**: August 27, 2025  
**Author**: Engineering Team  
**Based on**: BRD v1.0, PRD v1.0, Axon Backend Chat Module Patterns  

---

## 1. Overview

### 1.1 Responsibility

Define all external API contracts for Dynamic.xyz authentication integration without any implementation. This stage establishes the public interface that clients will interact with, strictly following Axon Backend's established Chat module patterns and standard REST authentication practices.

### 1.2 Stage Context

- **S1 (Current)**: Contract definitions only (DTOs, endpoint signatures, stub responses)
- **S2**: Add infrastructure integration with Dynamic.xyz APIs
- **S3**: Wire up application handlers and business logic
- **S4**: Connect domain models and validation
- **S5**: Full persistence implementation with Entity Framework

### 1.3 Exit Criteria

- ✅ All API contracts defined following exact Chat module patterns
- ✅ JWT authentication via Authorization header (standard REST practice)
- ✅ Request/response models with primitive types only (no Strong ID dependencies)
- ✅ Validators inherit from `BaseValidator<T>` with proper configuration
- ✅ Endpoints inherit from Identity-specific base classes (following Chat pattern)
- ✅ Build passes with no warnings
- ✅ OpenAPI documentation generated with proper auth header declarations
- ✅ FastEndpoints-only integration (no Carter framework)

---

## 2. Architecture Integration

### 2.1 Following Exact Chat Module Patterns

The authentication endpoints follow the established Chat module patterns exactly:

**Base Class Pattern**: Custom `BaseIdentityCommandEndpoint` and `BaseIdentityQueryEndpoint` classes
**Mapping Chain**: Request → Command → Execute → Response using MapExecuteMap pattern
**Error Handling**: Return `Result<TResponse, Error>` following the Result pattern
**Validation**: Use `BaseValidator<T>` with FluentValidation
**Configuration**: Use `Configure()` method with abstract methods for route and metadata
**Authentication**: JWT tokens from `Authorization: Bearer <token>` header (standard REST)
**Primitive Types**: API contracts use only primitive types (string, Guid, int, etc.)

### 2.2 Module Structure

```
src/Api/
├── Endpoints/V1/Identity/
│   ├── Commands/
│   │   └── ExchangeToken/
│   │       ├── ExchangeTokenEndpoint.cs
│   │       └── ExchangeTokenRequestValidator.cs
│   └── Queries/
│       └── GetCurrentUser/
│           ├── GetCurrentUserEndpoint.cs
│           └── EmptyRequest.cs
├── Endpoints/V1/Webhooks/
│   └── Dynamic/
│       ├── ProcessDynamicWebhookEndpoint.cs
│       └── ProcessDynamicWebhookRequestValidator.cs
├── Contracts/V1/Identity/
│   ├── Authentication/
│   │   ├── ExchangeTokenRequestDto.cs
│   │   ├── ExchangeTokenResponseDto.cs
│   │   ├── CurrentUserResponseDto.cs
│   │   ├── UserProfileDto.cs
│   │   └── WalletDto.cs
│   ├── Webhooks/
│   │   ├── DynamicWebhookEventDto.cs
│   │   ├── DynamicWebhookResponseDto.cs
│   │   └── WebhookAcknowledgmentDto.cs
│   └── Common/
│       └── EmptyRequest.cs
└── Modules/
    ├── BaseIdentityCommandEndpoint.cs
    └── BaseIdentityQueryEndpoint.cs
```

---

## 3. Identity Base Endpoints (Following Chat Pattern)

### 3.1 Base Identity Command Endpoint

```csharp
// src/Api/Modules/BaseIdentityCommandEndpoint.cs

namespace Axon.Api.Modules;

/// <summary>
/// Base class for identity command endpoints that provides standardized configuration and command execution patterns
/// </summary>
public abstract class BaseIdentityCommandEndpoint<TRequest, TResponse, TCommand, TDomainResult>
    : BaseMappedEndpoint<TRequest, TResponse>
    where TRequest : notnull
    where TCommand : notnull
    where TDomainResult : notnull
{
    protected BaseIdentityCommandEndpoint(ILogger logger) : base(logger)
    {
    }

    public override void Configure()
    {
        Post(GetRoute());
        AllowAnonymous(); // TODO[S2]: Add Bearer token authentication

        Summary(s =>
        {
            s.Summary = GetSummary();
            s.Description = GetDescription();
            s.Responses[200] = GetSuccessResponse();
            s.Responses[400] = typeof(ProblemDetails);
            s.Responses[401] = typeof(ProblemDetails);
            s.Responses[403] = typeof(ProblemDetails);
            s.Responses[422] = typeof(ProblemDetails);
            s.Responses[500] = typeof(ProblemDetails);
        });

        Tags("Authentication");
        Consumes("application/json");
        Produces("application/json");
    }

    protected override async Task<Result<TResponse, Error>> ExecuteAsync(
        TRequest request,
        CancellationToken ct)
    {
        return await MapExecuteMap<TCommand, TDomainResult>(
            request,
            ExecuteCommand,
            ct);
    }

    /// <summary>
    /// Override to specify the route for this endpoint
    /// </summary>
    protected abstract string GetRoute();

    /// <summary>
    /// Override to provide the summary for OpenAPI documentation
    /// </summary>
    protected abstract string GetSummary();

    /// <summary>
    /// Override to provide the description for OpenAPI documentation
    /// </summary>
    protected abstract string GetDescription();

    /// <summary>
    /// Override to provide the success response description
    /// </summary>
    protected abstract string GetSuccessResponse();

    /// <summary>
    /// Override to implement the command execution logic
    /// </summary>
    protected abstract Task<Result<TDomainResult, Error>> ExecuteCommand(TCommand command, CancellationToken ct);
}
```

### 3.2 Base Identity Query Endpoint

```csharp
// src/Api/Modules/BaseIdentityQueryEndpoint.cs

namespace Axon.Api.Modules;

/// <summary>
/// Base class for identity query endpoints that provides standardized configuration and query execution patterns
/// </summary>
public abstract class BaseIdentityQueryEndpoint<TRequest, TResponse, TQuery, TDomainResult>
    : BaseMappedEndpoint<TRequest, TResponse>
    where TRequest : notnull
    where TQuery : notnull
    where TDomainResult : notnull
{
    protected BaseIdentityQueryEndpoint(ILogger logger) : base(logger)
    {
    }

    public override void Configure()
    {
        Get(GetRoute());
        AllowAnonymous(); // TODO[S2]: Add Bearer token authentication

        Summary(s =>
        {
            s.Summary = GetSummary();
            s.Description = GetDescription();
            s.Responses[200] = GetSuccessResponse();
            s.Responses[400] = typeof(ProblemDetails);
            s.Responses[401] = typeof(ProblemDetails);
            s.Responses[403] = typeof(ProblemDetails);
            s.Responses[404] = typeof(ProblemDetails);
            s.Responses[500] = typeof(ProblemDetails);
        });

        Tags("Authentication");
        Produces("application/json");
    }

    protected override async Task<Result<TResponse, Error>> ExecuteAsync(
        TRequest request,
        CancellationToken ct)
    {
        return await MapExecuteMap<TQuery, TDomainResult>(
            request,
            ExecuteQuery,
            ct);
    }

    /// <summary>
    /// Override to specify the route for this endpoint
    /// </summary>
    protected abstract string GetRoute();

    /// <summary>
    /// Override to provide the summary for OpenAPI documentation
    /// </summary>
    protected abstract string GetSummary();

    /// <summary>
    /// Override to provide the description for OpenAPI documentation
    /// </summary>
    protected abstract string GetDescription();

    /// <summary>
    /// Override to provide the success response description
    /// </summary>
    protected abstract string GetSuccessResponse();

    /// <summary>
    /// Override to implement the query execution logic
    /// </summary>
    protected abstract Task<Result<TDomainResult, Error>> ExecuteQuery(TQuery query, CancellationToken ct);
}
```

---

## 4. Authentication Endpoints

### 4.1 Exchange Token Endpoint

#### 4.1.1 Endpoint Implementation

```csharp
// src/Api/Endpoints/V1/Identity/Commands/ExchangeToken/ExchangeTokenEndpoint.cs

namespace Axon.Api.Endpoints.V1.Identity.Commands.ExchangeToken;

public sealed class ExchangeTokenEndpoint(ILogger<ExchangeTokenEndpoint> logger)
    : BaseIdentityCommandEndpoint<ExchangeTokenRequestDto, ExchangeTokenResponseDto, ExchangeTokenCommand, ExchangeTokenResult>(logger)
{
    protected override string GetRoute() => "/api/v1/auth/exchange";

    protected override string GetSummary() => "Exchange Dynamic.xyz JWT token for Axon user context";

    protected override string GetDescription() => 
        "Validates Dynamic.xyz JWT token from Authorization header and returns local user information with connected wallets";

    protected override string GetSuccessResponse() => "Token validated successfully, user context returned";

    public override void Configure()
    {
        base.Configure();
        
        // Expect Authorization header with Bearer token
        Options(o => o.WithHeader("Authorization", "Bearer JWT token from Dynamic.xyz"));
        
        Summary(s =>
        {
            s.ExampleRequest = new ExchangeTokenRequestDto
            {
                ForceRefresh = false
            };
            s.Responses[401] = "Invalid or expired JWT token";
            s.Responses[502] = "Dynamic.xyz API unavailable";
        });
    }

    protected override async Task<Result<ExchangeTokenResult, Error>> ExecuteCommand(
        ExchangeTokenCommand command,
        CancellationToken ct)
    {
        // S1: Extract JWT from Authorization header
        var authHeader = HttpContext.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogWarning("Missing or invalid Authorization header format");
            return Error.Unauthorized("Missing or invalid Authorization header");
        }

        var jwtToken = authHeader.Substring("Bearer ".Length).Trim();
        if (string.IsNullOrEmpty(jwtToken))
        {
            Logger.LogWarning("Empty JWT token in Authorization header");
            return Error.Unauthorized("Empty JWT token");
        }

        // S1: Basic JWT format validation (3 segments)
        if (jwtToken.Split('.').Length != 3)
        {
            Logger.LogWarning("Invalid JWT format - must have 3 segments");
            return Error.Validation("Invalid JWT format");
        }

        // S1: Return stub response with mock data
        var mockResponse = new ExchangeTokenResult
        {
            UserId = "usr_2Z4e8K9mNp3QrS7T",
            DynamicUserId = Guid.Parse("95b11417-f18f-457f-8804-68e361f9164f"),
            Email = "user@example.com",
            DisplayName = "John Doe",
            Username = "johndoe",
            Wallets = new List<WalletResult>
            {
                new()
                {
                    Id = "wlt_8X2nMk9P4qR5sT7U",
                    DynamicWalletId = Guid.Parse("a7c11234-e89b-12d3-a456-426614174000"),
                    Address = "So11111111111111111111111111111111111111112",
                    Chain = "SOL",
                    Provider = "phantom",
                    WalletName = "Main Solana Wallet",
                    LastSelectedAt = DateTime.UtcNow.AddHours(-1),
                    ConnectedAt = DateTime.UtcNow.AddDays(-30)
                }
            },
            SyncedAt = DateTime.UtcNow,
            SyncStatus = "completed"
        };

        Logger.LogInformation("S1 Stub: Returning mock user context for JWT token");
        return Result<ExchangeTokenResult, Error>.Success(mockResponse);
    }
}
```

#### 4.1.2 Request DTO (No JWT Token - Comes from Header)

```csharp
// src/Api/Contracts/V1/Identity/Authentication/ExchangeTokenRequestDto.cs

namespace Axon.Api.Contracts.V1.Identity.Authentication;

/// <summary>
/// Request to exchange Dynamic.xyz JWT token for Axon user context.
/// JWT token is provided via Authorization: Bearer header.
/// </summary>
public sealed record ExchangeTokenRequestDto
{
    /// <summary>
    /// Optional flag to force refresh of user data from Dynamic.xyz
    /// </summary>
    public bool ForceRefresh { get; init; } = false;
}
```

#### 4.1.3 Response DTO (Primitive Types Only)

```csharp
// src/Api/Contracts/V1/Identity/Authentication/ExchangeTokenResponseDto.cs

namespace Axon.Api.Contracts.V1.Identity.Authentication;

/// <summary>
/// Response containing validated user context and wallet information
/// </summary>
public sealed record ExchangeTokenResponseDto
{
    /// <summary>
    /// Axon internal user identifier
    /// </summary>
    public required string UserId { get; init; }

    /// <summary>
    /// Dynamic.xyz user identifier
    /// </summary>
    public required Guid DynamicUserId { get; init; }

    /// <summary>
    /// User's verified email address
    /// </summary>
    public required string Email { get; init; }

    /// <summary>
    /// User's display name (optional)
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// User's username (optional)
    /// </summary>
    public string? Username { get; init; }

    /// <summary>
    /// List of connected wallets
    /// </summary>
    public required IReadOnlyList<WalletDto> Wallets { get; init; }

    /// <summary>
    /// Timestamp when user data was last synchronized
    /// </summary>
    public required DateTime SyncedAt { get; init; }

    /// <summary>
    /// Current synchronization status
    /// </summary>
    public required string SyncStatus { get; init; }
}
```

#### 4.1.4 Wallet DTO (Primitive Types Only)

```csharp
// src/Api/Contracts/V1/Identity/Authentication/WalletDto.cs

namespace Axon.Api.Contracts.V1.Identity.Authentication;

/// <summary>
/// Wallet information from Dynamic.xyz
/// </summary>
public sealed record WalletDto
{
    /// <summary>
    /// Axon internal wallet identifier
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Dynamic.xyz wallet identifier
    /// </summary>
    public required Guid DynamicWalletId { get; init; }

    /// <summary>
    /// Wallet public address/key
    /// </summary>
    public required string Address { get; init; }

    /// <summary>
    /// Blockchain type
    /// </summary>
    public required string Chain { get; init; }

    /// <summary>
    /// Wallet provider type
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// User-assigned wallet name (optional)
    /// </summary>
    public string? WalletName { get; init; }

    /// <summary>
    /// Timestamp when wallet was last selected by user
    /// </summary>
    public DateTime? LastSelectedAt { get; init; }

    /// <summary>
    /// Timestamp when wallet was connected
    /// </summary>
    public DateTime? ConnectedAt { get; init; }
}
```

#### 4.1.5 Request Validator (No JWT Validation - Header Based)

```csharp
// src/Api/Endpoints/V1/Identity/Commands/ExchangeToken/ExchangeTokenRequestValidator.cs

namespace Axon.Api.Endpoints.V1.Identity.Commands.ExchangeToken;

public sealed class ExchangeTokenRequestValidator : BaseValidator<ExchangeTokenRequestDto>
{
    public ExchangeTokenRequestValidator()
    {
        // No JWT validation needed - it comes from Authorization header
        // ForceRefresh is always valid boolean - no validation needed
        
        // Could add other business rules if needed
        RuleFor(x => x.ForceRefresh)
            .Must(x => x is true or false) // Always passes, but shows pattern
            .WithMessage("ForceRefresh must be a boolean value");
    }
}
```

### 4.2 Get Current User Endpoint

#### 4.2.1 Endpoint Implementation

```csharp
// src/Api/Endpoints/V1/Identity/Queries/GetCurrentUser/GetCurrentUserEndpoint.cs

namespace Axon.Api.Endpoints.V1.Identity.Queries.GetCurrentUser;

public sealed class GetCurrentUserEndpoint(ILogger<GetCurrentUserEndpoint> logger)
    : BaseIdentityQueryEndpoint<EmptyRequest, CurrentUserResponseDto, GetCurrentUserQuery, CurrentUserResult>(logger)
{
    protected override string GetRoute() => "/api/v1/auth/me";

    protected override string GetSummary() => "Get current authenticated user profile";

    protected override string GetDescription() => 
        "Returns current user information with connected wallets (local data only). Requires Bearer token in Authorization header.";

    protected override string GetSuccessResponse() => "User profile retrieved successfully";

    public override void Configure()
    {
        base.Configure();
        
        // Expect Authorization header with Bearer token
        Options(o => o.WithHeader("Authorization", "Bearer JWT token from Dynamic.xyz"));
    }

    protected override async Task<Result<CurrentUserResult, Error>> ExecuteQuery(
        GetCurrentUserQuery query,
        CancellationToken ct)
    {
        // S1: Extract JWT from Authorization header for user identification
        var authHeader = HttpContext.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogWarning("Missing or invalid Authorization header format");
            return Error.Unauthorized("Missing or invalid Authorization header");
        }

        var jwtToken = authHeader.Substring("Bearer ".Length).Trim();
        if (string.IsNullOrEmpty(jwtToken))
        {
            Logger.LogWarning("Empty JWT token in Authorization header");
            return Error.Unauthorized("Empty JWT token");
        }

        // S1: Return stub response with mock data based on header presence
        var mockResponse = new CurrentUserResult
        {
            User = new UserProfileResult
            {
                Id = "usr_2Z4e8K9mNp3QrS7T",
                DynamicUserId = Guid.Parse("95b11417-f18f-457f-8804-68e361f9164f"),
                Email = "user@example.com",
                DisplayName = "John Doe",
                Username = "johndoe",
                FirstVisit = DateTime.UtcNow.AddDays(-30),
                LastVisit = DateTime.UtcNow.AddMinutes(-5),
                Metadata = new Dictionary<string, object>
                {
                    ["preferences"] = new { theme = "dark", notifications = true },
                    ["tags"] = new[] { "premium", "beta" }
                }
            },
            Wallets = new List<WalletResult>
            {
                new()
                {
                    Id = "wlt_8X2nMk9P4qR5sT7U",
                    DynamicWalletId = Guid.Parse("a7c11234-e89b-12d3-a456-426614174000"),
                    Address = "So11111111111111111111111111111111111111112",
                    Chain = "SOL",
                    Provider = "phantom",
                    WalletName = "Main Solana Wallet",
                    LastSelectedAt = DateTime.UtcNow.AddHours(-1),
                    ConnectedAt = DateTime.UtcNow.AddDays(-30)
                }
            },
            SyncedAt = DateTime.UtcNow.AddMinutes(-5),
            SyncStatus = "completed"
        };

        Logger.LogInformation("S1 Stub: Returning mock current user profile");
        return Result<CurrentUserResult, Error>.Success(mockResponse);
    }
}
```

#### 4.2.2 Response DTOs

```csharp
// src/Api/Contracts/V1/Identity/Authentication/CurrentUserResponseDto.cs

namespace Axon.Api.Contracts.V1.Identity.Authentication;

/// <summary>
/// Response containing current user profile and wallet information
/// </summary>
public sealed record CurrentUserResponseDto
{
    /// <summary>
    /// User profile information
    /// </summary>
    public required UserProfileDto User { get; init; }

    /// <summary>
    /// Connected wallets
    /// </summary>
    public required IReadOnlyList<WalletDto> Wallets { get; init; }

    /// <summary>
    /// Last sync timestamp
    /// </summary>
    public required DateTime SyncedAt { get; init; }

    /// <summary>
    /// Data synchronization status
    /// </summary>
    public required string SyncStatus { get; init; }
}

/// <summary>
/// User profile information
/// </summary>
public sealed record UserProfileDto
{
    /// <summary>
    /// Axon internal user identifier
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Dynamic.xyz user identifier
    /// </summary>
    public required Guid DynamicUserId { get; init; }

    /// <summary>
    /// User's email address
    /// </summary>
    public required string Email { get; init; }

    /// <summary>
    /// User's display name
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// User's username
    /// </summary>
    public string? Username { get; init; }

    /// <summary>
    /// First visit timestamp
    /// </summary>
    public DateTime? FirstVisit { get; init; }

    /// <summary>
    /// Last visit timestamp
    /// </summary>
    public DateTime? LastVisit { get; init; }

    /// <summary>
    /// User metadata from Dynamic.xyz
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
}
```

---

## 5. Webhook Endpoints

### 5.1 Dynamic.xyz Webhook Receiver

#### 5.1.1 Endpoint Implementation

```csharp
// src/Api/Endpoints/V1/Webhooks/Dynamic/ProcessDynamicWebhookEndpoint.cs

namespace Axon.Api.Endpoints.V1.Webhooks.Dynamic;

public sealed class ProcessDynamicWebhookEndpoint(ILogger<ProcessDynamicWebhookEndpoint> logger)
    : BaseIdentityCommandEndpoint<DynamicWebhookEventDto, DynamicWebhookResponseDto, ProcessWebhookCommand, WebhookProcessResult>(logger)
{
    protected override string GetRoute() => "/api/v1/webhooks/dynamic";

    protected override string GetSummary() => "Process Dynamic.xyz webhook events";

    protected override string GetDescription() => 
        "Receives and processes lifecycle events from Dynamic.xyz for real-time synchronization";

    protected override string GetSuccessResponse() => "Webhook processed successfully";

    public override void Configure()
    {
        Post(GetRoute());
        AllowAnonymous(); // Webhook endpoints use signature validation instead of Bearer auth

        Summary(s =>
        {
            s.Summary = GetSummary();
            s.Description = GetDescription();
            s.ExampleRequest = new DynamicWebhookEventDto
            {
                EventId = "evt_user_created_123",
                EventName = "users.created",
                WebhookId = "wh_abc123def456",
                CreatedAt = DateTime.UtcNow,
                Data = new Dictionary<string, object>
                {
                    ["userId"] = "95b11417-f18f-457f-8804-68e361f9164f",
                    ["email"] = "user@example.com"
                }
            };
            s.Responses[202] = "Webhook accepted for processing";
            s.Responses[400] = typeof(ProblemDetails);
            s.Responses[500] = typeof(ProblemDetails);
        });

        Tags("Webhooks");
        
        // Webhook-specific configuration
        Options(o => o
            .RequireHeader("X-Dynamic-Signature")
            .Accepts<DynamicWebhookEventDto>("application/json")
            .Produces<DynamicWebhookResponseDto>(202)
            .ProducesProblem(400));
    }

    protected override async Task<Result<WebhookProcessResult, Error>> ExecuteCommand(
        ProcessWebhookCommand command,
        CancellationToken ct)
    {
        Logger.LogInformation("S1 Stub: Processing webhook event {EventType} with ID {EventId}", 
            command.EventName, command.EventId);

        // S1: Return success response (202 Accepted for async processing)
        var response = new WebhookProcessResult
        {
            Received = true,
            ProcessedAt = DateTime.UtcNow,
            SyncScheduled = command.EventName.Contains("users.") || command.EventName.Contains("wallets."),
            Acknowledgment = new WebhookAcknowledgmentResult
            {
                EventId = command.EventId,
                WebhookId = command.WebhookId ?? "",
                Status = "processed",
                RetryCount = 0,
                ErrorMessage = null
            }
        };

        return Result<WebhookProcessResult, Error>.Success(response);
    }
}
```

#### 5.1.2 Webhook DTOs

```csharp
// src/Api/Contracts/V1/Identity/Webhooks/DynamicWebhookEventDto.cs

namespace Axon.Api.Contracts.V1.Identity.Webhooks;

/// <summary>
/// Dynamic.xyz webhook event payload
/// </summary>
public sealed record DynamicWebhookEventDto
{
    /// <summary>
    /// Unique event identifier
    /// </summary>
    public required string EventId { get; init; }

    /// <summary>
    /// Event type name
    /// </summary>
    public required string EventName { get; init; }

    /// <summary>
    /// Webhook configuration identifier
    /// </summary>
    public string? WebhookId { get; init; }

    /// <summary>
    /// Event creation timestamp
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Event-specific data payload
    /// </summary>
    public required IReadOnlyDictionary<string, object> Data { get; init; }
}

// src/Api/Contracts/V1/Identity/Webhooks/DynamicWebhookResponseDto.cs

/// <summary>
/// Response confirming webhook processing
/// </summary>
public sealed record DynamicWebhookResponseDto
{
    /// <summary>
    /// Whether webhook was received successfully
    /// </summary>
    public required bool Received { get; init; }

    /// <summary>
    /// Processing completion timestamp
    /// </summary>
    public required DateTime ProcessedAt { get; init; }

    /// <summary>
    /// Whether data synchronization was scheduled
    /// </summary>
    public required bool SyncScheduled { get; init; }

    /// <summary>
    /// Processing acknowledgment details
    /// </summary>
    public required WebhookAcknowledgmentDto Acknowledgment { get; init; }
}

/// <summary>
/// Webhook processing acknowledgment
/// </summary>
public sealed record WebhookAcknowledgmentDto
{
    /// <summary>
    /// Original event ID
    /// </summary>
    public required string EventId { get; init; }

    /// <summary>
    /// Webhook configuration ID
    /// </summary>
    public required string WebhookId { get; init; }

    /// <summary>
    /// Processing status
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Retry attempt count
    /// </summary>
    public required int RetryCount { get; init; }

    /// <summary>
    /// Error message if processing failed
    /// </summary>
    public string? ErrorMessage { get; init; }
}
```

#### 5.1.3 Webhook Request Validator

```csharp
// src/Api/Endpoints/V1/Webhooks/Dynamic/ProcessDynamicWebhookRequestValidator.cs

namespace Axon.Api.Endpoints.V1.Webhooks.Dynamic;

public sealed class ProcessDynamicWebhookRequestValidator : BaseValidator<DynamicWebhookEventDto>
{
    private static readonly HashSet<string> ValidEventTypes = new()
    {
        "users.created", "users.updated", "users.deleted",
        "wallets.linked", "wallets.unlinked", "wallets.updated",
        "sessions.created", "sessions.deleted",
        "environments.updated"
    };

    public ProcessDynamicWebhookRequestValidator()
    {
        RuleFor(x => x.EventId)
            .NotEmpty()
            .WithMessage("EventId is required")
            .MaximumLength(100)
            .WithMessage("EventId must not exceed 100 characters");

        RuleFor(x => x.EventName)
            .NotEmpty()
            .WithMessage("EventName is required")
            .Must(BeValidEventType)
            .WithMessage($"EventName must be one of: {string.Join(", ", ValidEventTypes)}");

        // Fix: Use dynamic timestamp evaluation
        RuleFor(x => x.CreatedAt)
            .LessThanOrEqualTo(_ => DateTime.UtcNow.AddMinutes(5))
            .WithMessage("CreatedAt cannot be more than 5 minutes in the future");

        RuleFor(x => x.Data)
            .NotNull()
            .WithMessage("Data payload is required");
    }

    private static bool BeValidEventType(string eventName)
    {
        return ValidEventTypes.Contains(eventName);
    }
}
```

---

## 6. Common Types

### 6.1 Empty Request

```csharp
// src/Api/Contracts/V1/Identity/Common/EmptyRequest.cs

namespace Axon.Api.Contracts.V1.Identity.Common;

/// <summary>
/// Empty request for endpoints that don't require request parameters
/// </summary>
public sealed record EmptyRequest;
```

---

## 7. Key Improvements in This Final Version

### 7.1 Standard REST Authentication
- ✅ JWT tokens from `Authorization: Bearer <token>` header (standard practice)
- ✅ Request DTOs contain no authentication tokens
- ✅ Proper header validation and extraction in endpoints
- ✅ Clear OpenAPI documentation for required headers

### 7.2 Exact Chat Module Pattern Alignment
- ✅ Custom `BaseIdentityCommandEndpoint` and `BaseIdentityQueryEndpoint`
- ✅ Abstract methods for route, summary, description, success response
- ✅ Same Configure() and ExecuteAsync() patterns as Chat
- ✅ Consistent error response types and status codes

### 7.3 Primitive Types Only (No Strong ID Dependencies)
- ✅ All DTOs use `string` for Axon IDs, `Guid` for Dynamic IDs
- ✅ API layer has zero dependencies on Domain layer
- ✅ Strong ID mapping will happen in Application layer (S3+)

### 7.4 Fixed Validation Issues
- ✅ Dynamic timestamp evaluation in validators
- ✅ Removed JWT validation from request validators (header-based now)
- ✅ Proper cascade mode configuration from `BaseValidator`

### 7.5 Consistent Route Versioning
- ✅ All routes under `/api/v1/` prefix
- ✅ Authentication endpoints: `/api/v1/auth/*`
- ✅ Webhook endpoints: `/api/v1/webhooks/*`

### 7.6 FastEndpoints-Only Architecture
- ✅ Removed all Carter framework references
- ✅ Pure FastEndpoints with automatic discovery
- ✅ No manual service registration needed beyond validators

### 7.7 Proper OpenAPI Documentation
- ✅ Standard error responses using `ProblemDetails`
- ✅ Required headers declared in Options()
- ✅ Proper HTTP status codes (202 for webhooks)
- ✅ Consumes/Produces declarations

---

## 8. Authentication Flow Examples

### 8.1 Exchange Token Flow
```http
POST /api/v1/auth/exchange
Authorization: Bearer eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9...
Content-Type: application/json

{
  "forceRefresh": false
}
```

### 8.2 Get Current User Flow
```http
GET /api/v1/auth/me
Authorization: Bearer eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9...
```

### 8.3 Webhook Processing Flow
```http
POST /api/v1/webhooks/dynamic
X-Dynamic-Signature: sha256=abc123...
Content-Type: application/json

{
  "eventId": "evt_user_created_123",
  "eventName": "users.created",
  "webhookId": "wh_abc123def456",
  "createdAt": "2025-08-27T12:00:00Z",
  "data": {
    "userId": "95b11417-f18f-457f-8804-68e361f9164f",
    "email": "user@example.com"
  }
}
```

---

## 9. Next Steps for S2 and Beyond

### S2 - Infrastructure Integration
- Implement actual JWT validation with JWKS endpoint
- Add Dynamic.xyz API client for data fetching
- Implement webhook signature validation
- Add caching layer for JWKS keys

### S3 - Application Layer
- Create command/query handlers with Strong ID mapping
- Implement MediatR pipeline behaviors
- Map primitive API types to Strong ID domain types
- Add business logic validation

### S4 - Domain Layer  
- Implement User and Wallet aggregates with Strong IDs
- Add domain events and value objects
- Create repository interfaces
- Business rule implementation

### S5 - Persistence Layer
- Create Entity Framework DbContext with Strong IDs
- Add migrations and indices
- Implement repositories
- Configure Strong ID converters

---

This final refined FRD S1 document now perfectly aligns with Axon Backend's established patterns while following standard REST authentication practices. The JWT-in-header approach is the correct industry standard, and the primitive types ensure true foundational layer independence.