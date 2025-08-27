# FRD S1 - API Contracts

**Stage**: S1 - API Contracts Only  
**Layer**: API Layer (`src/Api`)  
**Dependencies**: None (Foundation)  
**Document Version**: 2.0  
**Date**: August 27, 2025  
**Author**: Engineering Team  
**Based on**: BRD v1.0, PRD v1.0, Dynamic.xyz API Reference  

---

## 1. Overview

### 1.1 Responsibility

Define all external API contracts for Dynamic.xyz authentication integration without any implementation. This stage establishes the public interface that clients will interact with, following Axon Backend's established patterns and conventions.

### 1.2 Stage Context

- **S1 (Current)**: Contract definitions only (DTOs, endpoint signatures, stub responses)
- **S2**: Add infrastructure integration with Dynamic.xyz APIs
- **S3**: Wire up application handlers and business logic
- **S4**: Connect domain models and validation
- **S5**: Full persistence implementation with Entity Framework

### 1.3 Exit Criteria

- ✅ All API contracts defined and documented
- ✅ Request/response models created following Axon conventions
- ✅ Endpoints return stub responses (200 OK with mock data)
- ✅ Build passes with no warnings
- ✅ OpenAPI documentation generated
- ✅ FastEndpoints integration follows existing patterns

---

## 2. Architecture Integration

### 2.1 Following Existing Patterns

The authentication endpoints will follow the established Axon Backend patterns:

**Base Class Pattern**: Inherit from `BaseMappedEndpoint<TRequest, TResponse>` (like `ChatTurnEndpoint`)
**Mapping Chain**: Request → Command → Execute → Response using MapExecuteMap pattern
**Error Handling**: Return `Result<TResponse, Error>` following the Result pattern
**Validation**: Use FluentValidation for request validation
**Configuration**: Use `Configure()` method for route and metadata setup

### 2.2 Module Structure

```
src/Api/
├── Endpoints/V1/Identity/
│   ├── Commands/
│   │   └── ExchangeToken/
│   │       ├── ExchangeTokenEndpoint.cs
│   │       ├── ExchangeTokenRequestValidator.cs
│   │       └── ExchangeTokenRequest.cs (internal command)
│   └── Queries/
│       └── GetCurrentUser/
│           ├── GetCurrentUserEndpoint.cs
│           └── GetCurrentUserQuery.cs (internal query)
├── Endpoints/V1/Webhooks/
│   └── Dynamic/
│       ├── ProcessDynamicWebhookEndpoint.cs
│       └── ProcessDynamicWebhookRequestValidator.cs
├── Contracts/V1/Identity/
│   ├── Authentication/
│   │   ├── ExchangeTokenRequestDto.cs
│   │   ├── ExchangeTokenResponseDto.cs
│   │   ├── CurrentUserResponseDto.cs
│   │   └── WalletDto.cs
│   ├── Webhooks/
│   │   ├── DynamicWebhookEventDto.cs
│   │   ├── DynamicWebhookResponseDto.cs
│   │   └── WebhookEventDataDto.cs
│   └── Common/
│       ├── AuthenticationErrorDto.cs
│       └── ProblemDetailsDto.cs
└── Modules/
    └── IdentityApiModule.cs
```

---

## 3. Authentication Endpoints

### 3.1 Exchange Token Endpoint

**Purpose**: Exchange Dynamic.xyz JWT for Axon user context and return local user data.

#### 3.1.1 Endpoint Implementation

