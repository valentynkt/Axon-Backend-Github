# Dynamic.xyz C# Integration Guide

## Overview

This guide provides comprehensive instructions for integrating Dynamic.xyz authentication into .NET applications, specifically tailored for the Axon Backend's Clean Architecture and CQRS patterns.

Dynamic.xyz doesn't provide an official C# SDK, so this guide demonstrates how to build a robust integration using standard .NET libraries while following Axon Backend's architectural patterns including the Result pattern, Strong IDs, and functional programming principles.

## Prerequisites

- .NET 10.0+
- Basic understanding of JWT tokens
- Access to Dynamic.xyz dashboard and API tokens

## Required NuGet Packages

```xml
<PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="7.0.3" />
<PackageReference Include="Microsoft.IdentityModel.Protocols.OpenIdConnect" Version="7.0.3" />
<PackageReference Include="Microsoft.Extensions.Http" Version="8.0.0" />
<PackageReference Include="System.Text.Json" Version="8.0.4" />
```

## HTTP Client Setup

### 1. DynamicApiClient Configuration

```csharp
namespace Axon.BuildingBlocks.Infrastructure.HttpClients;

public sealed class DynamicApiClientOptions
{
    public const string SectionName = "DynamicApi";
    
    public required string BaseUrl { get; init; } = "https://app.dynamic.xyz/api/v0";
    public required string ApiToken { get; init; }
    public required string EnvironmentId { get; init; }
    public int TimeoutSeconds { get; init; } = 30;
}

public sealed class DynamicApiClient
{
    private readonly HttpClient _httpClient;
    private readonly DynamicApiClientOptions _options;
    private readonly ILogger<DynamicApiClient> _logger;

    public DynamicApiClient(
        HttpClient httpClient,
        IOptions<DynamicApiClientOptions> options,
        ILogger<DynamicApiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", _options.ApiToken);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    public async Task<Result<T>> GetAsync<T>(
        string endpoint, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                return Result<T>.Failure(await CreateErrorFromResponse(response));
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var data = JsonSerializer.Deserialize<T>(json, JsonSerializerOptions.Web);

            return data is not null 
                ? Result<T>.Success(data)
                : Result<T>.Failure(Error.Validation("Invalid response data"));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed for endpoint: {Endpoint}", endpoint);
            return Result<T>.Failure(Error.External("HTTP request failed", ex.Message));
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout for endpoint: {Endpoint}", endpoint);
            return Result<T>.Failure(Error.External("Request timeout", ex.Message));
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON deserialization failed for endpoint: {Endpoint}", endpoint);
            return Result<T>.Failure(Error.Validation("Invalid JSON response"));
        }
    }

    private static async Task<Error> CreateErrorFromResponse(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => Error.Authentication("Invalid API token"),
            HttpStatusCode.Forbidden => Error.Authorization("Access denied to resource"),
            HttpStatusCode.NotFound => Error.NotFound("Resource not found"),
            HttpStatusCode.BadRequest => Error.Validation($"Bad request: {content}"),
            _ => Error.External($"API error ({(int)response.StatusCode})", content)
        };
    }
}
```

### 2. Service Registration

```csharp
namespace Axon.BuildingBlocks.Infrastructure.Extensions;

public static class DynamicApiExtensions
{
    public static IServiceCollection AddDynamicApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<DynamicApiClientOptions>(
            configuration.GetSection(DynamicApiClientOptions.SectionName));

        services.AddHttpClient<DynamicApiClient>();

        services.AddSingleton<IDynamicUserService, DynamicUserService>();
        services.AddSingleton<IDynamicWalletService, DynamicWalletService>();
        services.AddSingleton<IJwtValidationService, JwtValidationService>();

        return services;
    }
}
```

## Domain Models

### 1. Strong IDs

```csharp
namespace Axon.Modules.Identity.Domain.Users;

public readonly record struct DynamicUserId(Guid Value) : IStrongId
{
    public static DynamicUserId New() => new(Guid.NewGuid());
    public static DynamicUserId From(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString();
}

public readonly record struct DynamicWalletId(Guid Value) : IStrongId
{
    public static DynamicWalletId New() => new(Guid.NewGuid());
    public static DynamicWalletId From(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString();
}

public readonly record struct DynamicEnvironmentId(Guid Value) : IStrongId
{
    public static DynamicEnvironmentId New() => new(Guid.NewGuid());
    public static DynamicEnvironmentId From(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString();
}
```

