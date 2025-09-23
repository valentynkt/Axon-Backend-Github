# Story 4: Security Infrastructure & Rate Limiting

## Overview
Implement security infrastructure including rate limiting policies, security headers, secure logging, and testing frameworks to ensure the authentication system is robust and secure.

## Success Criteria
- Separate rate limiting policies for challenge and verify endpoints
- Security headers prevent caching of sensitive responses
- No sensitive data appears in logs
- Comprehensive test coverage for all security scenarios
- Memory cache properly manages replay protection

## Tasks

### Task 4.1: Configure Rate Limiting Policies
**File:** `src/Api/Program.cs` or Rate Limiting configuration

- [ ] Define "AuthChallenge" policy (10 req/min/IP)
- [ ] Define "AuthVerify" policy (5 req/min/IP + wallet)
- [ ] Configure sliding window for smoother throttling
- [ ] Use partitioned rate limiting for per-caller isolation

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // /auth/challenge: 10 req/min per IP (fixed window)
    options.AddPolicy("AuthChallenge", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ip,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 2,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });
    });

    // /auth/verify: 5 req/min per (IP + wallet) (sliding window)
    // Client MUST send X-Wallet-Address header for proper partitioning
    options.AddPolicy("AuthVerify", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var addr = httpContext.Request.Headers.TryGetValue("X-Wallet-Address", out var h)
            ? h.ToString().Trim()
            : string.Empty;

        var key = string.IsNullOrEmpty(addr) ? ip : $"{ip}:{addr}";
        return RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: key,
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 2,
                QueueLimit = 1,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });
    });

    // /auth/exchange: 20 req/min per IP (fixed window)
    options.AddPolicy("AuthExchange", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ip,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 5,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });
    });
});

// Enable rate limiting middleware
app.UseRateLimiter();

// In endpoints, use: Options(o => o.RequireRateLimiting("AuthChallenge"))
```

### Task 4.2: Create Security Headers Preprocessor
**File:** `src/Api/Middleware/SecurityHeadersPreProcessor.cs`

- [ ] Add strong no-cache headers for auth responses
- [ ] Add referrer policy for privacy
- [ ] Keep CORS configuration separate

```csharp
namespace Axon.Api.Middleware;

public sealed class SecurityHeadersPreProcessor<TRequest> : IPreProcessor<TRequest>
{
    public Task PreProcessAsync(
        IPreProcessorContext<TRequest> context,
        CancellationToken ct = default)
    {
        var response = context.HttpContext.Response;

        // Prevent caching of authentication responses (strongest settings)
        response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
        response.Headers["Pragma"] = "no-cache";
        response.Headers["Expires"] = "0";

        // Additional security headers
        response.Headers["X-Content-Type-Options"] = "nosniff";
        response.Headers["X-Frame-Options"] = "DENY";
        response.Headers["Referrer-Policy"] = "no-referrer";

        // Note: CORS headers should be configured separately via CORS middleware

        return Task.CompletedTask;
    }
}
```

### Task 4.3: Implement Secure Logging
**File:** `src/Modules/Identity/Infrastructure/Services/SecureLogger.cs`

- [ ] Create logging helper that sanitizes sensitive data
- [ ] Hash addresses/signatures for correlation
- [ ] Never log MACs, signatures, or tokens

```csharp
namespace Axon.Modules.Identity.Infrastructure.Services;

public static class SecureLogger
{
    /// <summary>
    /// Creates a hash prefix for logging correlation without exposing sensitive data
    /// </summary>
    public static string HashPrefix(string sensitiveData, int prefixLength = 8)
    {
        if (string.IsNullOrEmpty(sensitiveData))
            return "empty";

        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(sensitiveData);
        var hash = sha256.ComputeHash(bytes);
        var base64 = Convert.ToBase64String(hash);

        return base64[..Math.Min(prefixLength, base64.Length)];
    }