```csharp
// src/Api/Endpoints/V1/Identity/Commands/ExchangeToken/ExchangeTokenEndpoint.cs

namespace Axon.Api.Endpoints.V1.Identity.Commands.ExchangeToken;

public sealed class ExchangeTokenEndpoint 
    : BaseMappedEndpoint<ExchangeTokenRequestDto, ExchangeTokenResponseDto>
{
    private const string Route = "/api/v1/auth/exchange";
    private const string Tag = "Authentication";

    private readonly ILogger<ExchangeTokenEndpoint> _logger;

    public ExchangeTokenEndpoint(ILogger<ExchangeTokenEndpoint> logger) : base(logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override void Configure()
    {
        Post(Route);
        AllowAnonymous();
        
        Summary(s =>
        {
            s.Summary = "Exchange Dynamic.xyz JWT token for Axon user context";
            s.Description = "Validates Dynamic.xyz JWT token and returns local user information with connected wallets";
            s.ExampleRequest = new ExchangeTokenRequestDto
            {
                AuthToken = "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9..."
            };
            s.Responses[200] = "Token validated successfully, user context returned";
            s.Responses[401] = "Invalid or expired JWT token";
            s.Responses[400] = "Bad request (validation failed)";
            s.Responses[500] = "Internal server error";
            s.Responses[502] = "Dynamic.xyz API unavailable";
        });

        Tags(Tag);
        
        // Add idempotency support using header
        Options(o => o.WithHeader("Idempotency-Key", "Optional idempotency key for safe retries"));
    }

    protected override async Task<Result<ExchangeTokenResponseDto, Error>> ExecuteAsync(
        ExchangeTokenRequestDto request,
        CancellationToken ct)
    {
        // S1: Return stub response with mock data
        var mockResponse = new ExchangeTokenResponseDto
        {
            UserId = "usr_2Z4e8K9mNp3QrS7T",
            DynamicUserId = Guid.Parse("95b11417-f18f-457f-8804-68e361f9164f"),
            Email = "user@example.com",
            DisplayName = "John Doe",
            Username = "johndoe",
            Wallets = new List<WalletDto>
            {
                new()
                {
                    Id = "wlt_8X2nMk9P4qR5sT7U",
                    DynamicWalletId = Guid.Parse("a7c11234-e89b-12d3-a456-426614174000"),
                    Address = "So11111111111111111111111111111111111111112",
                    Chain = "SOL",
                    Provider = "phantom",
                    WalletName = "Main Solana Wallet",
                    LastSelectedAt = DateTime.UtcNow.AddHours(-1)
                }
            },
            SyncedAt = DateTime.UtcNow,
            SyncStatus = "completed"
        };

        _logger.LogInformation("S1 Stub: Returning mock user context for token validation");
        return Result<ExchangeTokenResponseDto, Error>.Success(mockResponse);
    }
}
```

#### 3.1.2 Request DTO

```csharp
// src/Api/Contracts/V1/Identity/Authentication/ExchangeTokenRequestDto.cs

namespace Axon.Api.Contracts.V1.Identity.Authentication;

/// <summary>
/// Request to exchange Dynamic.xyz JWT token for Axon user context
/// </summary>
public sealed record ExchangeTokenRequestDto
{
    /// <summary>
    /// Dynamic.xyz JWT token from frontend authentication
    /// </summary>
    /// <example>eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9...</example>
    [JsonPropertyName("authToken")]
    public required string AuthToken { get; init; }

    /// <summary>
    /// Optional flag to force refresh of user data from Dynamic.xyz
    /// </summary>
    /// <example>false</example>
    [JsonPropertyName("forceRefresh")]
    public bool ForceRefresh { get; init; } = false;
}
```

#### 3.1.3 Response DTO

