# FRD S2 - Infrastructure Integration

**Stage**: S2 - Infrastructure Integration (Proof of Concept)  
**Layer**: Infrastructure Layer (`src/Modules/Identity/Infrastructure`)  
**Dependencies**: S1 (API Contracts)  
**Document Version**: 2.0  
**Date**: August 27, 2025  
**Author**: Engineering Team  
**Based on**: BRD v1.0, PRD v1.0, Dynamic.xyz API Reference, C# Integration Guide  

---

## 1. Overview

### 1.1 Responsibility

Implement direct external integrations with Dynamic.xyz APIs and services without application layer abstractions. This stage creates working proof-of-concept with real Dynamic.xyz integration, JWT validation, and webhook processing while maintaining clean separation from domain logic.

### 1.2 Stage Context

- **S1 (Complete)**: API contracts with stub responses
- **S2 (Current)**: Infrastructure integration with real Dynamic.xyz APIs
- **S3**: Abstract behind application ports and add CQRS handlers
- **S4**: Add domain model validation and business rules
- **S5**: Full persistence implementation with Entity Framework

### 1.3 Exit Criteria

- ✅ JWT tokens successfully validated against Dynamic.xyz JWKS endpoint
- ✅ User data retrieved from Dynamic.xyz Management API
- ✅ Webhook signatures validated cryptographically
- ✅ API endpoints return real Dynamic.xyz data (not stubs)
- ✅ Integration tests pass with Dynamic.xyz sandbox environment
- ✅ Configuration supports multiple environments (dev/staging/prod)
- ✅ Proper error handling and logging for all external calls

---

## 2. Architecture Integration

### 2.1 Infrastructure Layer Organization

Following Axon Backend's Clean Architecture principles, all external integrations reside in the Infrastructure layer:

```
src/Modules/Identity/Infrastructure/
├── External/
│   ├── DynamicXyz/
│   │   ├── Client/
│   │   │   ├── IDynamicApiClient.cs
│   │   │   ├── DynamicApiClient.cs
│   │   │   └── DynamicApiClientOptions.cs
│   │   ├── Services/
│   │   │   ├── IDynamicUserService.cs
│   │   │   ├── DynamicUserService.cs
│   │   │   ├── IDynamicWalletService.cs
│   │   │   └── DynamicWalletService.cs
│   │   ├── Authentication/
│   │   │   ├── IJwtValidationService.cs
│   │   │   ├── JwtValidationService.cs
│   │   │   ├── IJwksKeyProvider.cs
│   │   │   └── JwksKeyProvider.cs
│   │   ├── Webhooks/
│   │   │   ├── IWebhookSignatureValidator.cs
│   │   │   ├── WebhookSignatureValidator.cs
│   │   │   ├── IWebhookProcessor.cs
│   │   │   └── WebhookProcessor.cs
│   │   ├── Models/
│   │   │   ├── DynamicUser.cs
│   │   │   ├── DynamicWallet.cs
│   │   │   ├── JwtPayload.cs
│   │   │   └── WebhookEvent.cs
│   │   └── Extensions/
│   │       └── ServiceCollectionExtensions.cs
├── Configuration/
│   ├── DynamicXyzConfiguration.cs
│   └── IdentityInfrastructureModule.cs
└── Persistence/ (S5 - future stage)
```

### 2.2 Integration Points

**S2 Stage Integration Points**:
1. **API Layer** → **Infrastructure Layer**: Direct service injection
2. **Infrastructure Layer** → **Dynamic.xyz APIs**: HTTP client integration
3. **Infrastructure Layer** → **JWKS Provider**: Key validation
4. **Infrastructure Layer** → **Configuration**: Environment settings

---

## 3. Dynamic.xyz API Client Infrastructure

### 3.1 HTTP Client Configuration

#### 3.1.1 Client Options

```csharp
// src/Modules/Identity/Infrastructure/External/DynamicXyz/Client/DynamicApiClientOptions.cs

namespace Axon.Modules.Identity.Infrastructure.External.DynamicXyz.Client;

/// <summary>
/// Configuration options for Dynamic.xyz API client
/// </summary>
public sealed class DynamicApiClientOptions
{
    public const string SectionName = "DynamicXyz";
    
    /// <summary>
    /// Base URL for Dynamic.xyz API (environment-specific)
    /// Production: https://app.dynamic.xyz/api/v0
    /// Alternative: https://app.dynamicauth.com/api/v0
    /// Development: http://localhost:3333/api/v0
    /// </summary>
    public required string BaseUrl { get; init; } = "https://app.dynamic.xyz/api/v0";
    
    /// <summary>
    /// API token for authentication (starts with 'dyn_')
    /// </summary>
    public required string ApiToken { get; init; }
    
    /// <summary>
    /// Environment ID for this Dynamic.xyz environment
    /// </summary>
    public required string EnvironmentId { get; init; }
    
    /// <summary>
    /// HTTP request timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; init; } = 30;
    
    /// <summary>
    /// Maximum number of retry attempts for failed requests
    /// </summary>
    public int MaxRetryAttempts { get; init; } = 3;
    
    /// <summary>
    /// Base delay for exponential backoff retry policy (seconds)
    /// </summary>
    public double RetryBaseDelaySeconds { get; init; } = 2.0;
    
    /// <summary>
    /// Enable detailed request/response logging (development only)
    /// </summary>
    public bool EnableDetailedLogging { get; init; } = false;
}
```

#### 3.1.2 API Client Interface

```csharp
// src/Modules/Identity/Infrastructure/External/DynamicXyz/Client/IDynamicApiClient.cs

namespace Axon.Modules.Identity.Infrastructure.External.DynamicXyz.Client;

/// <summary>
/// HTTP client for Dynamic.xyz Management API
/// </summary>
public interface IDynamicApiClient
{
    /// <summary>
    /// Execute GET request to Dynamic.xyz API
    /// </summary>
    /// <typeparam name="TResponse">Response type</typeparam>
    /// <param name="endpoint">API endpoint (relative to base URL)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Response data or error</returns>
    Task<Result<TResponse>> GetAsync<TResponse>(
        string endpoint,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Execute POST request to Dynamic.xyz API
    /// </summary>
    /// <typeparam name="TRequest">Request type</typeparam>
    /// <typeparam name="TResponse">Response type</typeparam>
    /// <param name="endpoint">API endpoint (relative to base URL)</param>
    /// <param name="request">Request payload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Response data or error</returns>
    Task<Result<TResponse>> PostAsync<TRequest, TResponse>(
        string endpoint,
        TRequest request,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check API health and connectivity
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success if API is reachable</returns>
    Task<Result<bool>> HealthCheckAsync(CancellationToken cancellationToken = default);
}
```

#### 3.1.3 API Client Implementation

```csharp
// src/Modules/Identity/Infrastructure/External/DynamicXyz/Client/DynamicApiClient.cs

namespace Axon.Modules.Identity.Infrastructure.External.DynamicXyz.Client;

/// <summary>
/// HTTP client implementation for Dynamic.xyz Management API
/// </summary>
internal sealed class DynamicApiClient : IDynamicApiClient
{
    private readonly HttpClient _httpClient;
    private readonly DynamicApiClientOptions _options;
    private readonly ILogger<DynamicApiClient> _logger;

    public DynamicApiClient(
        HttpClient httpClient,
        IOptions<DynamicApiClientOptions> options,
        ILogger<DynamicApiClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ConfigureHttpClient();
    }

    public async Task<Result<TResponse>> GetAsync<TResponse>(
        string endpoint,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync<TResponse>(
            async () => await _httpClient.GetAsync(endpoint, cancellationToken),
            endpoint,
            cancellationToken);
    }

    public async Task<Result<TResponse>> PostAsync<TRequest, TResponse>(
        string endpoint,
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync<TResponse>(
            async () =>
            {
                var json = JsonSerializer.Serialize(request, JsonSerializerOptions.Web);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                return await _httpClient.PostAsync(endpoint, content, cancellationToken);
            },
            endpoint,
            cancellationToken);
    }

    public async Task<Result<bool>> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10)); // Quick health check timeout

            var response = await _httpClient.GetAsync($"environments/{_options.EnvironmentId}", cts.Token);
            return response.IsSuccessStatusCode
                ? Result<bool>.Success(true)
                : Result<bool>.Failure(Error.External($"Health check failed with status {response.StatusCode}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dynamic.xyz API health check failed");
            return Result<bool>.Failure(Error.External("API health check failed", ex.Message));
        }
    }

    private void ConfigureHttpClient()
    {
        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", _options.ApiToken);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("Axon-Backend", "1.0"));
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    private async Task<Result<TResponse>> ExecuteWithRetryAsync<TResponse>(
        Func<Task<HttpResponseMessage>> requestFactory,
        string endpoint,
        CancellationToken cancellationToken)
    {
        var retryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .OrResult<HttpResponseMessage>(r => (int)r.StatusCode >= 500)
            .WaitAndRetryAsync(
                _options.MaxRetryAttempts,
                retryAttempt => TimeSpan.FromSeconds(_options.RetryBaseDelaySeconds * Math.Pow(2, retryAttempt - 1)),
                (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("Dynamic.xyz API request to {Endpoint} failed, retry {RetryCount}/{MaxRetries} in {Delay}s",
                        endpoint, retryCount, _options.MaxRetryAttempts, timespan.TotalSeconds);
                });

        try
        {
            var response = await retryPolicy.ExecuteAsync(requestFactory);

            if (!response.IsSuccessStatusCode)
            {
                return Result<TResponse>.Failure(await CreateErrorFromResponse(response));
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (_options.EnableDetailedLogging)
            {
                _logger.LogDebug("Dynamic.xyz API response from {Endpoint}: {Response}", endpoint, json);
            }

            var data = JsonSerializer.Deserialize<TResponse>(json, JsonSerializerOptions.Web);

            return data is not null
                ? Result<TResponse>.Success(data)
                : Result<TResponse>.Failure(Error.Validation("Invalid response data from Dynamic.xyz API"));
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON deserialization failed for Dynamic.xyz API endpoint: {Endpoint}", endpoint);
            return Result<TResponse>.Failure(Error.Validation("Invalid JSON response from Dynamic.xyz API"));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed for Dynamic.xyz API endpoint: {Endpoint}", endpoint);
            return Result<TResponse>.Failure(Error.External("HTTP request failed", ex.Message));
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout for Dynamic.xyz API endpoint: {Endpoint}", endpoint);
            return Result<TResponse>.Failure(Error.External("Request timeout", ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error for Dynamic.xyz API endpoint: {Endpoint}", endpoint);
            return Result<TResponse>.Failure(Error.External("Unexpected API error", ex.Message));
        }
    }

    private static async Task<Error> CreateErrorFromResponse(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => Error.Authentication("Invalid API token"),
            HttpStatusCode.Forbidden => Error.Authorization("Access denied to Dynamic.xyz resource"),
            HttpStatusCode.NotFound => Error.NotFound("Resource not found in Dynamic.xyz"),
            HttpStatusCode.BadRequest => Error.Validation($"Bad request to Dynamic.xyz: {content}"),
            HttpStatusCode.TooManyRequests => Error.External("Rate limit exceeded", content),
            _ => Error.External($"Dynamic.xyz API error ({(int)response.StatusCode})", content)
        };
    }
}
```