    /// <summary>
    /// Masks wallet address keeping only prefix and suffix
    /// </summary>
    public static string MaskAddress(string address)
    {
        if (string.IsNullOrEmpty(address) || address.Length < 10)
            return "***";

        return $"{address[..4]}...{address[^4..]}";
    }

    /// <summary>
    /// Logs authentication attempt with sanitized data
    /// </summary>
    public static void LogAuthAttempt(
        ILogger logger,
        string operation,
        string chainId,
        string address,
        string mkv,
        bool success)
    {
        logger.LogInformation(
            "Auth {Operation}: chain={Chain} address={MaskedAddress} mkv={Mkv} success={Success}",
            operation,
            chainId,
            MaskAddress(address),
            mkv,
            success);
    }

    /// <summary>
    /// Logs exception safely without leaking sensitive information
    /// </summary>
    public static void LogAuthException(
        ILogger logger,
        Exception ex,
        string operation)
    {
        // Log only exception type and error code, never the message from token libs
        logger.LogError(
            "Auth {Operation} failed: {ExceptionType} {ErrorCode}",
            operation,
            ex.GetType().Name,
            ex.HResult);

        // In debug mode only, can log more details
        #if DEBUG
        logger.LogDebug(ex, "Auth exception details");
        #endif
    }
}
```

### Task 4.4: Configure Memory Cache for Replay Protection
**File:** `src/Api/Program.cs` or Service configuration

- [ ] Configure memory cache with size limits
- [ ] Set eviction policies
- [ ] Monitor cache memory usage
- [ ] Set Size=1 on each cache entry for proper eviction

```csharp
// Configure memory cache with size limits
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 10_000;                 // Maximum number of nonce entries
    options.CompactionPercentage = 0.25;        // Compact by 25% when limit reached
    options.ExpirationScanFrequency = TimeSpan.FromMinutes(1); // Scan for expired items
});

// When setting cache entries, MUST specify Size for limit enforcement:
_cache.Set(cacheKey, true, new MemoryCacheEntryOptions
{
    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(remaining),
    Size = 1  // Required when SizeLimit is configured
});

// Note: Redis support deferred to future scaling requirements
// IMemoryCache is sufficient for single-instance deployments
```

### Task 4.5: Create Comprehensive Test Suite
**File:** `tests/Modules/Identity/Infrastructure/Services/AuthenticationServiceTests.cs`

- [ ] Test HMAC generation and validation
- [ ] Test replay protection
- [ ] Test TTL validation
- [ ] Test clock skew handling

```csharp
[TestFixture]
public class AuthenticationServiceTests
{
    private AuthenticationService _service = null!;
    private IMemoryCache _cache = null!;
    private IOptions<AuthenticationOptions> _options = null!;

    [SetUp]
    public void SetUp()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _options = Options.Create(new AuthenticationOptions
        {
            HmacKeys = new Dictionary<string, string>
            {
                ["v1"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            },
            CurrentKeyVersion = "v1",
            MaxTtlSeconds = 300,
            ClockSkewSeconds = 60
        });

        _service = new AuthenticationService(_options, _cache, /* other deps */);
    }

    [Test]
    public void GenerateMacForChallenge_ValidInput_ProducesConsistentMac()
    {
        var message = "{\"test\":\"message\"}";
        var mac1 = _service.GenerateMacForChallenge(message, "v1");
        var mac2 = _service.GenerateMacForChallenge(message, "v1");

        mac1.Should().Be(mac2);
        mac1.Should().NotBeNullOrEmpty();
    }

    [Test]
    public void ValidateMac_ValidMac_ReturnsSuccess()
    {
        var message = "{\"test\":\"message\"}";
        var mac = _service.GenerateMacForChallenge(message, "v1");

        var result = _service.ValidateMac(message, mac, "v1");

        result.IsSuccess.Should().BeTrue();
    }

    [Test]
    public void ValidateMac_InvalidMac_ReturnsUnauthorized()
    {
        var message = "{\"test\":\"message\"}";
        var invalidMac = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        var result = _service.ValidateMac(message, invalidMac, "v1");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AUTH.INVALID_MAC");
    }