```csharp
// src/Api/Contracts/V1/Identity/Authentication/ExchangeTokenResponseDto.cs

namespace Axon.Api.Contracts.V1.Identity.Authentication;

/// <summary>
/// Response containing validated user context and wallet information
/// </summary>
public sealed record ExchangeTokenResponseDto
{
    /// <summary>
    /// Axon internal user identifier (strong ID)
    /// </summary>
    /// <example>usr_2Z4e8K9mNp3QrS7T</example>
    [JsonPropertyName("userId")]
    public required string UserId { get; init; }

    /// <summary>
    /// Dynamic.xyz user identifier
    /// </summary>
    /// <example>95b11417-f18f-457f-8804-68e361f9164f</example>
    [JsonPropertyName("dynamicUserId")]
    public required Guid DynamicUserId { get; init; }

    /// <summary>
    /// User's verified email address
    /// </summary>
    /// <example>user@example.com</example>
    [JsonPropertyName("email")]
    public required string Email { get; init; }

    /// <summary>
    /// User's display name (optional)
    /// </summary>
    /// <example>John Doe</example>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    /// <summary>
    /// User's username (optional)
    /// </summary>
    /// <example>johndoe</example>
    [JsonPropertyName("username")]
    public string? Username { get; init; }

    /// <summary>
    /// List of connected wallets
    /// </summary>
    [JsonPropertyName("wallets")]
    public required IReadOnlyList<WalletDto> Wallets { get; init; }

    /// <summary>
    /// Timestamp when user data was last synchronized
    /// </summary>
    /// <example>2025-08-27T12:00:00Z</example>
    [JsonPropertyName("syncedAt")]
    public required DateTime SyncedAt { get; init; }

    /// <summary>
    /// Current synchronization status
    /// </summary>
    /// <example>completed</example>
    [JsonPropertyName("syncStatus")]
    public required string SyncStatus { get; init; }
}
```

#### 3.1.4 Wallet DTO

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
    /// <example>wlt_8X2nMk9P4qR5sT7U</example>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// Dynamic.xyz wallet identifier
    /// </summary>
    /// <example>a7c11234-e89b-12d3-a456-426614174000</example>
    [JsonPropertyName("dynamicWalletId")]
    public required Guid DynamicWalletId { get; init; }

    /// <summary>
    /// Wallet public address/key
    /// </summary>
    /// <example>So11111111111111111111111111111111111111112</example>
    [JsonPropertyName("address")]
    public required string Address { get; init; }

    /// <summary>
    /// Blockchain type
    /// </summary>
    /// <example>SOL</example>
    [JsonPropertyName("chain")]
    public required string Chain { get; init; }

    /// <summary>
    /// Wallet provider type
    /// </summary>
    /// <example>phantom</example>
    [JsonPropertyName("provider")]
    public required string Provider { get; init; }

    /// <summary>
    /// User-assigned wallet name (optional)
    /// </summary>
    /// <example>Main Solana Wallet</example>
    [JsonPropertyName("walletName")]
    public string? WalletName { get; init; }

    /// <summary>
    /// Timestamp when wallet was last selected by user
    /// </summary>
    /// <example>2025-08-27T11:30:00Z</example>
    [JsonPropertyName("lastSelectedAt")]
    public DateTime? LastSelectedAt { get; init; }

    /// <summary>
    /// Timestamp when wallet was connected
    /// </summary>
    /// <example>2025-08-01T10:00:00Z</example>
    [JsonPropertyName("connectedAt")]
    public DateTime? ConnectedAt { get; init; }
}
```

#### 3.1.5 Request Validator

```csharp
// src/Api/Endpoints/V1/Identity/Commands/ExchangeToken/ExchangeTokenRequestValidator.cs

namespace Axon.Api.Endpoints.V1.Identity.Commands.ExchangeToken;

public sealed class ExchangeTokenRequestValidator : Validator<ExchangeTokenRequestDto>
{
    public ExchangeTokenRequestValidator()
    {
        RuleFor(x => x.AuthToken)
            .NotEmpty()
            .WithMessage("AuthToken is required")
            .MinimumLength(10)
            .WithMessage("AuthToken must be a valid JWT token")
            .Must(BeValidJwtFormat)
            .WithMessage("AuthToken must be a valid JWT format");
    }

    private static bool BeValidJwtFormat(string token)
    {
        // Basic JWT format validation (3 parts separated by dots)
        return token.Split('.').Length == 3;
    }
}
```

### 3.2 Get Current User Endpoint

**Purpose**: Return current authenticated user profile with wallet information (local data only).

#### 3.2.1 Endpoint Implementation

```csharp
// src/Api/Endpoints/V1/Identity/Queries/GetCurrentUser/GetCurrentUserEndpoint.cs

namespace Axon.Api.Endpoints.V1.Identity.Queries.GetCurrentUser;

public sealed class GetCurrentUserEndpoint : EndpointWithoutRequest<CurrentUserResponseDto>
{
    private const string Route = "/api/v1/auth/me";
    private const string Tag = "Authentication";