---

## 4. User and Wallet Services

### 4.1 Dynamic User Service

#### 4.1.1 Service Interface

```csharp
// src/Modules/Identity/Infrastructure/External/DynamicXyz/Services/IDynamicUserService.cs

namespace Axon.Modules.Identity.Infrastructure.External.DynamicXyz.Services;

/// <summary>
/// Service for retrieving user data from Dynamic.xyz
/// </summary>
public interface IDynamicUserService
{
    /// <summary>
    /// Retrieve user by Dynamic.xyz user ID
    /// </summary>
    /// <param name="dynamicUserId">Dynamic.xyz user identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>User data or error</returns>
    Task<Result<DynamicUser>> GetUserByIdAsync(
        Guid dynamicUserId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieve user wallets
    /// </summary>
    /// <param name="dynamicUserId">Dynamic.xyz user identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of user wallets or error</returns>
    Task<Result<IReadOnlyList<DynamicWallet>>> GetUserWalletsAsync(
        Guid dynamicUserId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if user exists in Dynamic.xyz
    /// </summary>
    /// <param name="dynamicUserId">Dynamic.xyz user identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if user exists</returns>
    Task<Result<bool>> UserExistsAsync(
        Guid dynamicUserId,
        CancellationToken cancellationToken = default);
}
```

#### 4.1.2 Service Implementation

```csharp
// src/Modules/Identity/Infrastructure/External/DynamicXyz/Services/DynamicUserService.cs

namespace Axon.Modules.Identity.Infrastructure.External.DynamicXyz.Services;

/// <summary>
/// Implementation of Dynamic.xyz user service
/// </summary>
internal sealed class DynamicUserService : IDynamicUserService
{
    private readonly IDynamicApiClient _apiClient;
    private readonly DynamicApiClientOptions _options;
    private readonly ILogger<DynamicUserService> _logger;

    public DynamicUserService(
        IDynamicApiClient apiClient,
        IOptions<DynamicApiClientOptions> options,
        ILogger<DynamicUserService> logger)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<DynamicUser>> GetUserByIdAsync(
        Guid dynamicUserId,
        CancellationToken cancellationToken = default)
    {
        var endpoint = $"environments/{_options.EnvironmentId}/users/{dynamicUserId}";
        
        _logger.LogDebug("Retrieving user {UserId} from Dynamic.xyz", dynamicUserId);

        var response = await _apiClient.GetAsync<DynamicUserApiResponse>(endpoint, cancellationToken);

        return response.IsSuccess
            ? Result<DynamicUser>.Success(MapApiResponseToDomain(response.Value))
            : Result<DynamicUser>.Failure(response.Error);
    }

    public async Task<Result<IReadOnlyList<DynamicWallet>>> GetUserWalletsAsync(
        Guid dynamicUserId,
        CancellationToken cancellationToken = default)
    {
        var endpoint = $"environments/{_options.EnvironmentId}/users/{dynamicUserId}/wallets";
        
        _logger.LogDebug("Retrieving wallets for user {UserId} from Dynamic.xyz", dynamicUserId);

        var response = await _apiClient.GetAsync<DynamicWalletsApiResponse>(endpoint, cancellationToken);

        return response.IsSuccess
            ? Result<IReadOnlyList<DynamicWallet>>.Success(
                response.Value.Wallets.Select(MapWalletApiResponseToDomain).ToList())
            : Result<IReadOnlyList<DynamicWallet>>.Failure(response.Error);
    }

    public async Task<Result<bool>> UserExistsAsync(
        Guid dynamicUserId,
        CancellationToken cancellationToken = default)
    {
        var userResult = await GetUserByIdAsync(dynamicUserId, cancellationToken);
        
        return userResult.IsSuccess
            ? Result<bool>.Success(true)
            : userResult.Error.Type == ErrorType.NotFound
                ? Result<bool>.Success(false)
                : Result<bool>.Failure(userResult.Error);
    }

    private static DynamicUser MapApiResponseToDomain(DynamicUserApiResponse response)
    {
        return new DynamicUser
        {
            Id = Guid.Parse(response.User.Id),
            ProjectEnvironmentId = Guid.Parse(response.User.ProjectEnvironmentId),
            Email = response.User.Email ?? "",
            FirstName = response.User.FirstName,
            LastName = response.User.LastName,
            Username = response.User.Username,
            PhoneNumber = response.User.PhoneNumber,
            Country = response.User.Country,
            NewUser = response.User.NewUser,
            FirstVisit = response.User.FirstVisit,
            LastVisit = response.User.LastVisit,
            CreatedAt = response.User.CreatedAt,
            UpdatedAt = response.User.UpdatedAt,
            Wallets = response.User.Wallets?.Select(MapWalletApiResponseToDomain).ToList() ?? [],
            Lists = response.User.Lists ?? [],
            Scope = response.User.Scope ?? "",
            Metadata = response.User.Metadata ?? new Dictionary<string, object>()
        };
    }

    private static DynamicWallet MapWalletApiResponseToDomain(WalletApiResponse wallet)
    {
        return new DynamicWallet
        {
            Id = Guid.Parse(wallet.Id),
            Name = wallet.Name,
            Chain = ParseBlockchainType(wallet.Chain),
            PublicKey = wallet.PublicKey,
            Provider = ParseWalletProvider(wallet.Provider),
            Properties = wallet.Properties != null ? MapWalletProperties(wallet.Properties) : null,
            LastSelectedAt = !string.IsNullOrEmpty(wallet.LastSelectedAt) 
                ? DateTime.Parse(wallet.LastSelectedAt) 
                : null
        };
    }

    private static BlockchainType ParseBlockchainType(string chain) => chain.ToUpperInvariant() switch
    {
        "ETH" or "EVM" => BlockchainType.ETH,
        "SOL" => BlockchainType.SOL,
        "BTC" => BlockchainType.BTC,
        "ALGO" => BlockchainType.ALGO,
        "FLOW" => BlockchainType.FLOW,
        "STARK" => BlockchainType.STARK,
        "COSMOS" => BlockchainType.COSMOS,
        "SUI" => BlockchainType.SUI,
        "ECLIPSE" => BlockchainType.ECLIPSE,
        _ => BlockchainType.ETH // Default to ETH for unknown types
    };

    private static WalletProvider ParseWalletProvider(string provider) => provider.ToLowerInvariant() switch
    {
        "browserextension" => WalletProvider.BrowserExtension,
        "walletconnect" => WalletProvider.WalletConnect,
        "email" => WalletProvider.Email,
        "social" => WalletProvider.Social,
        "embedded" => WalletProvider.Embedded,
        "passkey" => WalletProvider.Passkey,
        _ => WalletProvider.BrowserExtension // Default fallback
    };

    private static WalletProperties MapWalletProperties(WalletPropertiesApiResponse props)
    {
        return new WalletProperties
        {
            TurnkeySubOrganizationId = props.TurnkeySubOrganizationId,
            TurnkeyPrivateKeyId = props.TurnkeyPrivateKeyId,
            TurnkeyHDWalletId = props.TurnkeyHDWalletId,
            IsAuthenticatorAttached = props.IsAuthenticatorAttached,
            TurnkeyUserId = props.TurnkeyUserId,
            IsSessionKeyCompatible = props.IsSessionKeyCompatible,
            Version = props.Version,
            EcdsaProviderType = props.EcdsaProviderType,
            EntryPointVersion = props.EntryPointVersion,
            KernelVersion = props.KernelVersion
        };
    }
}
```

### 4.2 API Response Models