    [Test]
    public async Task CheckAndMarkNonceUsedAsync_FirstUse_ReturnsSuccess()
    {
        var message = """
            {
                "issued_at": 1234567890,
                "exp": 1234568190,
                "nonce": "unique-nonce"
            }
            """;

        var result = await _service.CheckAndMarkNonceUsedAsync(message, "v1");

        result.IsSuccess.Should().BeTrue();
    }

    [Test]
    public async Task CheckAndMarkNonceUsedAsync_SecondUse_ReturnsReplayError()
    {
        var message = """
            {
                "issued_at": 1234567890,
                "exp": 1234568190,
                "nonce": "unique-nonce"
            }
            """;

        await _service.CheckAndMarkNonceUsedAsync(message, "v1");
        var result = await _service.CheckAndMarkNonceUsedAsync(message, "v1");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AUTH.REPLAY");
    }
}
```

### Task 4.6: Integration Tests for Rate Limiting
**File:** `tests/Api/RateLimitingIntegrationTests.cs`

- [ ] Test rate limiting on challenge endpoint
- [ ] Test rate limiting on verify endpoint with X-Wallet-Address header
- [ ] Verify 429 responses
- [ ] Test partitioning by IP+wallet

```csharp
[TestFixture]
public class RateLimitingIntegrationTests : IntegrationTestBase
{
    [Test]
    public async Task ChallengeEndpoint_ExceedsRateLimit_Returns429()
    {
        // Make 10 requests (should succeed)
        for (int i = 0; i < 10; i++)
        {
            var response = await Client.PostAsJsonAsync("/api/v1/auth/challenge",
                new { /* request data */ });
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // 11th request should be rate limited
        var limitedResponse = await Client.PostAsJsonAsync("/api/v1/auth/challenge",
            new { /* request data */ });

        limitedResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Test]
    public async Task VerifyEndpoint_DifferentWalletHeaders_IndependentLimits()
    {
        var wallet1 = "9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM";
        var wallet2 = "8YtNMqUxvQRAyrZzDsGYdLVL9zYtAWWM9WzDXwBbmkg";

        // Make 5 requests for wallet1 (should succeed)
        for (int i = 0; i < 5; i++)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/verify");
            request.Headers.Add("X-Wallet-Address", wallet1);
            request.Content = JsonContent.Create(new { /* verify data */ });

            var response = await Client.SendAsync(request);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Make request for wallet2 (should succeed - different partition)
        var wallet2Request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/verify");
        wallet2Request.Headers.Add("X-Wallet-Address", wallet2);
        wallet2Request.Content = JsonContent.Create(new { /* verify data */ });

        var wallet2Response = await Client.SendAsync(wallet2Request);
        wallet2Response.StatusCode.Should().Be(HttpStatusCode.OK);

        // 6th request for wallet1 should be rate limited
        var limitedRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/verify");
        limitedRequest.Headers.Add("X-Wallet-Address", wallet1);
        limitedRequest.Content = JsonContent.Create(new { /* verify data */ });

        var limitedResponse = await Client.SendAsync(limitedRequest);
        limitedResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Test]
    public async Task VerifyEndpoint_NoWalletHeader_FallsBackToIPOnly()
    {
        // Test that requests without X-Wallet-Address header
        // fall back to IP-only rate limiting
        for (int i = 0; i < 5; i++)
        {
            var response = await Client.PostAsJsonAsync("/api/v1/auth/verify",
                new { /* verify data */ });
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // 6th request should be rate limited (IP-only partition)
        var limitedResponse = await Client.PostAsJsonAsync("/api/v1/auth/verify",
            new { /* verify data */ });
        limitedResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
```

### Task 4.7: Performance Testing
**File:** `tests/Performance/AuthenticationLoadTests.cs`

- [ ] Load test replay cache under pressure
- [ ] Verify memory usage stays within limits
- [ ] Test cache eviction behavior
- [ ] Verify Size parameter enforces limits

```csharp
[TestFixture]
[Category("Performance")]
public class AuthenticationLoadTests
{
    [Test]
    public async Task ReplayCache_HighLoad_MaintainsMemoryLimit()
    {
        var cache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = 1000
        });

        var tasks = new List<Task>();

        // Simulate high load
        for (int i = 0; i < 10000; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                var key = $"nonce:v1:{Guid.NewGuid()}";
                cache.Set(key, true, new MemoryCacheEntryOptions
                {
                    Size = 1,  // MUST set Size when SizeLimit is configured
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                });
            }));
        }

