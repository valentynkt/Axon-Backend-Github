using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;
using FluentAssertions;
using MediatR;
using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Application.Caching;
using BuildingBlocks.Infrastructure.Caching;

namespace BuildingBlocks.Tests.Application.Behaviors;

public sealed class CachingBehaviorTests
{
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache _distributedCache;
    private readonly ICacheKeyGenerator _keyGenerator;
    private readonly ILogger<CachingBehavior<TestQuery, Result<string>>> _logger;
    private readonly IOptions<CacheOptions> _options;
    private readonly CachingBehavior<TestQuery, Result<string>> _behavior;
    private readonly RequestHandlerDelegate<Result<string>> _next;

    public CachingBehaviorTests()
    {
        _memoryCache = Substitute.For<IMemoryCache>();
        _distributedCache = Substitute.For<IDistributedCache>();
        _keyGenerator = Substitute.For<ICacheKeyGenerator>();
        _logger = Substitute.For<ILogger<CachingBehavior<TestQuery, Result<string>>>>();
        
        var cacheOptions = new CacheOptions
        {
            DefaultDuration = TimeSpan.FromMinutes(5),
            IncludeTraceInKey = false
        };
        _options = Options.Create(cacheOptions);
        
        _behavior = new CachingBehavior<TestQuery, Result<string>>(
            _memoryCache,
            _distributedCache,
            _keyGenerator,
            _logger,
            _options);
            
        _next = Substitute.For<RequestHandlerDelegate<Result<string>>>();
    }

    [Fact]
    public async Task Handle_WhenCachingDisabled_ShouldBypassCache()
    {
        // Arrange
        var query = new TestQuery { UseCache = false, Value = "test" };
        var expectedResult = Result<string>.Success("response");
        _next().Returns(expectedResult);

        // Act
        var result = await _behavior.Handle(query, _next, CancellationToken.None);

        // Assert
        result.Should().Be(expectedResult);
        await _next.Received(1)();
        _keyGenerator.DidNotReceive().GenerateKey(Arg.Any<TestQuery>());
    }

    [Fact]
    public async Task Handle_WhenMemoryCacheHit_ShouldReturnCachedResult()
    {
        // Arrange
        var query = new TestQuery { UseCache = true, Value = "test" };
        var cachedResult = Result<string>.Success("cached");
        var cacheKey = "test-key";

        _keyGenerator.GenerateKey(query).Returns(cacheKey);
        _memoryCache.TryGetValue(cacheKey, out Arg.Any<object>())
            .Returns(x =>
            {
                x[1] = cachedResult;
                return true;
            });

        // Act
        var result = await _behavior.Handle(query, _next, CancellationToken.None);

        // Assert
        result.Should().Be(cachedResult);
        await _next.DidNotReceive()();
    }