```csharp
// src/Modules/Identity/Infrastructure/External/DynamicXyz/Models/ApiResponses.cs

namespace Axon.Modules.Identity.Infrastructure.External.DynamicXyz.Models;

/// <summary>
/// Dynamic.xyz user API response model
/// </summary>
internal sealed record DynamicUserApiResponse
{
    [JsonPropertyName("user")]
    public required UserApiResponse User { get; init; }
}

/// <summary>
/// User data from Dynamic.xyz API
/// </summary>
internal sealed record UserApiResponse
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
    public IReadOnlyList<WalletApiResponse>? Wallets { get; init; }

    [JsonPropertyName("lists")]
    public IReadOnlyList<string>? Lists { get; init; }

    [JsonPropertyName("scope")]
    public string? Scope { get; init; }

    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Wallets response from Dynamic.xyz API
/// </summary>
internal sealed record DynamicWalletsApiResponse
{
    [JsonPropertyName("count")]
    public int Count { get; init; }

    [JsonPropertyName("wallets")]
    public required IReadOnlyList<WalletApiResponse> Wallets { get; init; }
}

/// <summary>
/// Wallet data from Dynamic.xyz API
/// </summary>
internal sealed record WalletApiResponse
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
    public WalletPropertiesApiResponse? Properties { get; init; }

    [JsonPropertyName("lastSelectedAt")]
    public string? LastSelectedAt { get; init; }
}

/// <summary>
/// Wallet properties from Dynamic.xyz API
/// </summary>
internal sealed record WalletPropertiesApiResponse
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
```

---

## 5. JWT Validation Infrastructure

### 5.1 JWKS Key Provider

#### 5.1.1 JWKS Provider Interface

```csharp
// src/Modules/Identity/Infrastructure/External/DynamicXyz/Authentication/IJwksKeyProvider.cs

namespace Axon.Modules.Identity.Infrastructure.External.DynamicXyz.Authentication;

/// <summary>
/// Provider for retrieving and caching Dynamic.xyz JWKS keys
/// </summary>
public interface IJwksKeyProvider
{
    /// <summary>
    /// Retrieve signing keys from Dynamic.xyz JWKS endpoint
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of signing keys or error</returns>
    Task<Result<IList<SecurityKey>>> GetSigningKeysAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Force refresh of cached keys from JWKS endpoint
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success result or error</returns>
    Task<Result<Unit>> RefreshKeysAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get cache status and key information
    /// </summary>
    /// <returns>Key cache status</returns>
    Task<Result<JwksKeyStatus>> GetKeyStatusAsync();
}

/// <summary>
/// JWKS key cache status information
/// </summary>
public sealed record JwksKeyStatus
{
    public required int KeyCount { get; init; }
    public required DateTime LastRefresh { get; init; }
    public required DateTime NextRefresh { get; init; }
    public required bool IsHealthy { get; init; }
    public required TimeSpan CacheAge { get; init; }
}
```

#### 5.1.2 JWKS Provider Implementation

```csharp
// src/Modules/Identity/Infrastructure/External/DynamicXyz/Authentication/JwksKeyProvider.cs

namespace Axon.Modules.Identity.Infrastructure.External.DynamicXyz.Authentication;

/// <summary>
/// Implementation of Dynamic.xyz JWKS key provider with caching
/// </summary>
internal sealed class JwksKeyProvider : IJwksKeyProvider
{
    private readonly HttpClient _httpClient;
    private readonly DynamicApiClientOptions _options;
    private readonly IMemoryCache _cache;
    private readonly ILogger<JwksKeyProvider> _logger;
    
    private const string CacheKey = "dynamic_jwks_keys";
    private const string CacheStatusKey = "dynamic_jwks_status";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan HealthCheckInterval = TimeSpan.FromMinutes(1);

    public JwksKeyProvider(
        HttpClient httpClient,
        IOptions<DynamicApiClientOptions> options,
        IMemoryCache cache,
        ILogger<JwksKeyProvider> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<IList<SecurityKey>>> GetSigningKeysAsync(CancellationToken cancellationToken = default)
    {
        // Try to get from cache first
        if (_cache.TryGetValue(CacheKey, out IList<SecurityKey>? cachedKeys) && cachedKeys is not null)
        {
            _logger.LogDebug("Retrieved {KeyCount} JWKS keys from cache", cachedKeys.Count);
            return Result<IList<SecurityKey>>.Success(cachedKeys);
        }

        // Cache miss - fetch from Dynamic.xyz
        return await RefreshAndGetKeysAsync(cancellationToken);
    }

    public async Task<Result<Unit>> RefreshKeysAsync(CancellationToken cancellationToken = default)
    {
        var result = await RefreshAndGetKeysAsync(cancellationToken);
        return result.IsSuccess 
            ? Result<Unit>.Success(Unit.Value)
            : Result<Unit>.Failure(result.Error);
    }

    public async Task<Result<JwksKeyStatus>> GetKeyStatusAsync()
    {
        var cachedStatus = _cache.Get<JwksKeyStatus>(CacheStatusKey);
        var keyCount = _cache.TryGetValue(CacheKey, out IList<SecurityKey>? keys) ? keys?.Count ?? 0 : 0;

        var status = new JwksKeyStatus
        {
            KeyCount = keyCount,
            LastRefresh = cachedStatus?.LastRefresh ?? DateTime.MinValue,
            NextRefresh = cachedStatus?.LastRefresh.Add(CacheDuration) ?? DateTime.UtcNow,
            IsHealthy = keyCount > 0 && (cachedStatus?.LastRefresh ?? DateTime.MinValue) > DateTime.UtcNow.Subtract(CacheDuration.Add(TimeSpan.FromMinutes(5))),
            CacheAge = cachedStatus?.LastRefresh != null ? DateTime.UtcNow.Subtract(cachedStatus.LastRefresh) : TimeSpan.MaxValue
        };

        return Result<JwksKeyStatus>.Success(status);
    }

    private async Task<Result<IList<SecurityKey>>> RefreshAndGetKeysAsync(CancellationToken cancellationToken)
    {
        try
        {
            var jwksUri = $"https://app.dynamic.xyz/api/v0/sdk/{_options.EnvironmentId}/.well-known/jwks";
            
            _logger.LogDebug("Fetching JWKS keys from {JwksUri}", jwksUri);

            var configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                jwksUri,
                new OpenIdConnectConfigurationRetriever(),
                _httpClient);

            var config = await configManager.GetConfigurationAsync(cancellationToken);
            var keys = config.SigningKeys.ToList();

            if (keys.Count == 0)
            {
                _logger.LogWarning("No signing keys found in Dynamic.xyz JWKS response");
                return Result<IList<SecurityKey>>.Failure(Error.External("No JWKS signing keys available"));
            }

            // Cache the keys and status
            _cache.Set(CacheKey, keys, CacheDuration);
            _cache.Set(CacheStatusKey, new JwksKeyStatus
            {
                KeyCount = keys.Count,
                LastRefresh = DateTime.UtcNow,
                NextRefresh = DateTime.UtcNow.Add(CacheDuration),
                IsHealthy = true,
                CacheAge = TimeSpan.Zero
            }, CacheDuration);

            _logger.LogInformation("Successfully cached {KeyCount} JWKS keys from Dynamic.xyz", keys.Count);
            
            return Result<IList<SecurityKey>>.Success(keys);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve JWKS keys from Dynamic.xyz");
            return Result<IList<SecurityKey>>.Failure(Error.External("JWKS key retrieval failed", ex.Message));
        }
    }
}
```

### 5.2 JWT Validation Service

#### 5.2.1 Validation Service Interface

```csharp
// src/Modules/Identity/Infrastructure/External/DynamicXyz/Authentication/IJwtValidationService.cs

namespace Axon.Modules.Identity.Infrastructure.External.DynamicXyz.Authentication;

/// <summary>
/// Service for validating Dynamic.xyz JWT tokens
/// </summary>
public interface IJwtValidationService
{
    /// <summary>
    /// Validate Dynamic.xyz JWT token and extract payload
    /// </summary>
    /// <param name="token">JWT token to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>JWT payload or validation error</returns>
    Task<Result<DynamicJwtPayload>> ValidateTokenAsync(
        string token,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validate token format without cryptographic verification
    /// </summary>
    /// <param name="token">JWT token to check</param>
    /// <returns>True if token has valid JWT format</returns>
    Result<bool> IsValidJwtFormat(string token);
    
    /// <summary>
    /// Extract claims from token without validation (for debugging)
    /// </summary>
    /// <param name="token">JWT token</param>
    /// <returns>Raw claims or error</returns>
    Result<IReadOnlyDictionary<string, object>> ExtractClaimsWithoutValidation(string token);
}
```

#### 5.2.2 Validation Service Implementation

