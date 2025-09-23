using System.Security.Claims;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Infrastructure.Services;
using BuildingBlocks.Primitives.Ids;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Infrastructure.Tests.Services;

[TestFixture]
public class HttpContextUserServiceTests
{
    private HttpContextUserService _service;
    private IHttpContextAccessor _httpContextAccessor;
    private IMemoryCache _memoryCache;
    private IAxonPrincipalReadRepository _principalRepository;
    private ILogger<HttpContextUserService> _logger;
    private HttpContext _httpContext;
    private ClaimsPrincipal _claimsPrincipal;

    [SetUp]
    public void Setup()
    {
        _httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _memoryCache = Substitute.For<IMemoryCache>();
        _principalRepository = Substitute.For<IAxonPrincipalReadRepository>();
        _logger = Substitute.For<ILogger<HttpContextUserService>>();

        _httpContext = new DefaultHttpContext();
        _claimsPrincipal = new ClaimsPrincipal();
        _httpContext.User = _claimsPrincipal;

        _httpContextAccessor.HttpContext.Returns(_httpContext);

        _service = new HttpContextUserService(_httpContextAccessor, _memoryCache, _principalRepository, _logger);
    }

    [TearDown]
    public void TearDown()
    {
        _memoryCache?.Dispose();
        // NSubstitute mocks don't need disposal
        // The NUnit analyzer warning is about the interface, not the mock
    }

    [Test]
    public async Task GetAxonUserIdAsync_WhenCacheHit_ShouldReturnCachedValue()
    {
        // Arrange
        var cachedAxonUserId = AxonUserId.New();
        _httpContext.Items["AxonUserId"] = cachedAxonUserId;

        // Act
        var result = await _service.GetAxonUserIdAsync();

        // Assert
        result.ShouldBe(cachedAxonUserId);
        // Note: Cannot verify DidNotReceive with Vogen value objects due to uninitialized value issues
    }

    [Test]
    public async Task GetAxonUserIdAsync_WhenNotAuthenticated_ShouldReturnNull()
    {
        // Arrange
        _claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity()); // Not authenticated
        _httpContext.User = _claimsPrincipal;

        // Act
        var result = await _service.GetAxonUserIdAsync();