### 2. Value Objects and Entities

```csharp
namespace Axon.Modules.Identity.Domain.Users;

public sealed record DynamicUser
{
    public required DynamicUserId Id { get; init; }
    public required DynamicEnvironmentId ProjectEnvironmentId { get; init; }
    public required string Email { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Username { get; init; }
    public string? PhoneNumber { get; init; }
    public string? Country { get; init; }
    public bool NewUser { get; init; }
    public DateTime FirstVisit { get; init; }
    public DateTime LastVisit { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public IReadOnlyList<DynamicWallet> Wallets { get; init; } = [];
    public IReadOnlyList<string> Lists { get; init; } = [];
    public string Scope { get; init; } = "";
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = 
        new Dictionary<string, object>();
}

public sealed record DynamicWallet
{
    public required DynamicWalletId Id { get; init; }
    public required string Name { get; init; }
    public required BlockchainType Chain { get; init; }
    public required string PublicKey { get; init; }
    public required WalletProvider Provider { get; init; }
    public WalletProperties? Properties { get; init; }
    public DateTime? LastSelectedAt { get; init; }
}

public sealed record WalletProperties
{
    public string? TurnkeySubOrganizationId { get; init; }
    public string? TurnkeyPrivateKeyId { get; init; }
    public string? TurnkeyHDWalletId { get; init; }
    public bool IsAuthenticatorAttached { get; init; }
    public string? TurnkeyUserId { get; init; }
    public bool IsSessionKeyCompatible { get; init; }
    public string? Version { get; init; }
    public string? EcdsaProviderType { get; init; }
    public string? EntryPointVersion { get; init; }
    public string? KernelVersion { get; init; }
}

public enum BlockchainType
{
    ETH,
    EVM, 
    SOL,
    BTC,
    ALGO,
    FLOW,
    STARK,
    COSMOS,
    ECLIPSE,
    SUI
}

public enum WalletProvider
{
    BrowserExtension,
    WalletConnect,
    Email,
    Social,
    Embedded,
    Passkey
}
```

### 3. API Response DTOs

```csharp
namespace Axon.BuildingBlocks.Infrastructure.HttpClients.Dynamic.Responses;

public sealed record DynamicUserResponse
{
    [JsonPropertyName("user")]
    public required UserDto User { get; init; }
}

public sealed record UserDto
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("projectEnvironmentId")]
    public required string ProjectEnvironmentId { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("firstName")]
    public string? FirstName { get; init; }

    [JsonPropertyName("lastName")]
    public string? LastName { get; init; }

    [JsonPropertyName("username")]
    public string? Username { get; init; }

    [JsonPropertyName("phoneNumber")]
    public string? PhoneNumber { get; init; }

    [JsonPropertyName("country")]
    public string? Country { get; init; }

    [JsonPropertyName("newUser")]
    public bool NewUser { get; init; }

    [JsonPropertyName("firstVisit")]
    public DateTime FirstVisit { get; init; }

    [JsonPropertyName("lastVisit")]
    public DateTime LastVisit { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; init; }

    [JsonPropertyName("wallets")]
    public IReadOnlyList<WalletDto> Wallets { get; init; } = [];

    [JsonPropertyName("lists")]
    public IReadOnlyList<string> Lists { get; init; } = [];

    [JsonPropertyName("scope")]
    public string Scope { get; init; } = "";

    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = 
        new Dictionary<string, object>();
}

public sealed record WalletDto
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("chain")]
    public required string Chain { get; init; }

    [JsonPropertyName("publicKey")]
    public required string PublicKey { get; init; }

    [JsonPropertyName("provider")]
    public required string Provider { get; init; }

    [JsonPropertyName("properties")]
    public WalletPropertiesDto? Properties { get; init; }

    [JsonPropertyName("lastSelectedAt")]
    public string? LastSelectedAt { get; init; }
}

public sealed record WalletPropertiesDto
{
    [JsonPropertyName("turnkeySubOrganizationId")]
    public string? TurnkeySubOrganizationId { get; init; }

    [JsonPropertyName("turnkeyPrivateKeyId")]
    public string? TurnkeyPrivateKeyId { get; init; }

    [JsonPropertyName("turnkeyHDWalletId")]
    public string? TurnkeyHDWalletId { get; init; }

    [JsonPropertyName("isAuthenticatorAttached")]
    public bool IsAuthenticatorAttached { get; init; }

    [JsonPropertyName("turnkeyUserId")]
    public string? TurnkeyUserId { get; init; }

    [JsonPropertyName("isSessionKeyCompatible")]
    public bool IsSessionKeyCompatible { get; init; }

    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("ecdsaProviderType")]
    public string? EcdsaProviderType { get; init; }

    [JsonPropertyName("entryPointVersion")]
    public string? EntryPointVersion { get; init; }

    [JsonPropertyName("kernelVersion")]
    public string? KernelVersion { get; init; }
}

public sealed record DynamicWalletsResponse
{
    [JsonPropertyName("count")]
    public required int Count { get; init; }

    [JsonPropertyName("wallets")]
    public required IReadOnlyList<WalletDto> Wallets { get; init; }
}
```