```csharp
// src/Modules/Identity/Infrastructure/External/DynamicXyz/Authentication/JwtValidationService.cs

namespace Axon.Modules.Identity.Infrastructure.External.DynamicXyz.Authentication;

/// <summary>
/// Implementation of Dynamic.xyz JWT validation service
/// </summary>
internal sealed class JwtValidationService : IJwtValidationService
{
    private readonly IJwksKeyProvider _keyProvider;
    private readonly DynamicApiClientOptions _options;
    private readonly ILogger<JwtValidationService> _logger;

    public JwtValidationService(
        IJwksKeyProvider keyProvider,
        IOptions<DynamicApiClientOptions> options,
        ILogger<JwtValidationService> logger)
    {
        _keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<DynamicJwtPayload>> ValidateTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Result<DynamicJwtPayload>.Failure(Error.Validation("JWT token is required"));
        }

        // Basic format validation
        var formatResult = IsValidJwtFormat(token);
        if (formatResult.IsFailure)
        {
            return Result<DynamicJwtPayload>.Failure(formatResult.Error);
        }

        try
        {
            // Get signing keys
            var keysResult = await _keyProvider.GetSigningKeysAsync(cancellationToken);
            if (keysResult.IsFailure)
            {
                return Result<DynamicJwtPayload>.Failure(keysResult.Error);
            }

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = keysResult.Value,
                ValidateIssuer = true,
                ValidIssuer = $"https://app.dynamic.xyz/api/v0/sdk/{_options.EnvironmentId}",
                ValidateAudience = false, // Dynamic.xyz doesn't use audience validation
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(5), // Allow 5-minute clock skew
                RequireExpirationTime = true,
                RequireSignedTokens = true
            };

            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, validationParameters, out var validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;
            var payload = MapToPayload(jwtToken);

            // Check for additional verification requirements
            if (payload.Scopes.Contains("requiresAdditionalAuth"))
            {
                _logger.LogWarning("JWT token requires additional authentication: {Subject}", payload.Subject);
                return Result<DynamicJwtPayload>.Failure(
                    Error.Authentication("Token requires additional verification"));
            }

            _logger.LogDebug("Successfully validated JWT token for user {Subject}", payload.Subject);
            return Result<DynamicJwtPayload>.Success(payload);
        }
        catch (SecurityTokenExpiredException ex)
        {
            _logger.LogWarning(ex, "JWT token has expired");
            return Result<DynamicJwtPayload>.Failure(Error.Authentication("JWT token has expired"));
        }
        catch (SecurityTokenInvalidSignatureException ex)
        {
            _logger.LogWarning(ex, "JWT token has invalid signature");
            return Result<DynamicJwtPayload>.Failure(Error.Authentication("Invalid JWT token signature"));
        }
        catch (SecurityTokenValidationException ex)
        {
            _logger.LogWarning(ex, "JWT token validation failed: {Message}", ex.Message);
            return Result<DynamicJwtPayload>.Failure(Error.Authentication($"JWT validation failed: {ex.Message}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during JWT validation");
            return Result<DynamicJwtPayload>.Failure(Error.External("JWT validation error", ex.Message));
        }
    }

    public Result<bool> IsValidJwtFormat(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Result<bool>.Failure(Error.Validation("JWT token is required"));
        }

        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return Result<bool>.Failure(Error.Validation("JWT token must have exactly 3 parts separated by dots"));
        }

        // Validate each part is valid Base64
        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part))
            {
                return Result<bool>.Failure(Error.Validation("JWT token parts cannot be empty"));
            }

            try
            {
                // Add padding if needed for Base64 decoding
                var padded = part.PadRight((part.Length + 3) & ~3, '=');
                Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/'));
            }
            catch (FormatException)
            {
                return Result<bool>.Failure(Error.Validation("JWT token contains invalid Base64 encoding"));
            }
        }

        return Result<bool>.Success(true);
    }

    public Result<IReadOnlyDictionary<string, object>> ExtractClaimsWithoutValidation(string token)
    {
        var formatResult = IsValidJwtFormat(token);
        if (formatResult.IsFailure)
        {
            return Result<IReadOnlyDictionary<string, object>>.Failure(formatResult.Error);
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            
            var claims = jwtToken.Claims.ToDictionary(c => c.Type, c => (object)c.Value);
            return Result<IReadOnlyDictionary<string, object>>.Success(claims);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract JWT claims");
            return Result<IReadOnlyDictionary<string, object>>.Failure(
                Error.Validation("Failed to parse JWT token"));
        }
    }

    private static DynamicJwtPayload MapToPayload(JwtSecurityToken jwt)
    {
        var scopes = jwt.Claims.FirstOrDefault(c => c.Type == "scopes")?.Value ?? "";
        var lists = jwt.Claims.FirstOrDefault(c => c.Type == "lists")?.Value ?? "";
        var environmentId = jwt.Claims.FirstOrDefault(c => c.Type == "environment_id")?.Value ?? "";

        return new DynamicJwtPayload
        {
            Subject = jwt.Subject,
            Issuer = jwt.Issuer,
            Audience = jwt.Audiences.FirstOrDefault() ?? "",
            ExpiresAt = jwt.ValidTo,
            IssuedAt = jwt.ValidFrom,
            Scopes = scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList(),
            EnvironmentId = environmentId,
            Lists = lists.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList(),
            Claims = jwt.Claims.ToDictionary(c => c.Type, c => (object)c.Value)
        };
    }
}
```

---

## 6. Webhook Infrastructure

### 6.1 Webhook Signature Validator

#### 6.1.1 Validator Interface

```csharp
// src/Modules/Identity/Infrastructure/External/DynamicXyz/Webhooks/IWebhookSignatureValidator.cs

namespace Axon.Modules.Identity.Infrastructure.External.DynamicXyz.Webhooks;

/// <summary>
/// Service for validating Dynamic.xyz webhook signatures
/// </summary>
public interface IWebhookSignatureValidator
{
    /// <summary>
    /// Validate webhook signature using HMAC-SHA256
    /// </summary>
    /// <param name="payload">Raw webhook payload</param>
    /// <param name="signature">Signature from X-Dynamic-Signature header</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if signature is valid</returns>
    Task<Result<bool>> ValidateSignatureAsync(
        string payload,
        string signature,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validate webhook timestamp to prevent replay attacks
    /// </summary>
    /// <param name="timestamp">Timestamp from webhook headers</param>
    /// <param name="maxAgeMinutes">Maximum allowed age in minutes</param>
    /// <returns>True if timestamp is within allowed range</returns>
    Result<bool> ValidateTimestamp(string timestamp, int maxAgeMinutes = 5);
}
```

#### 6.1.2 Validator Implementation

```csharp
// src/Modules/Identity/Infrastructure/External/DynamicXyz/Webhooks/WebhookSignatureValidator.cs

namespace Axon.Modules.Identity.Infrastructure.External.DynamicXyz.Webhooks;

/// <summary>
/// Implementation of Dynamic.xyz webhook signature validation
/// </summary>
internal sealed class WebhookSignatureValidator : IWebhookSignatureValidator
{
    private readonly DynamicApiClientOptions _options;
    private readonly ILogger<WebhookSignatureValidator> _logger;

    public WebhookSignatureValidator(
        IOptions<DynamicApiClientOptions> options,
        ILogger<WebhookSignatureValidator> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<bool>> ValidateSignatureAsync(
        string payload,
        string signature,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return Result<bool>.Failure(Error.Validation("Webhook payload is required"));
        }

        if (string.IsNullOrWhiteSpace(signature))
        {
            return Result<bool>.Failure(Error.Validation("Webhook signature is required"));
        }

        try
        {
            // Dynamic.xyz uses HMAC-SHA256 with the API token as the secret
            var secret = Encoding.UTF8.GetBytes(_options.ApiToken);
            var payloadBytes = Encoding.UTF8.GetBytes(payload);

            using var hmac = new HMACSHA256(secret);
            var computedHash = hmac.ComputeHash(payloadBytes);
            var computedSignature = Convert.ToHexString(computedHash).ToLowerInvariant();

            // Remove any prefixes from the signature (e.g., "sha256=")
            var cleanSignature = signature.StartsWith("sha256=") 
                ? signature[7..] 
                : signature;

            var isValid = string.Equals(computedSignature, cleanSignature, StringComparison.OrdinalIgnoreCase);

            if (!isValid)
            {
                _logger.LogWarning("Webhook signature validation failed for payload length {Length}", payload.Length);
            }

            return Result<bool>.Success(isValid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating webhook signature");
            return Result<bool>.Failure(Error.External("Webhook signature validation error", ex.Message));
        }
    }

    public Result<bool> ValidateTimestamp(string timestamp, int maxAgeMinutes = 5)
    {
        if (string.IsNullOrWhiteSpace(timestamp))
        {
            return Result<bool>.Failure(Error.Validation("Webhook timestamp is required"));
        }

        try
        {
            if (!long.TryParse(timestamp, out var unixTimestamp))
            {
                return Result<bool>.Failure(Error.Validation("Invalid timestamp format"));
            }

            var webhookTime = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp);
            var currentTime = DateTimeOffset.UtcNow;
            var age = currentTime - webhookTime;

            var isValid = age.TotalMinutes <= maxAgeMinutes && age.TotalMinutes >= -1; // Allow 1 minute clock skew

            if (!isValid)
            {
                _logger.LogWarning("Webhook timestamp is too old: {Age} minutes", age.TotalMinutes);
            }

            return Result<bool>.Success(isValid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating webhook timestamp");
            return Result<bool>.Failure(Error.Validation("Invalid timestamp format"));
        }
    }
}
```

### 6.2 Webhook Processor

#### 6.2.1 Processor Interface

```csharp
// src/Modules/Identity/Infrastructure/External/DynamicXyz/Webhooks/IWebhookProcessor.cs

namespace Axon.Modules.Identity.Infrastructure.External.DynamicXyz.Webhooks;

/// <summary>
/// Service for processing Dynamic.xyz webhook events
/// </summary>
public interface IWebhookProcessor
{
    /// <summary>
    /// Process incoming webhook event
    /// </summary>
    /// <param name="webhookEvent">Webhook event data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processing result</returns>
    Task<Result<WebhookProcessingResult>> ProcessEventAsync(
        DynamicWebhookEvent webhookEvent,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if event type is supported
    /// </summary>
    /// <param name="eventName">Event type name</param>
    /// <returns>True if event type is supported</returns>
    Result<bool> IsEventTypeSupported(string eventName);
    
    /// <summary>
    /// Get supported event types
    /// </summary>
    /// <returns>List of supported event type names</returns>
    Result<IReadOnlyList<string>> GetSupportedEventTypes();
}

/// <summary>
/// Result of webhook event processing
/// </summary>
public sealed record WebhookProcessingResult
{
    public required string EventId { get; init; }
    public required string EventType { get; init; }
    public required bool Processed { get; init; }
    public required bool SyncScheduled { get; init; }
    public required DateTime ProcessedAt { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}
```