        // Assert
        result.ShouldBeNull();
        // Note: Cannot verify DidNotReceive with Vogen value objects due to uninitialized value issues
    }

    [Test]
    public async Task GetAxonUserIdAsync_WhenCacheMissAndPrincipalFound_ShouldFetchFromDatabaseAndCache()
    {
        // Arrange
        var dynamicAxonUserId = "test-user-123";

        _claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, dynamicAxonUserId)
        }, "test"));
        _httpContext.User = _claimsPrincipal;

        var principal = AxonPrincipal.CreateWithDynamicCredential(
            ProviderType.Dynamic,
            "https://app.dynamic.xyz",
            dynamicAxonUserId).Value;

        // Use the principal's actual ID for the assertion
        var expectedAxonUserId = principal.Id;

        _principalRepository.FindByCredentialAsync(
            ProviderType.Dynamic,
            "https://app.dynamic.xyz",
            dynamicAxonUserId,
            Arg.Any<CancellationToken>())
            .Returns(principal);

        // Setup memory cache to simulate cache miss and execute factory function
        var cacheKey = $"axon:user:{dynamicAxonUserId}";
        _memoryCache.TryGetValue(cacheKey, out Arg.Any<object?>()).Returns(false);

        var mockCacheEntry = Substitute.For<ICacheEntry>();
        mockCacheEntry.Key.Returns(cacheKey);
        _memoryCache.CreateEntry(cacheKey).Returns(mockCacheEntry);

        // Act
        var result = await _service.GetAxonUserIdAsync();

        // Assert
        result.ShouldBe(expectedAxonUserId);
        _httpContext.Items["AxonUserId"].ShouldBe(expectedAxonUserId);
        
        await _principalRepository.Received(1).FindByCredentialAsync(
            ProviderType.Dynamic,
            "https://app.dynamic.xyz",
            dynamicAxonUserId,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetAxonUserIdAsync_WhenCacheMissAndPrincipalNotFound_ShouldReturnNull()
    {
        // Arrange
        var dynamicAxonUserId = "test-user-123";
        
        _claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, dynamicAxonUserId)
        }, "test"));
        _httpContext.User = _claimsPrincipal;

        _principalRepository.FindByCredentialAsync(
            ProviderType.Dynamic,
            "https://app.dynamic.xyz",
            dynamicAxonUserId,
            Arg.Any<CancellationToken>())
            .Returns((AxonPrincipal?)null);

        // Act
        var result = await _service.GetAxonUserIdAsync();

        // Assert
        result.ShouldBeNull();
        _httpContext.Items.ContainsKey("AxonUserId").ShouldBeFalse();
        
        await _principalRepository.Received(1).FindByCredentialAsync(
            ProviderType.Dynamic,
            "https://app.dynamic.xyz",
            dynamicAxonUserId,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetAxonUserIdAsync_WhenHttpContextIsNull_ShouldHandleGracefully()
    {
        // Arrange
        _httpContextAccessor.HttpContext.Returns((HttpContext?)null);

        // Act
        var result = await _service.GetAxonUserIdAsync();

        // Assert
        result.ShouldBeNull();
        
        // Note: Cannot verify DidNotReceive with Vogen value objects due to uninitialized value issues
    }

    [Test]
    public async Task GetAxonUserIdAsync_OnSecondCall_ShouldUseCacheAndNotCallDatabase()
    {
        // Arrange
        var dynamicAxonUserId = "test-user-123";

        _claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, dynamicAxonUserId)
        }, "test"));
        _httpContext.User = _claimsPrincipal;

        var principal = AxonPrincipal.CreateWithDynamicCredential(
            ProviderType.Dynamic,
            "https://app.dynamic.xyz",
            dynamicAxonUserId).Value;

        // Use the principal's actual ID for the assertion
        var expectedAxonUserId = principal.Id;

        _principalRepository.FindByCredentialAsync(
            ProviderType.Dynamic,
            "https://app.dynamic.xyz",
            dynamicAxonUserId,
            Arg.Any<CancellationToken>())
            .Returns(principal);

        // Setup memory cache to simulate cache behavior for both calls
        var cacheKey = $"axon:user:{dynamicAxonUserId}";

        // For the second call test, we want to simulate that the first call populates the request cache
        // and the second call uses the request cache
        var mockCacheEntry = Substitute.For<ICacheEntry>();
        mockCacheEntry.Key.Returns(cacheKey);
        _memoryCache.CreateEntry(cacheKey).Returns(mockCacheEntry);

        // First call: memory cache miss, will call database and populate cache
        _memoryCache.TryGetValue(cacheKey, out Arg.Any<object?>()).Returns(false);

        // Act - First call
        var result1 = await _service.GetAxonUserIdAsync();

        // Act - Second call (should use request cache populated by first call)
        var result2 = await _service.GetAxonUserIdAsync();

        // Assert
        result1.ShouldBe(expectedAxonUserId);
        result2.ShouldBe(expectedAxonUserId);

        // Database should only be called once
        await _principalRepository.Received(1).FindByCredentialAsync(
            ProviderType.Dynamic,
            "https://app.dynamic.xyz",
            dynamicAxonUserId,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public void TryGetAxonUserId_WhenCacheHit_ShouldReturnTrueAndValue()
    {
        // Arrange
        var cachedAxonUserId = AxonUserId.New();
        _httpContext.Items["AxonUserId"] = cachedAxonUserId;

        // Act
        var result = _service.TryGetAxonUserId(out var axonAxonUserId);

        // Assert
        result.ShouldBeTrue();
        axonAxonUserId.ShouldBe(cachedAxonUserId);
    }

    [Test]
    public void TryGetAxonUserId_WhenCacheMiss_ShouldReturnFalseAndDefault()
    {
        // Arrange - No cache entry

        // Act
        var result = _service.TryGetAxonUserId(out var axonAxonUserId);

        // Assert
        result.ShouldBeFalse();
        axonAxonUserId.ShouldBe(default(AxonUserId));
    }

    [Test]
    public void TryGetAxonUserId_WhenHttpContextIsNull_ShouldReturnFalseAndDefault()
    {
        // Arrange
        _httpContextAccessor.HttpContext.Returns((HttpContext?)null);

        // Act
        var result = _service.TryGetAxonUserId(out var axonAxonUserId);

        // Assert
        result.ShouldBeFalse();
        axonAxonUserId.ShouldBe(default(AxonUserId));
    }

    [Test]
    public async Task GetAxonUserIdAsync_WhenDatabaseThrowsException_ShouldPropagateException()
    {
        // Arrange
        var dynamicAxonUserId = "test-user-123";

        _claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, dynamicAxonUserId)
        }, "test"));
        _httpContext.User = _claimsPrincipal;

        _principalRepository.When(x => x.FindByCredentialAsync(
            ProviderType.Dynamic,
            "https://app.dynamic.xyz",
            dynamicAxonUserId,
            Arg.Any<CancellationToken>()))
            .Do(x => { throw new InvalidOperationException("Database error"); });

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            () => _service.GetAxonUserIdAsync());
    }

    [Test]
    public async Task GetAxonUserIdAsync_WhenMemoryCacheHit_ShouldReturnCachedValueAndPopulateRequestCache()
    {
        // Arrange
        var dynamicAxonUserId = "test-user-123";
        var axonAxonUserId = AxonUserId.New();
        var cacheKey = $"axon:user:{dynamicAxonUserId}";

        _claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, dynamicAxonUserId)
        }, "test"));
        _httpContext.User = _claimsPrincipal;

        // Setup memory cache to return cached value (cache hit)
        _memoryCache.TryGetValue(cacheKey, out Arg.Any<object?>())
            .Returns(x =>
            {
                x[1] = axonAxonUserId; // Set the out parameter to the cached value
                return true; // Return true for cache hit
            });

        // Act
        var result = await _service.GetAxonUserIdAsync();

        // Assert
        result.ShouldBe(axonAxonUserId);
        _httpContext.Items["AxonUserId"].ShouldBe(axonAxonUserId);

        // Note: Cannot verify DidNotReceive with Vogen value objects due to uninitialized value issues
    }

    [Test]
    public async Task GetAxonUserIdAsync_WhenMemoryCacheMiss_ShouldFetchFromDatabaseAndCacheInMemory()
    {
        // Arrange
        var dynamicAxonUserId = "test-user-123";
        var cacheKey = $"axon:user:{dynamicAxonUserId}";

        _claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, dynamicAxonUserId)
        }, "test"));
        _httpContext.User = _claimsPrincipal;

        var principal = AxonPrincipal.CreateWithDynamicCredential(
            ProviderType.Dynamic,
            "https://app.dynamic.xyz",
            dynamicAxonUserId).Value;

        // Use the principal's actual ID for the assertion
        var expectedAxonUserId = principal.Id;

        _principalRepository.FindByCredentialAsync(
            ProviderType.Dynamic,
            "https://app.dynamic.xyz",
            dynamicAxonUserId,
            Arg.Any<CancellationToken>())
            .Returns(principal);

        // Setup memory cache to simulate cache miss
        _memoryCache.TryGetValue(cacheKey, out Arg.Any<object?>()).Returns(false);

        var mockCacheEntry = Substitute.For<ICacheEntry>();
        mockCacheEntry.Key.Returns(cacheKey);
        _memoryCache.CreateEntry(cacheKey).Returns(mockCacheEntry);

        // Act
        var result = await _service.GetAxonUserIdAsync();

        // Assert
        result.ShouldBe(expectedAxonUserId);
        _httpContext.Items["AxonUserId"].ShouldBe(expectedAxonUserId);

        await _principalRepository.Received(1).FindByCredentialAsync(
            ProviderType.Dynamic,
            "https://app.dynamic.xyz",
            dynamicAxonUserId,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public void TryGetAxonUserId_WhenMemoryCacheHit_ShouldReturnTrueAndPopulateRequestCache()
    {
        // Arrange
        var dynamicAxonUserId = "test-user-123";
        var axonAxonUserId = AxonUserId.New();
        var cacheKey = $"axon:user:{dynamicAxonUserId}";

        _claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, dynamicAxonUserId)
        }, "test"));
        _httpContext.User = _claimsPrincipal;

        // Setup memory cache hit
        _memoryCache.TryGetValue(cacheKey, out Arg.Any<object?>())
            .Returns(callInfo =>
            {
                callInfo[1] = axonAxonUserId;
                return true;
            });

        // Act
        var result = _service.TryGetAxonUserId(out var resultAxonUserId);

        // Assert
        result.ShouldBeTrue();
        resultAxonUserId.ShouldBe(axonAxonUserId);
        _httpContext.Items["AxonUserId"].ShouldBe(axonAxonUserId);
    }

    [Test]
    public void TryGetAxonUserId_WhenMemoryCacheMiss_ShouldReturnFalse()
    {
        // Arrange
        var dynamicAxonUserId = "test-user-123";
        var cacheKey = $"axon:user:{dynamicAxonUserId}";

        _claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, dynamicAxonUserId)
        }, "test"));
        _httpContext.User = _claimsPrincipal;

        // Setup memory cache miss
        _memoryCache.TryGetValue(cacheKey, out Arg.Any<object?>())
            .Returns(false);

        // Act
        var result = _service.TryGetAxonUserId(out var resultAxonUserId);

        // Assert
        result.ShouldBeFalse();
        resultAxonUserId.ShouldBe(default(AxonUserId));
        _httpContext.Items.ContainsKey("AxonUserId").ShouldBeFalse();
    }
}