    private readonly ILogger<GetCurrentUserEndpoint> _logger;

    public GetCurrentUserEndpoint(ILogger<GetCurrentUserEndpoint> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override void Configure()
    {
        Get(Route);
        AllowAnonymous(); // S1: Allow anonymous for testing, will require auth in S2+
        
        Summary(s =>
        {
            s.Summary = "Get current authenticated user profile";
            s.Description = "Returns current user information with connected wallets (local data only)";
            s.Responses[200] = "User profile retrieved successfully";
            s.Responses[401] = "User not authenticated";
            s.Responses[404] = "User data not found";
            s.Responses[500] = "Internal server error";
        });

        Tags(Tag);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // S1: Return stub response with mock data
        var mockResponse = new CurrentUserResponseDto
        {
            User = new UserProfileDto
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
            Wallets = new List<WalletDto>
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
                },
                new()
                {
                    Id = "wlt_7Y1mLj8O3pQ4rS6T",
                    DynamicWalletId = Guid.Parse("b8d22345-f90c-23e4-b567-537725285111"),
                    Address = "0x742d35Cc6bF4532a35b8c5F9D4476D6a6B8C4aF6",
                    Chain = "ETH",
                    Provider = "metamask",
                    WalletName = "MetaMask Wallet",
                    LastSelectedAt = DateTime.UtcNow.AddDays(-2),
                    ConnectedAt = DateTime.UtcNow.AddDays(-20)
                }
            },
            SyncedAt = DateTime.UtcNow.AddMinutes(-5),
            SyncStatus = "completed"
        };

        _logger.LogInformation("S1 Stub: Returning mock current user profile");
        await SendOkAsync(mockResponse, ct);
    }
}
```

#### 3.2.2 Response DTOs

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
    [JsonPropertyName("user")]
    public required UserProfileDto User { get; init; }

    /// <summary>
    /// Connected wallets
    /// </summary>
    [JsonPropertyName("wallets")]
    public required IReadOnlyList<WalletDto> Wallets { get; init; }

    /// <summary>
    /// Last sync timestamp
    /// </summary>
    /// <example>2025-08-27T12:00:00Z</example>
    [JsonPropertyName("syncedAt")]
    public required DateTime SyncedAt { get; init; }

    /// <summary>
    /// Data synchronization status
    /// </summary>
    /// <example>completed</example>
    [JsonPropertyName("syncStatus")]
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
    /// <example>usr_2Z4e8K9mNp3QrS7T</example>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// Dynamic.xyz user identifier
    /// </summary>
    /// <example>95b11417-f18f-457f-8804-68e361f9164f</example>
    [JsonPropertyName("dynamicUserId")]
    public required Guid DynamicUserId { get; init; }

    /// <summary>
    /// User's email address
    /// </summary>
    /// <example>user@example.com</example>
    [JsonPropertyName("email")]
    public required string Email { get; init; }

    /// <summary>
    /// User's display name
    /// </summary>
    /// <example>John Doe</example>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    /// <summary>
    /// User's username
    /// </summary>
    /// <example>johndoe</example>
    [JsonPropertyName("username")]
    public string? Username { get; init; }

    /// <summary>
    /// First visit timestamp
    /// </summary>
    /// <example>2025-08-01T10:00:00Z</example>
    [JsonPropertyName("firstVisit")]
    public DateTime? FirstVisit { get; init; }

    /// <summary>
    /// Last visit timestamp
    /// </summary>
    /// <example>2025-08-27T11:55:00Z</example>
    [JsonPropertyName("lastVisit")]
    public DateTime? LastVisit { get; init; }

    /// <summary>
    /// User metadata from Dynamic.xyz
    /// </summary>
    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
}
```

---

## 4. Webhook Endpoints

### 4.1 Dynamic.xyz Webhook Receiver

**Purpose**: Process Dynamic.xyz lifecycle events for real-time data synchronization.

#### 4.1.1 Endpoint Implementation