#### 6.2.2 Processor Implementation

```csharp
// src/Modules/Identity/Infrastructure/External/DynamicXyz/Webhooks/WebhookProcessor.cs

namespace Axon.Modules.Identity.Infrastructure.External.DynamicXyz.Webhooks;

/// <summary>
/// Implementation of Dynamic.xyz webhook event processor
/// </summary>
internal sealed class WebhookProcessor : IWebhookProcessor
{
    private readonly ILogger<WebhookProcessor> _logger;
    
    private static readonly HashSet<string> SupportedEventTypes = new()
    {
        "users.created", "users.updated", "users.deleted",
        "wallets.linked", "wallets.unlinked", "wallets.updated",
        "sessions.created", "sessions.deleted",
        "environments.updated"
    };

    public WebhookProcessor(ILogger<WebhookProcessor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<WebhookProcessingResult>> ProcessEventAsync(
        DynamicWebhookEvent webhookEvent,
        CancellationToken cancellationToken = default)
    {
        if (webhookEvent is null)
        {
            return Result<WebhookProcessingResult>.Failure(Error.Validation("Webhook event is required"));
        }

        _logger.LogInformation("Processing webhook event {EventId} of type {EventType}", 
            webhookEvent.EventId, webhookEvent.EventName);

        try
        {
            // Check if event type is supported
            var supportedResult = IsEventTypeSupported(webhookEvent.EventName);
            if (supportedResult.IsFailure)
            {
                return Result<WebhookProcessingResult>.Failure(supportedResult.Error);
            }

            if (!supportedResult.Value)
            {
                _logger.LogWarning("Unsupported webhook event type: {EventType}", webhookEvent.EventName);
                
                return Result<WebhookProcessingResult>.Success(new WebhookProcessingResult
                {
                    EventId = webhookEvent.EventId,
                    EventType = webhookEvent.EventName,
                    Processed = false,
                    SyncScheduled = false,
                    ProcessedAt = DateTime.UtcNow,
                    ErrorMessage = $"Event type '{webhookEvent.EventName}' is not supported"
                });
            }

            // Process based on event type
            var result = webhookEvent.EventName switch
            {
                var e when e.StartsWith("users.") => await ProcessUserEventAsync(webhookEvent, cancellationToken),
                var e when e.StartsWith("wallets.") => await ProcessWalletEventAsync(webhookEvent, cancellationToken),
                var e when e.StartsWith("sessions.") => await ProcessSessionEventAsync(webhookEvent, cancellationToken),
                "environments.updated" => await ProcessEnvironmentEventAsync(webhookEvent, cancellationToken),
                _ => CreateUnsupportedEventResult(webhookEvent)
            };

            _logger.LogDebug("Webhook event {EventId} processed successfully: {Processed}", 
                webhookEvent.EventId, result.Processed);

            return Result<WebhookProcessingResult>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook event {EventId}", webhookEvent.EventId);
            
            return Result<WebhookProcessingResult>.Success(new WebhookProcessingResult
            {
                EventId = webhookEvent.EventId,
                EventType = webhookEvent.EventName,
                Processed = false,
                SyncScheduled = false,
                ProcessedAt = DateTime.UtcNow,
                ErrorMessage = ex.Message
            });
        }
    }

    public Result<bool> IsEventTypeSupported(string eventName)
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            return Result<bool>.Failure(Error.Validation("Event name is required"));
        }

        return Result<bool>.Success(SupportedEventTypes.Contains(eventName));
    }

    public Result<IReadOnlyList<string>> GetSupportedEventTypes()
    {
        return Result<IReadOnlyList<string>>.Success(SupportedEventTypes.ToList());
    }

    private async Task<WebhookProcessingResult> ProcessUserEventAsync(
        DynamicWebhookEvent webhookEvent,
        CancellationToken cancellationToken)
    {
        // S2: Basic event processing - log and schedule sync
        _logger.LogInformation("Processing user event {EventType} for user {UserId}", 
            webhookEvent.EventName, 
            webhookEvent.Data.GetValueOrDefault("userId"));

        // In S2, we just acknowledge and schedule data synchronization
        // Future stages will implement actual business logic
        
        return new WebhookProcessingResult
        {
            EventId = webhookEvent.EventId,
            EventType = webhookEvent.EventName,
            Processed = true,
            SyncScheduled = true,
            ProcessedAt = DateTime.UtcNow,
            Metadata = new Dictionary<string, object>
            {
                ["action"] = "user_sync_scheduled",
                ["userId"] = webhookEvent.Data.GetValueOrDefault("userId", "unknown")
            }
        };
    }

    private async Task<WebhookProcessingResult> ProcessWalletEventAsync(
        DynamicWebhookEvent webhookEvent,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing wallet event {EventType} for user {UserId}", 
            webhookEvent.EventName, 
            webhookEvent.Data.GetValueOrDefault("userId"));

        return new WebhookProcessingResult
        {
            EventId = webhookEvent.EventId,
            EventType = webhookEvent.EventName,
            Processed = true,
            SyncScheduled = true,
            ProcessedAt = DateTime.UtcNow,
            Metadata = new Dictionary<string, object>
            {
                ["action"] = "wallet_sync_scheduled",
                ["userId"] = webhookEvent.Data.GetValueOrDefault("userId", "unknown"),
                ["walletId"] = webhookEvent.Data.GetValueOrDefault("walletId", "unknown")
            }
        };
    }

    private async Task<WebhookProcessingResult> ProcessSessionEventAsync(
        DynamicWebhookEvent webhookEvent,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing session event {EventType}", webhookEvent.EventName);

        return new WebhookProcessingResult
        {
            EventId = webhookEvent.EventId,
            EventType = webhookEvent.EventName,
            Processed = true,
            SyncScheduled = false, // Session events don't require data sync
            ProcessedAt = DateTime.UtcNow,
            Metadata = new Dictionary<string, object>
            {
                ["action"] = "session_event_logged"
            }
        };
    }

    private async Task<WebhookProcessingResult> ProcessEnvironmentEventAsync(
        DynamicWebhookEvent webhookEvent,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing environment event {EventType}", webhookEvent.EventName);

        return new WebhookProcessingResult
        {
            EventId = webhookEvent.EventId,
            EventType = webhookEvent.EventName,
            Processed = true,
            SyncScheduled = false,
            ProcessedAt = DateTime.UtcNow,
            Metadata = new Dictionary<string, object>
            {
                ["action"] = "environment_config_refresh_scheduled"
            }
        };
    }

    private static WebhookProcessingResult CreateUnsupportedEventResult(DynamicWebhookEvent webhookEvent)
    {
        return new WebhookProcessingResult
        {
            EventId = webhookEvent.EventId,
            EventType = webhookEvent.EventName,
            Processed = false,
            SyncScheduled = false,
            ProcessedAt = DateTime.UtcNow,
            ErrorMessage = $"Event type '{webhookEvent.EventName}' is not supported"
        };
    }
}
```

---

## 7. Domain Models (Infrastructure Layer)

### 7.1 Core Models

```csharp
// src/Modules/Identity/Infrastructure/External/DynamicXyz/Models/DynamicUser.cs

namespace Axon.Modules.Identity.Infrastructure.External.DynamicXyz.Models;

/// <summary>
/// Dynamic.xyz user model (Infrastructure layer)
/// </summary>
public sealed record DynamicUser
{
    public required Guid Id { get; init; }
    public required Guid ProjectEnvironmentId { get; init; }
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
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
}

/// <summary>
/// Dynamic.xyz wallet model (Infrastructure layer)
/// </summary>
public sealed record DynamicWallet
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required BlockchainType Chain { get; init; }
    public required string PublicKey { get; init; }
    public required WalletProvider Provider { get; init; }
    public WalletProperties? Properties { get; init; }
    public DateTime? LastSelectedAt { get; init; }
}

/// <summary>
/// Wallet provider-specific properties
/// </summary>
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

/// <summary>
/// Supported blockchain types
/// </summary>
public enum BlockchainType
{
    ETH,
    SOL,
    BTC,
    ALGO,
    FLOW,
    STARK,
    COSMOS,
    SUI,
    ECLIPSE
}

/// <summary>
/// Supported wallet providers
/// </summary>
public enum WalletProvider
{
    BrowserExtension,
    WalletConnect,
    Email,
    Social,
    Embedded,
    Passkey
}

/// <summary>
/// JWT payload from Dynamic.xyz
/// </summary>
public sealed record DynamicJwtPayload
{
    public required string Subject { get; init; }
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required DateTime ExpiresAt { get; init; }
    public required DateTime IssuedAt { get; init; }
    public required IReadOnlyList<string> Scopes { get; init; }
    public required string EnvironmentId { get; init; }
    public IReadOnlyList<string> Lists { get; init; } = [];
    public IReadOnlyDictionary<string, object> Claims { get; init; } = new Dictionary<string, object>();
}

/// <summary>
/// Webhook event from Dynamic.xyz
/// </summary>
public sealed record DynamicWebhookEvent
{
    public required string EventId { get; init; }
    public required string EventName { get; init; }
    public string? WebhookId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required IReadOnlyDictionary<string, object> Data { get; init; }
}
```

