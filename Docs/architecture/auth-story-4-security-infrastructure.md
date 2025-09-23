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

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Challenge endpoint - fixed window by IP
    options.AddFixedWindowLimiter("AuthChallenge", limiterOptions =>
    {
        limiterOptions.PermitLimit = 10;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 2;
    });

    // Verify endpoint - sliding window by IP + wallet
    options.AddSlidingWindowLimiter("AuthVerify", limiterOptions =>
    {
        limiterOptions.PermitLimit = 5;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.SegmentsPerWindow = 2;
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 1;
    });

    // Exchange endpoint - can reuse or create specific
    options.AddFixedWindowLimiter("AuthExchange", limiterOptions =>
    {
        limiterOptions.PermitLimit = 20;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 5;
    });
});

// Enable rate limiting middleware
app.UseRateLimiter();
```

### Task 4.2: Create Security Headers Preprocessor
**File:** `src/Api/Middleware/SecurityHeadersPreProcessor.cs`

- [ ] Add no-cache headers for auth responses
- [ ] Consider security headers for CORS if needed

```csharp
namespace Axon.Api.Middleware;

public sealed class SecurityHeadersPreProcessor<TRequest> : IPreProcessor<TRequest>
{
    public Task PreProcessAsync(
        IPreProcessorContext<TRequest> context,
        CancellationToken ct = default)
    {
        var response = context.HttpContext.Response;

        // Prevent caching of authentication responses
        response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
        response.Headers["Pragma"] = "no-cache";
        response.Headers["Expires"] = "0";

        // Additional security headers
        response.Headers["X-Content-Type-Options"] = "nosniff";
        response.Headers["X-Frame-Options"] = "DENY";

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
}
```

### Task 4.4: Configure Memory Cache for Replay Protection
**File:** `src/Api/Program.cs` or Service configuration

- [ ] Configure memory cache with size limits
- [ ] Set eviction policies
- [ ] Monitor cache memory usage

```csharp
// Configure memory cache with size limits
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 10000; // Maximum number of entries
    options.CompactionPercentage = 0.25; // Compact by 25% when limit reached
    options.ExpirationScanFrequency = TimeSpan.FromMinutes(1); // Scan for expired items
});

// Optional: Add distributed cache for scaling
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = configuration.GetConnectionString("Redis");
    options.InstanceName = "AxonAuth";
});
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
- [ ] Test rate limiting on verify endpoint
- [ ] Verify 429 responses
- [ ] Test queue behavior

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
    public async Task VerifyEndpoint_DifferentWallets_IndependentLimits()
    {
        // Test that rate limiting is partitioned by wallet address
        // Each wallet should have its own limit
    }
}
```

### Task 4.7: Performance Testing
**File:** `tests/Performance/AuthenticationLoadTests.cs`

- [ ] Load test replay cache under pressure
- [ ] Verify memory usage stays within limits
- [ ] Test cache eviction behavior

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
                    Size = 1,
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                });
            }));
        }

        await Task.WhenAll(tasks);

        // Verify cache respects size limit
        cache.Count.Should().BeLessOrEqualTo(1000);
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
- Redis cache for horizontal scaling
- Rate limit synchronization across instances
- Memory cache warm-up strategy
- Graceful degradation if cache fails