```csharp
// src/Api/Endpoints/V1/Webhooks/Dynamic/ProcessDynamicWebhookEndpoint.cs

namespace Axon.Api.Endpoints.V1.Webhooks.Dynamic;

public sealed class ProcessDynamicWebhookEndpoint : Endpoint<DynamicWebhookEventDto, DynamicWebhookResponseDto>
{
    private const string Route = "/api/webhooks/dynamic";
    private const string Tag = "Webhooks";

    private readonly ILogger<ProcessDynamicWebhookEndpoint> _logger;

    public ProcessDynamicWebhookEndpoint(ILogger<ProcessDynamicWebhookEndpoint> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override void Configure()
    {
        Post(Route);
        AllowAnonymous(); // Webhook endpoints are typically anonymous but use signature validation
        
        Summary(s =>
        {
            s.Summary = "Process Dynamic.xyz webhook events";
            s.Description = "Receives and processes lifecycle events from Dynamic.xyz for real-time synchronization";
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
            s.Responses[200] = "Webhook processed successfully";
            s.Responses[202] = "Webhook accepted for processing";
            s.Responses[400] = "Invalid webhook signature or format";
            s.Responses[500] = "Processing failed";
        });

        Tags(Tag);
        
        // Expected headers from Dynamic.xyz
        Description(d => d.WithHeaders("X-Dynamic-Signature", "Dynamic.xyz HMAC signature"));
    }

    public override async Task HandleAsync(DynamicWebhookEventDto req, CancellationToken ct)
    {
        _logger.LogInformation("S1 Stub: Processing webhook event {EventType} with ID {EventId}", 
            req.EventName, req.EventId);

        // S1: Basic validation and stub processing
        if (string.IsNullOrEmpty(req.EventId) || string.IsNullOrEmpty(req.EventName))
        {
            await SendAsync(new DynamicWebhookResponseDto
            {
                Received = false,
                ProcessedAt = DateTime.UtcNow,
                SyncScheduled = false,
                Acknowledgment = new WebhookAcknowledgmentDto
                {
                    EventId = req.EventId ?? "",
                    WebhookId = req.WebhookId ?? "",
                    Status = "failed",
                    RetryCount = 0,
                    ErrorMessage = "Invalid event format"
                }
            }, 400, ct);
            return;
        }

        // S1: Simulate processing different event types
        var response = new DynamicWebhookResponseDto
        {
            Received = true,
            ProcessedAt = DateTime.UtcNow,
            SyncScheduled = req.EventName.Contains("users.") || req.EventName.Contains("wallets."),
            Acknowledgment = new WebhookAcknowledgmentDto
            {
                EventId = req.EventId,
                WebhookId = req.WebhookId ?? "",
                Status = "processed",
                RetryCount = 0
            }
        };

        await SendOkAsync(response, ct);
    }
}
```

#### 4.1.2 Webhook DTOs

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
    /// <example>evt_user_created_123</example>
    [JsonPropertyName("eventId")]
    public required string EventId { get; init; }

    /// <summary>
    /// Event type name
    /// </summary>
    /// <example>users.created</example>
    [JsonPropertyName("eventName")]
    public required string EventName { get; init; }

    /// <summary>
    /// Webhook configuration identifier
    /// </summary>
    /// <example>wh_abc123def456</example>
    [JsonPropertyName("webhookId")]
    public string? WebhookId { get; init; }

    /// <summary>
    /// Event creation timestamp
    /// </summary>
    /// <example>2025-08-27T12:00:00Z</example>
    [JsonPropertyName("createdAt")]
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Event-specific data payload
    /// </summary>
    [JsonPropertyName("data")]
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
    /// <example>true</example>
    [JsonPropertyName("received")]
    public required bool Received { get; init; }

    /// <summary>
    /// Processing completion timestamp
    /// </summary>
    /// <example>2025-08-27T12:00:02Z</example>
    [JsonPropertyName("processedAt")]
    public required DateTime ProcessedAt { get; init; }

    /// <summary>
    /// Whether data synchronization was scheduled
    /// </summary>
    /// <example>true</example>
    [JsonPropertyName("syncScheduled")]
    public required bool SyncScheduled { get; init; }

    /// <summary>
    /// Processing acknowledgment details
    /// </summary>
    [JsonPropertyName("acknowledgment")]
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
    /// <example>evt_user_created_123</example>
    [JsonPropertyName("eventId")]
    public required string EventId { get; init; }

    /// <summary>
    /// Webhook configuration ID
    /// </summary>
    /// <example>wh_abc123def456</example>
    [JsonPropertyName("webhookId")]
    public required string WebhookId { get; init; }

    /// <summary>
    /// Processing status
    /// </summary>
    /// <example>processed</example>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>
    /// Retry attempt count
    /// </summary>
    /// <example>0</example>
    [JsonPropertyName("retryCount")]
    public required int RetryCount { get; init; }

    /// <summary>
    /// Error message if processing failed
    /// </summary>
    /// <example>null</example>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; init; }
}
```

#### 4.1.3 Webhook Validator

```csharp
// src/Api/Endpoints/V1/Webhooks/Dynamic/ProcessDynamicWebhookRequestValidator.cs