## Service Implementation

### 1. User Service

```csharp
namespace Axon.BuildingBlocks.Infrastructure.HttpClients.Dynamic;

public interface IDynamicUserService
{
    Task<Result<DynamicUser>> GetUserByIdAsync(
        DynamicUserId userId,
        CancellationToken cancellationToken = default);
    
    Task<Result<IReadOnlyList<DynamicWallet>>> GetUserWalletsAsync(
        DynamicUserId userId,
        CancellationToken cancellationToken = default);
}

public sealed class DynamicUserService : IDynamicUserService
{
    private readonly DynamicApiClient _client;
    private readonly DynamicApiClientOptions _options;

    public DynamicUserService(
        DynamicApiClient client,
        IOptions<DynamicApiClientOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    public async Task<Result<DynamicUser>> GetUserByIdAsync(
        DynamicUserId userId,
        CancellationToken cancellationToken = default)
    {
        var endpoint = $"environments/{_options.EnvironmentId}/users/{userId}";
        
        var response = await _client.GetAsync<DynamicUserResponse>(endpoint, cancellationToken);
        
        return response.IsSuccess 
            ? Result<DynamicUser>.Success(MapToDomain(response.Value.User))
            : Result<DynamicUser>.Failure(response.Error);
    }

    public async Task<Result<IReadOnlyList<DynamicWallet>>> GetUserWalletsAsync(
        DynamicUserId userId,
        CancellationToken cancellationToken = default)
    {
        var endpoint = $"environments/{_options.EnvironmentId}/users/{userId}/wallets";
        
        var response = await _client.GetAsync<DynamicWalletsResponse>(endpoint, cancellationToken);
        
        return response.IsSuccess 
            ? Result<IReadOnlyList<DynamicWallet>>.Success(
                response.Value.Wallets.Select(MapWalletToDomain).ToList())
            : Result<IReadOnlyList<DynamicWallet>>.Failure(response.Error);
    }

    private static DynamicUser MapToDomain(UserDto dto) => new()
    {
        Id = DynamicUserId.From(dto.Id),
        ProjectEnvironmentId = DynamicEnvironmentId.From(dto.ProjectEnvironmentId),
        Email = dto.Email ?? "",
        FirstName = dto.FirstName,
        LastName = dto.LastName,
        Username = dto.Username,
        PhoneNumber = dto.PhoneNumber,
        Country = dto.Country,
        NewUser = dto.NewUser,
        FirstVisit = dto.FirstVisit,
        LastVisit = dto.LastVisit,
        CreatedAt = dto.CreatedAt,
        UpdatedAt = dto.UpdatedAt,
        Wallets = dto.Wallets.Select(MapWalletToDomain).ToList(),
        Lists = dto.Lists,
        Scope = dto.Scope,
        Metadata = dto.Metadata
    };

    private static DynamicWallet MapWalletToDomain(WalletDto dto) => new()
    {
        Id = DynamicWalletId.From(dto.Id),
        Name = dto.Name,
        Chain = Enum.Parse<BlockchainType>(dto.Chain, ignoreCase: true),
        PublicKey = dto.PublicKey,
        Provider = MapProviderToDomain(dto.Provider),
        Properties = dto.Properties is not null ? MapPropertiesToDomain(dto.Properties) : null,
        LastSelectedAt = dto.LastSelectedAt is not null 
            ? DateTime.Parse(dto.LastSelectedAt) 
            : null
    };

    private static WalletProvider MapProviderToDomain(string provider) => provider.ToLowerInvariant() switch
    {
        "browserextension" => WalletProvider.BrowserExtension,
        "walletconnect" => WalletProvider.WalletConnect,
        "email" => WalletProvider.Email,
        "social" => WalletProvider.Social,
        "embedded" => WalletProvider.Embedded,
        "passkey" => WalletProvider.Passkey,
        _ => WalletProvider.BrowserExtension
    };

    private static WalletProperties MapPropertiesToDomain(WalletPropertiesDto dto) => new()
    {
        TurnkeySubOrganizationId = dto.TurnkeySubOrganizationId,
        TurnkeyPrivateKeyId = dto.TurnkeyPrivateKeyId,
        TurnkeyHDWalletId = dto.TurnkeyHDWalletId,
        IsAuthenticatorAttached = dto.IsAuthenticatorAttached,
        TurnkeyUserId = dto.TurnkeyUserId,
        IsSessionKeyCompatible = dto.IsSessionKeyCompatible,
        Version = dto.Version,
        EcdsaProviderType = dto.EcdsaProviderType,
        EntryPointVersion = dto.EntryPointVersion,
        KernelVersion = dto.KernelVersion
    };
}
```