---

## 8. Configuration and Service Registration

### 8.1 Configuration Model

```csharp
// src/Modules/Identity/Infrastructure/Configuration/DynamicXyzConfiguration.cs

namespace Axon.Modules.Identity.Infrastructure.Configuration;

/// <summary>
/// Dynamic.xyz integration configuration
/// </summary>
public sealed class DynamicXyzConfiguration
{
    public const string SectionName = "DynamicXyz";
    
    /// <summary>
    /// API client configuration
    /// </summary>
    public required DynamicApiClientOptions Api { get; init; }
    
    /// <summary>
    /// JWT validation configuration
    /// </summary>
    public JwtValidationOptions Jwt { get; init; } = new();
    
    /// <summary>
    /// Webhook processing configuration
    /// </summary>
    public WebhookOptions Webhooks { get; init; } = new();
}

/// <summary>
/// JWT validation specific options
/// </summary>
public sealed class JwtValidationOptions
{
    /// <summary>
    /// Clock skew tolerance in minutes
    /// </summary>
    public int ClockSkewMinutes { get; init; } = 5;
    
    /// <summary>
    /// JWKS cache duration in minutes
    /// </summary>
    public int JwksCacheMinutes { get; init; } = 10;
    
    /// <summary>
    /// Enable detailed JWT logging (development only)
    /// </summary>
    public bool EnableDetailedLogging { get; init; } = false;
}

/// <summary>
/// Webhook processing specific options
/// </summary>
public sealed class WebhookOptions
{
    /// <summary>
    /// Maximum webhook age in minutes for timestamp validation
    /// </summary>
    public int MaxWebhookAgeMinutes { get; init; } = 5;
    
    /// <summary>
    /// Enable webhook signature validation
    /// </summary>
    public bool ValidateSignatures { get; init; } = true;
    
    /// <summary>
    /// Enable webhook processing
    /// </summary>
    public bool EnableProcessing { get; init; } = true;
}
```

### 8.2 Service Registration

```csharp
// src/Modules/Identity/Infrastructure/External/DynamicXyz/Extensions/ServiceCollectionExtensions.cs

namespace Axon.Modules.Identity.Infrastructure.External.DynamicXyz.Extensions;

/// <summary>
/// Service collection extensions for Dynamic.xyz integration
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add Dynamic.xyz infrastructure services
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddDynamicXyzInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register configuration
        services.Configure<DynamicXyzConfiguration>(
            configuration.GetSection(DynamicXyzConfiguration.SectionName));
        services.Configure<DynamicApiClientOptions>(
            configuration.GetSection($"{DynamicXyzConfiguration.SectionName}:Api"));

        // Register HTTP clients
        services.AddHttpClient<IDynamicApiClient, DynamicApiClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<DynamicApiClientOptions>>();
            client.BaseAddress = new Uri(options.Value.BaseUrl);
            client.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", options.Value.ApiToken);
            client.Timeout = TimeSpan.FromSeconds(options.Value.TimeoutSeconds);
        });

        // Add Polly for retry policies
        services.AddHttpClient<DynamicApiClient>()
            .AddPolicyHandler(GetRetryPolicy());

        // Register Dynamic.xyz services
        services.AddScoped<IDynamicUserService, DynamicUserService>();
        services.AddScoped<IDynamicWalletService, DynamicWalletService>();

        // Register authentication services
        services.AddSingleton<IJwksKeyProvider, JwksKeyProvider>();
        services.AddScoped<IJwtValidationService, JwtValidationService>();

        // Register webhook services
        services.AddScoped<IWebhookSignatureValidator, WebhookSignatureValidator>();
        services.AddScoped<IWebhookProcessor, WebhookProcessor>();

        // Add memory cache for JWKS keys
        services.AddMemoryCache();

        // Add health checks
        services.AddHealthChecks()
            .AddCheck<DynamicXyzHealthCheck>("dynamic-xyz");

        return services;
    }

    /// <summary>
    /// Create retry policy for Dynamic.xyz API calls
    /// </summary>
    /// <returns>Retry policy</returns>
    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode && (int)r.StatusCode >= 500)
            .Or<HttpRequestException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => 
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    var logger = context.GetLogger();
                    logger?.LogWarning("Dynamic.xyz API retry {RetryCount}/3 in {Delay}s", 
                        retryCount, timespan.TotalSeconds);
                });
    }
}

/// <summary>
/// Health check for Dynamic.xyz integration
/// </summary>
internal sealed class DynamicXyzHealthCheck : IHealthCheck
{
    private readonly IDynamicApiClient _apiClient;
    private readonly IJwksKeyProvider _keyProvider;

    public DynamicXyzHealthCheck(IDynamicApiClient apiClient, IJwksKeyProvider keyProvider)
    {
        _apiClient = apiClient;
        _keyProvider = keyProvider;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check API connectivity
            var apiHealthResult = await _apiClient.HealthCheckAsync(cancellationToken);
            if (apiHealthResult.IsFailure)
            {
                return HealthCheckResult.Degraded("Dynamic.xyz API unreachable", 
                    data: new Dictionary<string, object> { ["error"] = apiHealthResult.Error.Message });
            }

            // Check JWKS key status
            var keyStatusResult = await _keyProvider.GetKeyStatusAsync();
            if (keyStatusResult.IsFailure)
            {
                return HealthCheckResult.Degraded("JWKS keys unavailable",
                    data: new Dictionary<string, object> { ["error"] = keyStatusResult.Error.Message });
            }

            var keyStatus = keyStatusResult.Value;
            if (!keyStatus.IsHealthy)
            {
                return HealthCheckResult.Degraded("JWKS keys are stale",
                    data: new Dictionary<string, object> 
                    {
                        ["keyCount"] = keyStatus.KeyCount,
                        ["cacheAge"] = keyStatus.CacheAge.ToString(),
                        ["lastRefresh"] = keyStatus.LastRefresh
                    });
            }

            return HealthCheckResult.Healthy("Dynamic.xyz integration healthy",
                data: new Dictionary<string, object>
                {
                    ["keyCount"] = keyStatus.KeyCount,
                    ["lastRefresh"] = keyStatus.LastRefresh
                });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Dynamic.xyz health check failed", ex);
        }
    }
}
```

---

## 9. Updated API Endpoints (S2 Integration)

### 9.1 Modified Exchange Token Endpoint

```csharp
// src/Api/Endpoints/V1/Identity/Commands/ExchangeToken/ExchangeTokenEndpoint.cs (S2 Update)

namespace Axon.Api.Endpoints.V1.Identity.Commands.ExchangeToken;

public sealed class ExchangeTokenEndpoint 
    : BaseMappedEndpoint<ExchangeTokenRequestDto, ExchangeTokenResponseDto>
{
    private const string Route = "/api/v1/auth/exchange";
    private const string Tag = "Authentication";

    private readonly IJwtValidationService _jwtValidationService;
    private readonly IDynamicUserService _dynamicUserService;
    private readonly ILogger<ExchangeTokenEndpoint> _logger;

    public ExchangeTokenEndpoint(
        IJwtValidationService jwtValidationService,
        IDynamicUserService dynamicUserService,
        ILogger<ExchangeTokenEndpoint> logger) : base(logger)
    {
        _jwtValidationService = jwtValidationService ?? throw new ArgumentNullException(nameof(jwtValidationService));
        _dynamicUserService = dynamicUserService ?? throw new ArgumentNullException(nameof(dynamicUserService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override void Configure()
    {
        Post(Route);
        AllowAnonymous();
        
        Summary(s =>
        {
            s.Summary = "Exchange Dynamic.xyz JWT token for Axon user context";
            s.Description = "Validates Dynamic.xyz JWT token and returns real user information with connected wallets";
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
    }

    protected override async Task<Result<ExchangeTokenResponseDto, Error>> ExecuteAsync(
        ExchangeTokenRequestDto request,
        CancellationToken ct)
    {
        // S2: Real Dynamic.xyz integration
        _logger.LogInformation("S2: Validating Dynamic.xyz JWT token and retrieving user data");

        // Step 1: Validate JWT token
        var jwtResult = await _jwtValidationService.ValidateTokenAsync(request.AuthToken, ct);
        if (jwtResult.IsFailure)
        {
            _logger.LogWarning("JWT validation failed: {Error}", jwtResult.Error.Message);
            return Result<ExchangeTokenResponseDto, Error>.Failure(jwtResult.Error);
        }

        var jwtPayload = jwtResult.Value;
        var dynamicUserId = Guid.Parse(jwtPayload.Subject);

        // Step 2: Retrieve user data from Dynamic.xyz
        var userResult = await _dynamicUserService.GetUserByIdAsync(dynamicUserId, ct);
        if (userResult.IsFailure)
        {
            _logger.LogError("Failed to retrieve user {UserId} from Dynamic.xyz: {Error}", 
                dynamicUserId, userResult.Error.Message);
            return Result<ExchangeTokenResponseDto, Error>.Failure(userResult.Error);
        }

        var dynamicUser = userResult.Value;

        // Step 3: Map to response DTO
        var response = new ExchangeTokenResponseDto
        {
            UserId = GenerateAxonUserId(dynamicUser.Id), // S2: Generate deterministic Axon ID
            DynamicUserId = dynamicUser.Id,
            Email = dynamicUser.Email,
            DisplayName = GetDisplayName(dynamicUser),
            Username = dynamicUser.Username,
            Wallets = dynamicUser.Wallets.Select(MapWalletToDto).ToList(),
            SyncedAt = DateTime.UtcNow,
            SyncStatus = "completed"
        };

        _logger.LogInformation("Successfully exchanged token for user {UserId} ({Email})", 
            response.UserId, response.Email);

        return Result<ExchangeTokenResponseDto, Error>.Success(response);
    }

    private static string GenerateAxonUserId(Guid dynamicUserId)
    {
        // S2: Generate deterministic Axon user ID from Dynamic.xyz ID
        // In S4/S5, this will be replaced with proper domain model IDs
        var hash = SHA256.HashData(dynamicUserId.ToByteArray());
        var base32 = Convert.ToBase32String(hash)[..16].ToLowerInvariant();
        return $"usr_{base32}";
    }

    private static string? GetDisplayName(DynamicUser user)
    {
        if (!string.IsNullOrWhiteSpace(user.FirstName) || !string.IsNullOrWhiteSpace(user.LastName))
        {
            return $"{user.FirstName} {user.LastName}".Trim();
        }
        return user.Username;
    }

    private static WalletDto MapWalletToDto(DynamicWallet wallet)
    {
        return new WalletDto
        {
            Id = GenerateAxonWalletId(wallet.Id),
            DynamicWalletId = wallet.Id,
            Address = wallet.PublicKey,
            Chain = wallet.Chain.ToString(),
            Provider = MapProviderToString(wallet.Provider),
            WalletName = wallet.Name,
            LastSelectedAt = wallet.LastSelectedAt,
            ConnectedAt = null // S2: Not available yet, will be added in S4/S5
        };
    }

    private static string GenerateAxonWalletId(Guid dynamicWalletId)
    {
        var hash = SHA256.HashData(dynamicWalletId.ToByteArray());
        var base32 = Convert.ToBase32String(hash)[..16].ToLowerInvariant();
        return $"wlt_{base32}";
    }

    private static string MapProviderToString(WalletProvider provider) => provider switch
    {
        WalletProvider.BrowserExtension => "browserextension",
        WalletProvider.WalletConnect => "walletconnect",
        WalletProvider.Email => "email",
        WalletProvider.Social => "social",
        WalletProvider.Embedded => "embedded",
        WalletProvider.Passkey => "passkey",
        _ => "unknown"
    };
}
```