namespace Axon.Api.Endpoints.V1.Webhooks.Dynamic;

public sealed class ProcessDynamicWebhookRequestValidator : Validator<DynamicWebhookEventDto>
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

        RuleFor(x => x.CreatedAt)
            .LessThanOrEqualTo(DateTime.UtcNow.AddMinutes(5))
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

## 5. Error Response Models

### 5.1 Standardized Error Responses

Following RFC 7807 Problem Details for HTTP APIs standard.

```csharp
// src/Api/Contracts/V1/Identity/Common/ProblemDetailsDto.cs

namespace Axon.Api.Contracts.V1.Identity.Common;

/// <summary>
/// RFC 7807 Problem Details for HTTP APIs
/// </summary>
public sealed record ProblemDetailsDto
{
    /// <summary>
    /// URI reference that identifies the problem type
    /// </summary>
    /// <example>https://axon.example.com/probs/auth/invalid-token</example>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>
    /// Short, human-readable summary of the problem type
    /// </summary>
    /// <example>Invalid JWT Token</example>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>
    /// HTTP status code
    /// </summary>
    /// <example>401</example>
    [JsonPropertyName("status")]
    public required int Status { get; init; }

    /// <summary>
    /// Human-readable explanation specific to this occurrence
    /// </summary>
    /// <example>JWT signature validation failed using RS256 algorithm</example>
    [JsonPropertyName("detail")]
    public required string Detail { get; init; }

    /// <summary>
    /// Unique error code for this problem type
    /// </summary>
    /// <example>AUTH001</example>
    [JsonPropertyName("code")]
    public required string Code { get; init; }

    /// <summary>
    /// Unique identifier for this request
    /// </summary>
    /// <example>req_abc123def456</example>
    [JsonPropertyName("correlationId")]
    public required string CorrelationId { get; init; }

    /// <summary>
    /// Timestamp when the error occurred
    /// </summary>
    /// <example>2025-08-27T12:00:00Z</example>
    [JsonPropertyName("timestamp")]
    public required DateTime Timestamp { get; init; }

    /// <summary>
    /// Additional error-specific properties
    /// </summary>
    [JsonPropertyName("extensions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, object>? Extensions { get; init; }
}
```

### 5.2 Authentication-Specific Errors

```csharp
// src/Api/Contracts/V1/Identity/Common/AuthenticationErrorDto.cs

namespace Axon.Api.Contracts.V1.Identity.Common;

/// <summary>
/// Authentication-specific error information
/// </summary>
public sealed record AuthenticationErrorDto
{
    /// <summary>
    /// Error code specific to authentication failures
    /// </summary>
    /// <example>AUTH001</example>
    [JsonPropertyName("errorCode")]
    public required string ErrorCode { get; init; }

    /// <summary>
    /// Detailed error message
    /// </summary>
    /// <example>JWT signature validation failed using RS256 algorithm</example>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>
    /// Suggested retry action
    /// </summary>
    /// <example>Obtain a new token from Dynamic.xyz and retry</example>
    [JsonPropertyName("suggestedAction")]
    public string? SuggestedAction { get; init; }

    /// <summary>
    /// Whether the error is retryable
    /// </summary>
    /// <example>false</example>
    [JsonPropertyName("retryable")]
    public required bool Retryable { get; init; }

    /// <summary>
    /// Additional error context
    /// </summary>
    [JsonPropertyName("context")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Context { get; init; }
}
```