## JWT Validation Service

### 1. JWT Service Implementation

```csharp
namespace Axon.BuildingBlocks.Infrastructure.Authentication;

public interface IJwtValidationService
{
    Task<Result<DynamicJwtPayload>> ValidateTokenAsync(
        string token,
        CancellationToken cancellationToken = default);
}

public sealed record DynamicJwtPayload
{
    public required string Sub { get; init; }
    public required string Iss { get; init; }
    public required string Aud { get; init; }
    public required DateTime Exp { get; init; }
    public required DateTime Iat { get; init; }
    public required IReadOnlyList<string> Scopes { get; init; }
    public required string EnvironmentId { get; init; }
    public IReadOnlyList<string> Lists { get; init; } = [];
    public IReadOnlyDictionary<string, object> Claims { get; init; } = 
        new Dictionary<string, object>();
}

public sealed class JwtValidationService : IJwtValidationService
{
    private readonly HttpClient _httpClient;
    private readonly DynamicApiClientOptions _options;
    private readonly ILogger<JwtValidationService> _logger;
    private readonly IMemoryCache _cache;

    public JwtValidationService(
        HttpClient httpClient,
        IOptions<DynamicApiClientOptions> options,
        ILogger<JwtValidationService> logger,
        IMemoryCache cache)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _cache = cache;
    }

    public async Task<Result<DynamicJwtPayload>> ValidateTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var jwks = await GetJwksAsync(cancellationToken);
            if (jwks.IsFailure)
                return Result<DynamicJwtPayload>.Failure(jwks.Error);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = jwks.Value,
                ValidateIssuer = true,
                ValidIssuer = $"https://app.dynamic.xyz/api/v0/sdk/{_options.EnvironmentId}",
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(5)
            };

            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, validationParameters, out var validatedToken);
            
            var jwtToken = (JwtSecurityToken)validatedToken;
            var payload = MapToPayload(jwtToken);

            if (payload.Scopes.Contains("requiresAdditionalAuth"))
            {
                return Result<DynamicJwtPayload>.Failure(
                    Error.Authentication("Additional verification required"));
            }

            return Result<DynamicJwtPayload>.Success(payload);
        }
        catch (SecurityTokenExpiredException ex)
        {
            _logger.LogWarning(ex, "JWT token has expired");
            return Result<DynamicJwtPayload>.Failure(Error.Authentication("Token expired"));
        }
        catch (SecurityTokenInvalidSignatureException ex)
        {
            _logger.LogWarning(ex, "JWT token has invalid signature");
            return Result<DynamicJwtPayload>.Failure(Error.Authentication("Invalid token signature"));
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "JWT token validation failed");
            return Result<DynamicJwtPayload>.Failure(Error.Authentication("Invalid token"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during JWT validation");
            return Result<DynamicJwtPayload>.Failure(Error.External("JWT validation failed", ex.Message));
        }
    }

    private async Task<Result<IList<SecurityKey>>> GetJwksAsync(CancellationToken cancellationToken)
    {
        const string cacheKey = "dynamic_jwks";
        
        if (_cache.TryGetValue(cacheKey, out IList<SecurityKey>? cachedKeys))
            return Result<IList<SecurityKey>>.Success(cachedKeys!);

        try
        {
            var jwksUri = $"https://app.dynamic.xyz/api/v0/sdk/{_options.EnvironmentId}/.well-known/jwks";
            var configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                jwksUri,
                new OpenIdConnectConfigurationRetriever(),
                _httpClient);

            var config = await configManager.GetConfigurationAsync(cancellationToken);
            var keys = config.SigningKeys.ToList();

            _cache.Set(cacheKey, keys, TimeSpan.FromMinutes(10));
            
            return Result<IList<SecurityKey>>.Success(keys);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve JWKS");
            return Result<IList<SecurityKey>>.Failure(Error.External("JWKS retrieval failed", ex.Message));
        }
    }

    private static DynamicJwtPayload MapToPayload(JwtSecurityToken jwt)
    {
        var scopes = jwt.Claims.FirstOrDefault(c => c.Type == "scopes")?.Value ?? "";
        var lists = jwt.Claims.FirstOrDefault(c => c.Type == "lists")?.Value ?? "";
        
        return new DynamicJwtPayload
        {
            Sub = jwt.Subject,
            Iss = jwt.Issuer,
            Aud = jwt.Audiences.FirstOrDefault() ?? "",
            Exp = jwt.ValidTo,
            Iat = jwt.ValidFrom,
            Scopes = scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            EnvironmentId = jwt.Claims.FirstOrDefault(c => c.Type == "environment_id")?.Value ?? "",
            Lists = lists.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            Claims = jwt.Claims.ToDictionary(c => c.Type, c => (object)c.Value)
        };
    }
}
```

