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

    [Test]
    public async Task GetAxonUserIdAsync_WhenCacheHit_ShouldReturnCachedValue()
    {
        // Arrange
        var cachedUserId = AxonUserId.New();
        _httpContext.Items["AxonUserId"] = cachedUserId;

        // Act
        var result = await _service.GetAxonUserIdAsync();

        // Assert
        result.ShouldBe(cachedUserId);
        await _principalRepository.DidNotReceive().FindByCredentialAsync(
            Arg.Any<ProviderType>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
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
        await _principalRepository.DidNotReceive().FindByCredentialAsync(
            Arg.Any<ProviderType>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetAxonUserIdAsync_WhenCacheMissAndPrincipalFound_ShouldFetchFromDatabaseAndCache()
    {
        // Arrange
        var dynamicUserId = "test-user-123";
        var axonUserId = AxonUserId.New();
        
        _claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, dynamicUserId)
        }, "test"));
        _httpContext.User = _claimsPrincipal;

        var principal = Substitute.For<AxonPrincipal>();
        principal.Id.Returns(axonUserId);

        _principalRepository.FindByCredentialAsync(
            ProviderType.Dynamic,
            "https://app.dynamic.xyz",
            dynamicUserId,
            Arg.Any<CancellationToken>())
            .Returns(principal);

        // Act
        var result = await _service.GetAxonUserIdAsync();

        // Assert
        result.ShouldBe(axonUserId);
        _httpContext.Items["AxonUserId"].ShouldBe(axonUserId);
        
        await _principalRepository.Received(1).FindByCredentialAsync(
            ProviderType.Dynamic,
            "https://app.dynamic.xyz",
            dynamicUserId,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetAxonUserIdAsync_WhenCacheMissAndPrincipalNotFound_ShouldReturnNull()
    {
        // Arrange
        var dynamicUserId = "test-user-123";
        
        _claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, dynamicUserId)
        }, "test"));
        _httpContext.User = _claimsPrincipal;

        _principalRepository.FindByCredentialAsync(
            ProviderType.Dynamic,
            "https://app.dynamic.xyz",
            dynamicUserId,
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
            dynamicUserId,
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
        
        // No database call should be made since we can't get the user ID
        await _principalRepository.DidNotReceive().FindByCredentialAsync(
            Arg.Any<ProviderType>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetAxonUserIdAsync_OnSecondCall_ShouldUseCacheAndNotCallDatabase()
    {
        // Arrange
        var dynamicUserId = "test-user-123";
        var axonUserId = AxonUserId.New();

        _claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, dynamicUserId)
        }, "test"));
        _httpContext.User = _claimsPrincipal;

        var principal = Substitute.For<AxonPrincipal>();
        principal.Id.Returns(axonUserId);

        _principalRepository.FindByCredentialAsync(
            ProviderType.Dynamic,
            "https://app.dynamic.xyz",
            dynamicUserId,
            Arg.Any<CancellationToken>())
            .Returns(principal);

        // Act - First call
        var result1 = await _service.GetAxonUserIdAsync();

        // Act - Second call
        var result2 = await _service.GetAxonUserIdAsync();

        // Assert
        result1.ShouldBe(axonUserId);
        result2.ShouldBe(axonUserId);

        // Database should only be called once
        await _principalRepository.Received(1).FindByCredentialAsync(
            ProviderType.Dynamic,
            "https://app.dynamic.xyz",
            dynamicUserId,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public void TryGetAxonUserId_WhenCacheHit_ShouldReturnTrueAndValue()
    {
        // Arrange
        var cachedUserId = AxonUserId.New();
        _httpContext.Items["AxonUserId"] = cachedUserId;

        // Act
        var result = _service.TryGetAxonUserId(out var axonUserId);

        // Assert
        result.ShouldBeTrue();
        axonUserId.ShouldBe(cachedUserId);
    }

    [Test]
    public void TryGetAxonUserId_WhenCacheMiss_ShouldReturnFalseAndDefault()
    {
        // Arrange - No cache entry

        // Act
        var result = _service.TryGetAxonUserId(out var axonUserId);

        // Assert
        result.ShouldBeFalse();
        axonUserId.ShouldBe(default(AxonUserId));
    }

    [Test]
    public void TryGetAxonUserId_WhenHttpContextIsNull_ShouldReturnFalseAndDefault()
    {
        // Arrange
        _httpContextAccessor.HttpContext.Returns((HttpContext?)null);

        // Act
        var result = _service.TryGetAxonUserId(out var axonUserId);

        // Assert
        result.ShouldBeFalse();
        axonUserId.ShouldBe(default(AxonUserId));
    }

    [Test]
    public async Task GetAxonUserIdAsync_WhenDatabaseThrowsException_ShouldPropagateException()
    {
        // Arrange
        var dynamicUserId = "test-user-123";

        _claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, dynamicUserId)
        }, "test"));
        _httpContext.User = _claimsPrincipal;

        _principalRepository.When(x => x.FindByCredentialAsync(
            ProviderType.Dynamic,
            "https://app.dynamic.xyz",
            dynamicUserId,
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
        var dynamicUserId = "test-user-123";
        var axonUserId = AxonUserId.New();
        var cacheKey = $"axon:user:{dynamicUserId}";

        _claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, dynamicUserId)
        }, "test"));
        _httpContext.User = _claimsPrincipal;

        // Setup memory cache to return cached value
        _memoryCache.GetOrCreateAsync(cacheKey, Arg.Any<Func<ICacheEntry, Task<AxonUserId?>>>())
            .Returns(axonUserId);

        // Act
        var result = await _service.GetAxonUserIdAsync();

        // Assert
        result.ShouldBe(axonUserId);
        _httpContext.Items["AxonUserId"].ShouldBe(axonUserId);

        // Database should not be called on memory cache hit
        await _principalRepository.DidNotReceive().FindByCredentialAsync(
            Arg.Any<ProviderType>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetAxonUserIdAsync_WhenMemoryCacheMiss_ShouldFetchFromDatabaseAndCacheInMemory()
    {
        // Arrange
        var dynamicUserId = "test-user-123";
        var axonUserId = AxonUserId.New();
        var cacheKey = $"axon:user:{dynamicUserId}";

        _claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, dynamicUserId)
        }, "test"));
        _httpContext.User = _claimsPrincipal;

        var principal = Substitute.For<AxonPrincipal>();
        principal.Id.Returns(axonUserId);

        _principalRepository.FindByCredentialAsync(
            ProviderType.Dynamic,
            "https://app.dynamic.xyz",
            dynamicUserId,
            Arg.Any<CancellationToken>())
            .Returns(principal);

        // Setup memory cache to execute factory function (cache miss)
        _memoryCache.GetOrCreateAsync(cacheKey, Arg.Any<Func<ICacheEntry, Task<AxonUserId?>>>())
            .Returns(async callInfo =>
            {
                var factory = callInfo.Arg<Func<ICacheEntry, Task<AxonUserId?>>>();
                var entry = Substitute.For<ICacheEntry>();
                return await factory(entry);
            });

        // Act
        var result = await _service.GetAxonUserIdAsync();

        // Assert
        result.ShouldBe(axonUserId);
        _httpContext.Items["AxonUserId"].ShouldBe(axonUserId);

        await _principalRepository.Received(1).FindByCredentialAsync(
            ProviderType.Dynamic,
            "https://app.dynamic.xyz",
            dynamicUserId,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public void TryGetAxonUserId_WhenMemoryCacheHit_ShouldReturnTrueAndPopulateRequestCache()
    {
        // Arrange
        var dynamicUserId = "test-user-123";
        var axonUserId = AxonUserId.New();
        var cacheKey = $"axon:user:{dynamicUserId}";

        _claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, dynamicUserId)
        }, "test"));
        _httpContext.User = _claimsPrincipal;

        // Setup memory cache hit
        _memoryCache.TryGetValue(cacheKey, out Arg.Any<object?>())
            .Returns(callInfo =>
            {
                callInfo[1] = axonUserId;
                return true;
            });

        // Act
        var result = _service.TryGetAxonUserId(out var resultUserId);

        // Assert
        result.ShouldBeTrue();
        resultUserId.ShouldBe(axonUserId);
        _httpContext.Items["AxonUserId"].ShouldBe(axonUserId);
    }

    [Test]
    public void TryGetAxonUserId_WhenMemoryCacheMiss_ShouldReturnFalse()
    {
        // Arrange
        var dynamicUserId = "test-user-123";
        var cacheKey = $"axon:user:{dynamicUserId}";

        _claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, dynamicUserId)
        }, "test"));
        _httpContext.User = _claimsPrincipal;

        // Setup memory cache miss
        _memoryCache.TryGetValue(cacheKey, out Arg.Any<object?>())
            .Returns(false);

        // Act
        var result = _service.TryGetAxonUserId(out var resultUserId);

        // Assert
        result.ShouldBeFalse();
        resultUserId.ShouldBe(default(AxonUserId));
        _httpContext.Items.ContainsKey("AxonUserId").ShouldBeFalse();
    }
}