### 9.2 Modified Webhook Endpoint

```csharp
// src/Api/Endpoints/V1/Webhooks/Dynamic/ProcessDynamicWebhookEndpoint.cs (S2 Update)

namespace Axon.Api.Endpoints.V1.Webhooks.Dynamic;

public sealed class ProcessDynamicWebhookEndpoint : Endpoint<DynamicWebhookEventDto, DynamicWebhookResponseDto>
{
    private const string Route = "/api/webhooks/dynamic";
    private const string Tag = "Webhooks";

    private readonly IWebhookSignatureValidator _signatureValidator;
    private readonly IWebhookProcessor _webhookProcessor;
    private readonly ILogger<ProcessDynamicWebhookEndpoint> _logger;

    public ProcessDynamicWebhookEndpoint(
        IWebhookSignatureValidator signatureValidator,
        IWebhookProcessor webhookProcessor,
        ILogger<ProcessDynamicWebhookEndpoint> logger)
    {
        _signatureValidator = signatureValidator ?? throw new ArgumentNullException(nameof(signatureValidator));
        _webhookProcessor = webhookProcessor ?? throw new ArgumentNullException(nameof(webhookProcessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override void Configure()
    {
        Post(Route);
        AllowAnonymous(); // Uses signature validation instead
        
        Summary(s =>
        {
            s.Summary = "Process Dynamic.xyz webhook events";
            s.Description = "Receives and processes lifecycle events from Dynamic.xyz with signature validation";
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
            s.Responses[401] = "Invalid webhook signature";
            s.Responses[500] = "Processing failed";
        });

        Tags(Tag);
    }

    public override async Task HandleAsync(DynamicWebhookEventDto req, CancellationToken ct)
    {
        _logger.LogInformation("S2: Processing webhook event {EventType} with ID {EventId}", 
            req.EventName, req.EventId);

        // Step 1: Validate webhook signature
        var signature = HttpContext.Request.Headers["X-Dynamic-Signature"].FirstOrDefault();
        if (!string.IsNullOrEmpty(signature))
        {
            var payload = await HttpContext.Request.GetRawBodyStringAsync();
            var signatureResult = await _signatureValidator.ValidateSignatureAsync(payload, signature, ct);
            
            if (signatureResult.IsFailure || !signatureResult.Value)
            {
                _logger.LogWarning("Invalid webhook signature for event {EventId}", req.EventId);
                await SendAsync(new DynamicWebhookResponseDto
                {
                    Received = false,
                    ProcessedAt = DateTime.UtcNow,
                    SyncScheduled = false,
                    Acknowledgment = new WebhookAcknowledgmentDto
                    {
                        EventId = req.EventId,
                        WebhookId = req.WebhookId ?? "",
                        Status = "signature_invalid",
                        RetryCount = 0,
                        ErrorMessage = "Invalid webhook signature"
                    }
                }, 401, ct);
                return;
            }
        }

        // Step 2: Validate webhook timestamp
        var timestamp = HttpContext.Request.Headers["X-Dynamic-Timestamp"].FirstOrDefault();
        if (!string.IsNullOrEmpty(timestamp))
        {
            var timestampResult = _signatureValidator.ValidateTimestamp(timestamp);
            if (timestampResult.IsFailure || !timestampResult.Value)
            {
                _logger.LogWarning("Invalid webhook timestamp for event {EventId}: {Timestamp}", req.EventId, timestamp);
                await SendAsync(new DynamicWebhookResponseDto
                {
                    Received = false,
                    ProcessedAt = DateTime.UtcNow,
                    SyncScheduled = false,
                    Acknowledgment = new WebhookAcknowledgmentDto
                    {
                        EventId = req.EventId,
                        WebhookId = req.WebhookId ?? "",
                        Status = "timestamp_invalid",
                        RetryCount = 0,
                        ErrorMessage = "Webhook timestamp is too old"
                    }
                }, 400, ct);
                return;
            }
        }

        // Step 3: Process webhook event
        var webhookEvent = new DynamicWebhookEvent
        {
            EventId = req.EventId,
            EventName = req.EventName,
            WebhookId = req.WebhookId,
            CreatedAt = req.CreatedAt,
            Data = req.Data
        };

        var processingResult = await _webhookProcessor.ProcessEventAsync(webhookEvent, ct);

        if (processingResult.IsFailure)
        {
            _logger.LogError("Failed to process webhook event {EventId}: {Error}", 
                req.EventId, processingResult.Error.Message);

            await SendAsync(new DynamicWebhookResponseDto
            {
                Received = true,
                ProcessedAt = DateTime.UtcNow,
                SyncScheduled = false,
                Acknowledgment = new WebhookAcknowledgmentDto
                {
                    EventId = req.EventId,
                    WebhookId = req.WebhookId ?? "",
                    Status = "processing_failed",
                    RetryCount = 0,
                    ErrorMessage = processingResult.Error.Message
                }
            }, 500, ct);
            return;
        }

        var result = processingResult.Value;
        var response = new DynamicWebhookResponseDto
        {
            Received = true,
            ProcessedAt = result.ProcessedAt,
            SyncScheduled = result.SyncScheduled,
            Acknowledgment = new WebhookAcknowledgmentDto
            {
                EventId = result.EventId,
                WebhookId = req.WebhookId ?? "",
                Status = result.Processed ? "processed" : "ignored",
                RetryCount = 0,
                ErrorMessage = result.ErrorMessage
            }
        };

        await SendOkAsync(response, ct);
    }
}
```

---

## 10. Configuration Files

### 10.1 appsettings.json Structure

```json
{
  "DynamicXyz": {
    "Api": {
      "BaseUrl": "https://app.dynamic.xyz/api/v0",
      "ApiToken": "dyn_XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
      "EnvironmentId": "95b11417-f18f-457f-8804-68e361f9164f",
      "TimeoutSeconds": 30,
      "MaxRetryAttempts": 3,
      "RetryBaseDelaySeconds": 2.0,
      "EnableDetailedLogging": false
    },
    "Jwt": {
      "ClockSkewMinutes": 5,
      "JwksCacheMinutes": 10,
      "EnableDetailedLogging": false
    },
    "Webhooks": {
      "MaxWebhookAgeMinutes": 5,
      "ValidateSignatures": true,
      "EnableProcessing": true
    }
  }
}
```

### 10.2 Environment-Specific Configuration

```json
// appsettings.Development.json
{
  "DynamicXyz": {
    "Api": {
      "BaseUrl": "http://localhost:3333/api/v0",
      "EnableDetailedLogging": true
    },
    "Jwt": {
      "EnableDetailedLogging": true
    }
  }
}

// appsettings.Production.json
{
  "DynamicXyz": {
    "Api": {
      "BaseUrl": "https://app.dynamic.xyz/api/v0",
      "TimeoutSeconds": 60,
      "MaxRetryAttempts": 5
    },
    "Webhooks": {
      "ValidateSignatures": true
    }
  }
}
```

---

## 11. Integration Tests (S2 Stage)

### 11.1 Dynamic.xyz API Client Tests