---

## 6. API Module Configuration

### 6.1 Identity API Module

```csharp
// src/Api/Modules/IdentityApiModule.cs

namespace Axon.Api.Modules;

/// <summary>
/// API module for identity and authentication endpoints
/// </summary>
public sealed class IdentityApiModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1")
            .WithTags("Identity & Authentication")
            .WithOpenApi();

        // Configure authentication endpoints
        group.MapGroup("/auth")
            .WithTags("Authentication")
            .MapEndpoint<ExchangeTokenEndpoint>()
            .MapEndpoint<GetCurrentUserEndpoint>();

        // Configure webhook endpoints  
        group.MapGroup("/webhooks")
            .WithTags("Webhooks")
            .MapEndpoint<ProcessDynamicWebhookEndpoint>();
    }
}
```

---

## 7. OpenAPI Documentation

### 7.1 Swagger Configuration

The endpoints will generate comprehensive OpenAPI documentation including:

- **Request/Response Examples**: All DTOs include example values
- **Error Response Documentation**: All possible HTTP status codes documented
- **Schema Validation**: Request validation rules reflected in schema
- **Authentication Requirements**: Clearly marked which endpoints require auth
- **Header Requirements**: Special headers (like signatures) documented

### 7.2 Generated Documentation

The following endpoints will be documented:

```yaml
/api/v1/auth/exchange:
  post:
    summary: Exchange Dynamic.xyz JWT token for Axon user context
    tags: [Authentication]
    
/api/v1/auth/me:
  get:
    summary: Get current authenticated user profile
    tags: [Authentication]
    
/api/webhooks/dynamic:
  post:
    summary: Process Dynamic.xyz webhook events
    tags: [Webhooks]
```

---

## 8. Testing Strategy

### 8.1 Unit Tests (S1 Stage)

```csharp
// Tests/Api.Tests/Endpoints/V1/Identity/Commands/ExchangeToken/ExchangeTokenEndpointTests.cs

namespace Axon.Tests.Api.Endpoints.V1.Identity.Commands.ExchangeToken;

public sealed class ExchangeTokenEndpointTests
{
    [Test]
    public async Task HandleAsync_ValidRequest_ReturnsStubResponse()
    {
        // Arrange
        var endpoint = new ExchangeTokenEndpoint(Mock.Of<ILogger<ExchangeTokenEndpoint>>());
        var request = new ExchangeTokenRequestDto
        {
            AuthToken = "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.stub.token"
        };

        // Act
        var result = await endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<Result<ExchangeTokenResponseDto, Error>>();
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().StartWith("usr_");
        result.Value.SyncStatus.Should().Be("completed");
    }

    [Test]  
    public async Task HandleAsync_EmptyToken_ReturnsValidationError()
    {
        // Arrange  
        var validator = new ExchangeTokenRequestValidator();
        var request = new ExchangeTokenRequestDto { AuthToken = "" };

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(request.AuthToken));
    }
}
```

### 8.2 Integration Tests (S1 Stage)