## FastEndpoints Integration

### 1. Authentication Endpoint

```csharp
namespace Axon.Api.Endpoints.Identity;

public sealed record AuthenticateRequest
{
    public required string AuthToken { get; init; }
}

public sealed record AuthenticateResponse
{
    public required string UserId { get; init; }
    public required string Email { get; init; }
    public required IReadOnlyList<string> Scopes { get; init; }
    public required IReadOnlyList<WalletResponse> Wallets { get; init; }
}

public sealed record WalletResponse
{
    public required string Id { get; init; }
    public required string Chain { get; init; }
    public required string PublicKey { get; init; }
    public required string Provider { get; init; }
}

public sealed class AuthenticateEndpoint : Endpoint<AuthenticateRequest, AuthenticateResponse>
{
    private readonly IJwtValidationService _jwtService;
    private readonly IDynamicUserService _userService;

    public AuthenticateEndpoint(
        IJwtValidationService jwtService,
        IDynamicUserService userService)
    {
        _jwtService = jwtService;
        _userService = userService;
    }

    public override void Configure()
    {
        Post("/api/auth/authenticate");
        AllowAnonymous();
        
        Summary(s =>
        {
            s.Summary = "Authenticate user with Dynamic.xyz JWT token";
            s.Description = "Validates the provided JWT token and returns user information";
            s.Response<AuthenticateResponse>(200, "User authenticated successfully");
            s.Response(401, "Invalid or expired token");
        });
    }

    public override async Task HandleAsync(AuthenticateRequest req, CancellationToken ct)
    {
        var tokenResult = await _jwtService.ValidateTokenAsync(req.AuthToken, ct);
        if (tokenResult.IsFailure)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var userId = DynamicUserId.From(tokenResult.Value.Sub);
        var userResult = await _userService.GetUserByIdAsync(userId, ct);
        
        if (userResult.IsFailure)
        {
            await SendErrorsAsync(400, ct);
            return;
        }

        var response = new AuthenticateResponse
        {
            UserId = userResult.Value.Id.ToString(),
            Email = userResult.Value.Email,
            Scopes = tokenResult.Value.Scopes,
            Wallets = userResult.Value.Wallets.Select(w => new WalletResponse
            {
                Id = w.Id.ToString(),
                Chain = w.Chain.ToString(),
                PublicKey = w.PublicKey,
                Provider = w.Provider.ToString()
            }).ToList()
        };

        await SendOkAsync(response, ct);
    }
}
```