        await Task.WhenAll(tasks);

        // Verify cache respects size limit
        cache.Count.Should().BeLessOrEqualTo(1000);
    }

    [Test]
    public void MemoryCache_WithoutSize_IgnoresSizeLimit()
    {
        // This test demonstrates why Size must be set on entries
        var cache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = 10
        });

        // Add entries WITHOUT Size - limit will be ignored
        for (int i = 0; i < 100; i++)
        {
            cache.Set($"key{i}", i, TimeSpan.FromMinutes(1));
        }

        // Without Size, the limit is not enforced
        // This is why we MUST set Size=1 on each entry
        cache.Count.Should().BeGreaterThan(10); // Proves limit ignored
    }
}
```

### Task 4.8: Security Audit Checklist
**File:** `Docs/architecture/auth-security-checklist.md`

```markdown
# Authentication Security Checklist

## Cryptographic Security
- [ ] HMAC uses SHA256 with 256-bit keys
- [ ] MAC comparison uses constant-time equality
- [ ] Ed25519 signatures verified on exact bytes
- [ ] No cryptographic material logged

## Replay Protection
- [ ] Nonces cached for full TTL + clock skew
- [ ] Cache keys include MAC version
- [ ] Memory limits prevent DoS

## Rate Limiting
- [ ] Challenge endpoint: 10 req/min/IP
- [ ] Verify endpoint: 5 req/min/IP+wallet
- [ ] Exchange endpoint: 20 req/min/IP
- [ ] Queue limits prevent memory exhaustion

## Logging & Monitoring
- [ ] No sensitive data in logs
- [ ] Address/signature correlation via hashes
- [ ] Rate limit violations logged
- [ ] Replay attempts logged

## HTTP Security
- [ ] No-cache headers on auth responses
- [ ] X-Content-Type-Options: nosniff
- [ ] X-Frame-Options: DENY
- [ ] CORS properly configured

## Error Handling
- [ ] Consistent error codes
- [ ] No information leakage in errors
- [ ] Timing-safe validations
```

## Testing Requirements

### Security Tests
- HMAC tampering detection
- Replay attack prevention
- Rate limiting effectiveness
- Cache memory limits
- Clock skew tolerance

### Performance Tests
- 1000+ concurrent verifications
- Memory usage under load
- Cache eviction behavior
- Rate limiter performance

### Integration Tests
- End-to-end authentication flows
- Multi-endpoint rate limiting
- Cache sharing between endpoints

## Monitoring & Alerts

### Metrics to Track
- Rate limit violations per endpoint
- Replay attempts detected
- Cache hit/miss ratios
- Authentication success/failure rates
- Memory cache size

### Alerts to Configure
- Spike in replay attempts
- Unusual rate limit violations
- Memory cache approaching limit
- Authentication failure rate anomaly

## Deployment Considerations
- IMemoryCache for single-instance deployments
- Redis cache consideration for future horizontal scaling
- Rate limit synchronization across instances (future)
- Memory cache warm-up strategy
- Graceful degradation if cache fails

## API Documentation Updates

### /auth/verify Endpoint
**Required Headers:**
- `X-Wallet-Address`: Wallet address for rate limit partitioning
  - Used ONLY for rate limiting, not authentication
  - If absent, falls back to IP-only rate limiting
  - The address in the header should match the one in the request body

**Rate Limits:**
- With `X-Wallet-Address` header: 5 requests/minute per IP+wallet combination
- Without header: 5 requests/minute per IP only
- Different wallet addresses on same IP have independent limits