```csharp
// Tests/Modules.Identity.Infrastructure.Tests/External/DynamicXyz/DynamicApiClientTests.cs

namespace Axon.Tests.Modules.Identity.Infrastructure.External.DynamicXyz;

public sealed class DynamicApiClientTests : IClassFixture<DynamicXyzTestFixture>
{
    private readonly DynamicXyzTestFixture _fixture;
    private readonly IDynamicApiClient _client;

    public DynamicApiClientTests(DynamicXyzTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.GetService<IDynamicApiClient>();
    }

    [Test]
    public async Task GetAsync_ValidEndpoint_ReturnsData()
    {
        // Arrange
        var endpoint = $"environments/{_fixture.EnvironmentId}";

        // Act
        var result = await _client.GetAsync<DynamicEnvironmentResponse>(endpoint);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(_fixture.EnvironmentId);
    }

    [Test]
    public async Task HealthCheckAsync_ValidConfiguration_ReturnsSuccess()
    {
        // Act
        var result = await _client.HealthCheckAsync();

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Test]
    public async Task GetAsync_InvalidEndpoint_ReturnsError()
    {
        // Arrange
        var endpoint = "invalid/endpoint/path";

        // Act
        var result = await _client.GetAsync<object>(endpoint);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }
}
```

### 11.2 JWT Validation Tests

```csharp
// Tests/Modules.Identity.Infrastructure.Tests/External/DynamicXyz/JwtValidationServiceTests.cs

namespace Axon.Tests.Modules.Identity.Infrastructure.External.DynamicXyz;

public sealed class JwtValidationServiceTests : IClassFixture<DynamicXyzTestFixture>
{
    private readonly DynamicXyzTestFixture _fixture;
    private readonly IJwtValidationService _jwtService;

    public JwtValidationServiceTests(DynamicXyzTestFixture fixture)
    {
        _fixture = fixture;
        _jwtService = fixture.GetService<IJwtValidationService>();
    }

    [Test]
    public async Task ValidateTokenAsync_ValidToken_ReturnsPayload()
    {
        // Arrange
        var validToken = await _fixture.CreateValidJwtTokenAsync();

        // Act
        var result = await _jwtService.ValidateTokenAsync(validToken);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Subject.Should().NotBeEmpty();
        result.Value.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Test]
    public async Task ValidateTokenAsync_ExpiredToken_ReturnsAuthenticationError()
    {
        // Arrange
        var expiredToken = await _fixture.CreateExpiredJwtTokenAsync();

        // Act
        var result = await _jwtService.ValidateTokenAsync(expiredToken);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Authentication);
        result.Error.Message.Should().Contain("expired");
    }

    [Test]
    public void IsValidJwtFormat_ValidFormat_ReturnsTrue()
    {
        // Arrange
        var validToken = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiYWRtaW4iOnRydWV9.signature";

        // Act
        var result = _jwtService.IsValidJwtFormat(validToken);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Test]
    public void IsValidJwtFormat_InvalidFormat_ReturnsFalse()
    {
        // Arrange
        var invalidToken = "not.a.jwt.token.format";

        // Act
        var result = _jwtService.IsValidJwtFormat(invalidToken);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }
}
```

### 11.3 Test Fixture

```csharp
// Tests/Modules.Identity.Infrastructure.Tests/Fixtures/DynamicXyzTestFixture.cs

namespace Axon.Tests.Modules.Identity.Infrastructure.Fixtures;

public sealed class DynamicXyzTestFixture : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;

    public string EnvironmentId { get; }
    public string ApiToken { get; }

    public DynamicXyzTestFixture()
    {
        // Load test configuration
        _configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Test.json", optional: false)
            .AddEnvironmentVariables("AXON_TEST_")
            .Build();

        EnvironmentId = _configuration["DynamicXyz:Api:EnvironmentId"] ?? throw new InvalidOperationException("Test environment ID not configured");
        ApiToken = _configuration["DynamicXyz:Api:ApiToken"] ?? throw new InvalidOperationException("Test API token not configured");

        // Build service provider
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    public T GetService<T>() where T : notnull => _serviceProvider.GetRequiredService<T>();

    public async Task<string> CreateValidJwtTokenAsync()
    {
        // For integration tests, use actual Dynamic.xyz sandbox tokens
        // or create test tokens using the same signing keys
        return await GetTestTokenFromDynamicXyz();
    }

    public async Task<string> CreateExpiredJwtTokenAsync()
    {
        // Create or retrieve an expired test token
        return await GetExpiredTestTokenFromDynamicXyz();
    }

    private void ConfigureServices(ServiceCollection services)
    {
        services.AddSingleton(_configuration);
        services.AddDynamicXyzInfrastructure(_configuration);
        services.AddLogging(builder => builder.AddConsole());
    }

    private async Task<string> GetTestTokenFromDynamicXyz()
    {
        // Implementation depends on Dynamic.xyz test environment
        // Either use sandbox API to create test tokens or use pre-created test tokens
        throw new NotImplementedException("Implement based on Dynamic.xyz test environment setup");
    }

    private async Task<string> GetExpiredTestTokenFromDynamicXyz()
    {
        // Return a known expired token for testing
        throw new NotImplementedException("Implement based on Dynamic.xyz test environment setup");
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}
```

---

## 12. Implementation Checklist

### 12.1 S2 Stage Tasks

- [ ] **Create Infrastructure Directory Structure**
  - [ ] `src/Modules/Identity/Infrastructure/External/DynamicXyz/` with all subfolders
  - [ ] `src/Modules/Identity/Infrastructure/Configuration/`
  - [ ] Test projects structure

- [ ] **Implement Dynamic.xyz API Client**
  - [ ] `DynamicApiClient` with retry policies and error handling
  - [ ] `DynamicUserService` and `DynamicWalletService`
  - [ ] API response models with JSON serialization
  - [ ] HTTP client configuration with authentication

- [ ] **Implement JWT Validation Infrastructure**
  - [ ] `JwksKeyProvider` with 10-minute caching
  - [ ] `JwtValidationService` with RS256 validation
  - [ ] JWKS endpoint integration and key rotation support
  - [ ] Proper error handling for all JWT scenarios

- [ ] **Implement Webhook Infrastructure**
  - [ ] `WebhookSignatureValidator` with HMAC-SHA256 validation
  - [ ] `WebhookProcessor` with event type routing
  - [ ] Timestamp validation and replay attack prevention
  - [ ] Event deduplication mechanism

- [ ] **Create Domain Models**
  - [ ] `DynamicUser`, `DynamicWallet`, `DynamicJwtPayload` records
  - [ ] Enum types for `BlockchainType` and `WalletProvider`
  - [ ] Mapping from API responses to domain models

- [ ] **Configure Service Registration**
  - [ ] `ServiceCollectionExtensions` with all services
  - [ ] Health checks for Dynamic.xyz integration
  - [ ] Polly retry policies for resilience
  - [ ] Memory cache for JWKS keys

- [ ] **Update API Endpoints**
  - [ ] Replace S1 stub implementations with real Dynamic.xyz calls
  - [ ] Add signature validation to webhook endpoint
  - [ ] Proper error handling and logging
  - [ ] Maintain API contract compatibility

- [ ] **Add Configuration Support**
  - [ ] Environment-specific appsettings files
  - [ ] Configuration validation and options pattern
  - [ ] Secure token storage and access

- [ ] **Create Integration Tests**
  - [ ] API client tests with real Dynamic.xyz sandbox
  - [ ] JWT validation tests with valid and invalid tokens
  - [ ] Webhook signature validation tests
  - [ ] End-to-end authentication flow tests

### 12.2 Exit Criteria Verification

- [ ] **JWT tokens successfully validated against Dynamic.xyz JWKS endpoint**
  - [ ] JWKS keys are cached for 10 minutes
  - [ ] Key rotation is handled automatically
  - [ ] All JWT validation scenarios tested (valid, expired, invalid signature)

- [ ] **User data retrieved from Dynamic.xyz Management API**
  - [ ] User profiles retrieved with all fields
  - [ ] Wallet information retrieved and mapped correctly
  - [ ] API errors handled gracefully with proper retry logic

- [ ] **Webhook signatures validated cryptographically**
  - [ ] HMAC-SHA256 signature validation works correctly
  - [ ] Timestamp validation prevents replay attacks
  - [ ] All supported event types are processed

- [ ] **API endpoints return real Dynamic.xyz data (not stubs)**
  - [ ] `/api/v1/auth/exchange` validates tokens and returns real user data
  - [ ] `/api/v1/auth/me` returns current user profile (still stub in S2)
  - [ ] `/api/webhooks/dynamic` processes real webhook events

- [ ] **Integration tests pass with Dynamic.xyz sandbox environment**
  - [ ] All API client operations work with sandbox
  - [ ] JWT validation works with sandbox JWKS keys
  - [ ] Webhook processing works with sandbox events

- [ ] **Configuration supports multiple environments (dev/staging/prod)**
  - [ ] Environment-specific configuration files
  - [ ] Secure token storage and access
  - [ ] Health checks report integration status

- [ ] **Proper error handling and logging for all external calls**
  - [ ] All errors are logged with appropriate levels
  - [ ] Error details are sanitized for external responses
  - [ ] Retry policies handle transient failures

---

## 13. Next Steps

Upon completion of S2, the infrastructure foundation will be ready for:

**S3 - Application Layer**: Add CQRS command/query handlers that abstract infrastructure calls behind clean application interfaces

**S4 - Domain Layer**: Implement proper domain models, aggregates, and business rules with rich domain logic

**S5 - Persistence Layer**: Add Entity Framework, local data mirroring, and comprehensive webhook event processing

The infrastructure integration established in S2 provides the solid foundation for Dynamic.xyz communication while maintaining clean separation from business logic through the Result pattern and dependency injection.