## Configuration

### 1. appsettings.json

```json
{
  "DynamicApi": {
    "BaseUrl": "https://app.dynamic.xyz/api/v0",
    "ApiToken": "dyn_XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
    "EnvironmentId": "95b11417-f18f-457f-8804-68e361f9164f",
    "TimeoutSeconds": 30
  }
}
```

### 2. Program.cs Registration

```csharp
// Add Dynamic.xyz integration
builder.Services.AddDynamicApi(builder.Configuration);

// Add memory cache for JWT key caching
builder.Services.AddMemoryCache();

// Add HttpClient for JWT key retrieval
builder.Services.AddHttpClient();
```

## Error Handling

The integration follows Axon Backend's Result pattern for comprehensive error handling:

### 1. Error Types

```csharp
// Authentication errors
Error.Authentication("Invalid token")
Error.Authentication("Token expired")
Error.Authentication("Additional verification required")

// Authorization errors  
Error.Authorization("Access denied to resource")

// Validation errors
Error.Validation("Invalid response data")
Error.Validation("Bad request: {details}")

// External service errors
Error.External("HTTP request failed", details)
Error.External("API error (statusCode)", content)

// Not found errors
Error.NotFound("Resource not found")
```

### 2. Error Handling in Endpoints

```csharp
public override async Task HandleAsync(SomeRequest req, CancellationToken ct)
{
    var result = await _dynamicUserService.GetUserByIdAsync(userId, ct);
    
    if (result.IsFailure)
    {
        var error = result.Error;
        var statusCode = error.Type switch
        {
            ErrorType.Authentication => 401,
            ErrorType.Authorization => 403,
            ErrorType.NotFound => 404,
            ErrorType.Validation => 400,
            _ => 500
        };
        
        await SendErrorsAsync(statusCode, ct);
        return;
    }
    
    // Success handling...
}
```

## Best Practices

1. **Use Strong IDs**: Always use strongly-typed IDs for type safety
2. **Follow Result Pattern**: Return Result<T> from all operations that can fail
3. **Cache JWT Keys**: Cache JWKS keys to improve performance
4. **Implement Proper Logging**: Log all external API calls and errors
5. **Handle Rate Limits**: Implement retry logic with exponential backoff
6. **Validate All Inputs**: Always validate data received from external APIs
7. **Use CancellationTokens**: Support cancellation in all async operations
8. **Configure Timeouts**: Set appropriate timeouts for HTTP requests
9. **Secure Token Storage**: Never log or expose API tokens
10. **Monitor API Usage**: Track API calls for billing and rate limiting

## Testing

### 1. Unit Testing Example

```csharp
public class DynamicUserServiceTests
{
    [Test]
    public async Task GetUserByIdAsync_ValidUserId_ReturnsUser()
    {
        // Arrange
        var userId = DynamicUserId.New();
        var mockResponse = new DynamicUserResponse
        {
            User = new UserDto
            {
                Id = userId.ToString(),
                ProjectEnvironmentId = DynamicEnvironmentId.New().ToString(),
                Email = "test@example.com"
            }
        };
        
        var mockClient = new Mock<DynamicApiClient>();
        mockClient.Setup(x => x.GetAsync<DynamicUserResponse>(
            It.IsAny<string>(), 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<DynamicUserResponse>.Success(mockResponse));
            
        var service = new DynamicUserService(mockClient.Object, Options.Create(new DynamicApiClientOptions
        {
            EnvironmentId = DynamicEnvironmentId.New().ToString(),
            ApiToken = "test-token",
            BaseUrl = "https://test.api"
        }));
        
        // Act
        var result = await service.GetUserByIdAsync(userId);
        
        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("test@example.com");
    }
}
```

This integration provides a robust, type-safe way to interact with Dynamic.xyz APIs while maintaining Axon Backend's architectural patterns and principles.