```csharp
// Tests/Api.Tests/Integration/IdentityEndpointsTests.cs

namespace Axon.Tests.Api.Integration;

public sealed class IdentityEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public IdentityEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Test]
    public async Task POST_AuthExchange_ReturnsStubData()
    {
        // Arrange
        var request = new ExchangeTokenRequestDto
        {
            AuthToken = "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.stub.token"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/exchange", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<ExchangeTokenResponseDto>();
        content.Should().NotBeNull();
        content!.UserId.Should().StartWith("usr_");
    }

    [Test]
    public async Task GET_AuthMe_ReturnsCurrentUser()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<CurrentUserResponseDto>();
        content.Should().NotBeNull();
        content!.User.Email.Should().Be("user@example.com");
    }

    [Test]
    public async Task POST_WebhookDynamic_ProcessesEvent()
    {
        // Arrange
        var webhook = new DynamicWebhookEventDto
        {
            EventId = "evt_test_123",
            EventName = "users.created",
            CreatedAt = DateTime.UtcNow,
            Data = new Dictionary<string, object> { ["userId"] = "test-user-id" }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/webhooks/dynamic", webhook);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<DynamicWebhookResponseDto>();
        content.Should().NotBeNull();
        content!.Received.Should().BeTrue();
    }
}
```

---

## 9. Implementation Checklist

### 9.1 S1 Stage Tasks

- [ ] **Create API Contracts Directory Structure**
  - [ ] `src/Api/Endpoints/V1/Identity/` with Commands and Queries
  - [ ] `src/Api/Endpoints/V1/Webhooks/Dynamic/`  
  - [ ] `src/Api/Contracts/V1/Identity/` with Authentication, Webhooks, Common

- [ ] **Implement Authentication Endpoints**
  - [ ] `ExchangeTokenEndpoint` with stub implementation
  - [ ] `GetCurrentUserEndpoint` with mock data
  - [ ] Request/Response DTOs with proper JSON serialization
  - [ ] FluentValidation validators

- [ ] **Implement Webhook Endpoints**
  - [ ] `ProcessDynamicWebhookEndpoint` with basic validation
  - [ ] Webhook DTOs with all event types support
  - [ ] Request validation for event formats

- [ ] **Create Error Response Models**
  - [ ] RFC 7807 Problem Details implementation
  - [ ] Authentication-specific error DTOs
  - [ ] Proper error code definitions

- [ ] **Configure API Module**
  - [ ] `IdentityApiModule` with route configuration
  - [ ] Proper endpoint registration and grouping
  - [ ] OpenAPI documentation tags

- [ ] **Add Unit Tests**
  - [ ] Endpoint behavior tests with stub responses
  - [ ] Validation tests for all request DTOs
  - [ ] Error handling tests

- [ ] **Add Integration Tests**  
  - [ ] Full HTTP request/response tests
  - [ ] OpenAPI schema validation
  - [ ] Error response format validation

### 9.2 Exit Criteria Verification

- [ ] **All API contracts defined and documented**
  - [ ] All DTOs have proper JSON attributes and examples
  - [ ] All endpoints have comprehensive summaries and response documentation
  - [ ] All validation rules are implemented with FluentValidation

- [ ] **Endpoints return stub responses (200 OK with mock data)**
  - [ ] `/api/v1/auth/exchange` returns mock user with wallets
  - [ ] `/api/v1/auth/me` returns complete user profile
  - [ ] `/api/webhooks/dynamic` accepts and acknowledges all event types

- [ ] **Build passes with no warnings**
  - [ ] All code follows Axon Backend conventions (file-scoped namespaces, records, etc.)
  - [ ] No nullable reference type warnings
  - [ ] All required properties properly annotated

- [ ] **OpenAPI documentation generated**
  - [ ] Swagger UI shows all endpoints with examples
  - [ ] Request/response schemas are complete
  - [ ] Error responses are documented

- [ ] **FastEndpoints integration follows existing patterns**
  - [ ] Endpoints inherit from appropriate base classes
  - [ ] Validation is implemented consistently  
  - [ ] Route configuration follows established patterns

---

## 10. Next Steps

Upon completion of S1, the foundation will be ready for:

**S2 - Infrastructure Integration**: Replace stub responses with real Dynamic.xyz API calls
**S3 - Application Layer**: Add CQRS command/query handlers with business logic
**S4 - Domain Layer**: Implement domain models, aggregates, and business rules
**S5 - Persistence Layer**: Add Entity Framework, data mirroring, and webhooks

The API contracts established in S1 will remain stable throughout subsequent stages, ensuring consistent external interface while evolving internal implementation.