    [Fact]
    public async Task Handle_WhenDistributedCacheHit_ShouldReturnCachedResultAndPopulateMemoryCache()
    {
        // Arrange
        var query = new TestQuery { UseCache = true, Value = "test" };
        var cachedResult = Result<string>.Success("cached");
        var cacheKey = "test-key";
        var serializedResult = JsonSerializer.SerializeToUtf8Bytes(cachedResult);

        _keyGenerator.GenerateKey(query).Returns(cacheKey);
        _memoryCache.TryGetValue(cacheKey, out Arg.Any<object>()).Returns(false);
        _distributedCache.GetAsync(cacheKey, Arg.Any<CancellationToken>())
            .Returns(serializedResult);

        // Act
        var result = await _behavior.Handle(query, _next, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("cached");
        await _next.DidNotReceive()();
        _memoryCache.Received(1).Set(cacheKey, Arg.Any<Result<string>>(), Arg.Any<MemoryCacheEntryOptions>());
    }

    [Fact]
    public async Task Handle_WhenCacheMiss_ShouldExecuteHandlerAndCacheSuccessfulResult()
    {
        // Arrange
        var query = new TestQuery { UseCache = true, Value = "test" };
        var handlerResult = Result<string>.Success("handler-response");
        var cacheKey = "test-key";

        _keyGenerator.GenerateKey(query).Returns(cacheKey);
        _memoryCache.TryGetValue(cacheKey, out Arg.Any<object>()).Returns(false);
        _distributedCache.GetAsync(cacheKey, Arg.Any<CancellationToken>()).Returns((byte[]?)null);
        _next().Returns(handlerResult);

        // Act
        var result = await _behavior.Handle(query, _next, CancellationToken.None);

        // Assert
        result.Should().Be(handlerResult);
        await _next.Received(1)();
        
        // Give some time for async caching to complete
        await Task.Delay(100);
        _memoryCache.Received().Set(cacheKey, handlerResult, Arg.Any<MemoryCacheEntryOptions>());
        await _distributedCache.Received().SetAsync(cacheKey, Arg.Any<byte[]>(), 
            Arg.Any<DistributedCacheEntryOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenHandlerReturnsFailure_ShouldNotCache()
    {
        // Arrange
        var query = new TestQuery { UseCache = true, Value = "test" };
        var failureResult = Result<string>.Failure(Error.Validation("Test error"));
        var cacheKey = "test-key";

        _keyGenerator.GenerateKey(query).Returns(cacheKey);
        _memoryCache.TryGetValue(cacheKey, out Arg.Any<object>()).Returns(false);
        _distributedCache.GetAsync(cacheKey, Arg.Any<CancellationToken>()).Returns((byte[]?)null);
        _next().Returns(failureResult);

        // Act
        var result = await _behavior.Handle(query, _next, CancellationToken.None);

        // Assert
        result.Should().Be(failureResult);
        await _next.Received(1)();
        
        // Ensure no caching happened
        await Task.Delay(100);
        _memoryCache.DidNotReceive().Set(Arg.Any<object>(), Arg.Any<object>(), Arg.Any<MemoryCacheEntryOptions>());
        await _distributedCache.DidNotReceive().SetAsync(Arg.Any<string>(), Arg.Any<byte[]>(), 
            Arg.Any<DistributedCacheEntryOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenCacheThrowsException_ShouldProceedWithoutCache()
    {
        // Arrange
        var query = new TestQuery { UseCache = true, Value = "test" };
        var handlerResult = Result<string>.Success("handler-response");
        var cacheKey = "test-key";

        _keyGenerator.GenerateKey(query).Returns(cacheKey);
        _memoryCache.TryGetValue(cacheKey, out Arg.Any<object>())
            .Throws(new InvalidOperationException("Cache error"));
        _next().Returns(handlerResult);

        // Act
        var result = await _behavior.Handle(query, _next, CancellationToken.None);

        // Assert
        result.Should().Be(handlerResult);
        await _next.Received(1)();
    }

    [Fact]
    public async Task Handle_WhenCustomCacheDuration_ShouldUseCustomDuration()
    {
        // Arrange
        var customDuration = TimeSpan.FromMinutes(10);
        var query = new TestQuery 
        { 
            UseCache = true, 
            Value = "test",
            CacheDuration = customDuration
        };
        var handlerResult = Result<string>.Success("handler-response");
        var cacheKey = "test-key";

        _keyGenerator.GenerateKey(query).Returns(cacheKey);
        _memoryCache.TryGetValue(cacheKey, out Arg.Any<object>()).Returns(false);
        _distributedCache.GetAsync(cacheKey, Arg.Any<CancellationToken>()).Returns((byte[]?)null);
        _next().Returns(handlerResult);

        // Act
        var result = await _behavior.Handle(query, _next, CancellationToken.None);

        // Assert
        result.Should().Be(handlerResult);
        
        // Give some time for async caching to complete
        await Task.Delay(100);
        await _distributedCache.Received().SetAsync(cacheKey, Arg.Any<byte[]>(), 
            Arg.Is<DistributedCacheEntryOptions>(opts => 
                opts.AbsoluteExpirationRelativeToNow == customDuration), 
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenCorruptedCacheEntry_ShouldRemoveAndProceed()
    {
        // Arrange
        var query = new TestQuery { UseCache = true, Value = "test" };
        var handlerResult = Result<string>.Success("handler-response");
        var cacheKey = "test-key";
        var corruptedBytes = "invalid-json"u8.ToArray();

        _keyGenerator.GenerateKey(query).Returns(cacheKey);
        _memoryCache.TryGetValue(cacheKey, out Arg.Any<object>()).Returns(false);
        _distributedCache.GetAsync(cacheKey, Arg.Any<CancellationToken>())
            .Returns(corruptedBytes);
        _next().Returns(handlerResult);

        // Act
        var result = await _behavior.Handle(query, _next, CancellationToken.None);

        // Assert
        result.Should().Be(handlerResult);
        await _next.Received(1)();
        await _distributedCache.Received().RemoveAsync(cacheKey, Arg.Any<CancellationToken>());
    }

    // Test query for testing
    private sealed record TestQuery : IQuery<string>
    {
        public string Value { get; init; } = string.Empty;
        public bool UseCache { get; init; }
        public TimeSpan? CacheDuration { get; init; }
        public string CacheKeyPrefix => "TestQuery";
        
        // IAxonRequest properties
        public Guid RequestId { get; } = Guid.NewGuid();
        public DateTime RequestedAt { get; } = DateTime.UtcNow;
        public IReadOnlyDictionary<string, object> Metadata { get; } = 
            new Dictionary<string, object>();
    }
}