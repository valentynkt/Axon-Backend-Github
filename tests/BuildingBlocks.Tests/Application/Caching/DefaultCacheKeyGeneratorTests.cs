using System;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using Xunit;
using FluentAssertions;
using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Application.Caching;

namespace BuildingBlocks.Tests.Application.Caching;

public sealed class DefaultCacheKeyGeneratorTests
{
    private readonly DefaultCacheKeyGenerator _generator;
    private readonly CacheOptions _options;

    public DefaultCacheKeyGeneratorTests()
    {
        _options = new CacheOptions
        {
            IncludeTraceInKey = false
        };
        _generator = new DefaultCacheKeyGenerator(Options.Create(_options));
    }

    [Fact]
    public void GenerateKey_WhenIQueryWithoutTraceContext_ShouldGenerateGlobalKey()
    {
        // Arrange
        var query = new TestQuery { Value = "test-value" };

        // Act
        var key1 = _generator.GenerateKey(query);
        var key2 = _generator.GenerateKey(query);

        // Assert
        key1.Should().Be(key2); // Deterministic
        key1.Should().StartWith("axon:query:TestQuery:");
        key1.Should().EndWith(":global");
    }

    [Fact]
    public void GenerateKey_WhenIQueryWithTraceContextEnabled_ShouldIncludeTraceId()
    {
        // Arrange
        var optionsWithTrace = new CacheOptions { IncludeTraceInKey = true };
        var generatorWithTrace = new DefaultCacheKeyGenerator(Options.Create(optionsWithTrace));
        var query = new TestQueryWithTrace 
        { 
            Value = "test-value",
            TestTraceId = "abcd1234567890ef"
        };

        // Act
        var key = generatorWithTrace.GenerateKey(query);

        // Assert
        key.Should().StartWith("axon:query:TestQueryWithTrace:");
        key.Should().EndWith(":abcd1234"); // First 8 chars of trace ID
    }

    [Fact]
    public void GenerateKey_WhenSameQueryContent_ShouldGenerateSameKey()
    {
        // Arrange
        var query1 = new TestQuery { Value = "identical" };
        var query2 = new TestQuery { Value = "identical" };

        // Act
        var key1 = _generator.GenerateKey(query1);
        var key2 = _generator.GenerateKey(query2);

        // Assert
        key1.Should().Be(key2);
    }

    [Fact]
    public void GenerateKey_WhenDifferentQueryContent_ShouldGenerateDifferentKeys()
    {
        // Arrange
        var query1 = new TestQuery { Value = "value1" };
        var query2 = new TestQuery { Value = "value2" };

        // Act
        var key1 = _generator.GenerateKey(query1);
        var key2 = _generator.GenerateKey(query2);

        // Assert
        key1.Should().NotBe(key2);
    }

    [Fact]
    public void GenerateKey_WhenNonIQueryType_ShouldUseFallbackFormat()
    {
        // Arrange
        var request = new NonQueryRequest { Data = "test" };

        // Act
        var key = _generator.GenerateKey(request);

        // Assert
        key.Should().StartWith("axon:cache:NonQueryRequest:v1:");
    }

    [Fact]
    public void GenerateKey_WhenQueryWithCustomPrefix_ShouldUseCustomPrefix()
    {
        // Arrange
        var query = new TestQueryWithCustomPrefix { Value = "test" };

        // Act
        var key = _generator.GenerateKey(query);

        // Assert
        key.Should().StartWith("axon:query:CustomTestPrefix:");
    }

    [Fact]
    public void GenerateKey_WhenNonSerializableContent_ShouldUseFallback()
    {
        // Arrange
        var query = new NonSerializableQuery();

        // Act
        var key = _generator.GenerateKey(query);

        // Assert
        key.Should().NotBeNullOrEmpty();
        key.Should().StartWith("axon:cache:NonSerializableQuery:v1:");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("short")]
    [InlineData("abcdef1234567890")]
    public void GenerateKey_WithVariousTraceIds_ShouldHandleCorrectly(string? traceId)
    {
        // Arrange
        var optionsWithTrace = new CacheOptions { IncludeTraceInKey = true };
        var generatorWithTrace = new DefaultCacheKeyGenerator(Options.Create(optionsWithTrace));
        var query = new TestQueryWithTrace 
        { 
            Value = "test",
            TestTraceId = traceId
        };

        // Act
        var key = generatorWithTrace.GenerateKey(query);

        // Assert
        key.Should().NotBeNullOrEmpty();
        
        if (string.IsNullOrEmpty(traceId))
        {
            key.Should().EndWith(":global");
        }
        else if (traceId.Length >= 8)
        {
            key.Should().EndWith($":{traceId[..8]}");
        }
        else
        {
            key.Should().EndWith($":{traceId}");
        }
    }

    // Test classes
    private sealed record TestQuery : IQuery<string>
    {
        public string Value { get; init; } = string.Empty;
        public bool UseCache => true;
        public TimeSpan? CacheDuration => null;
        public string CacheKeyPrefix => "TestQuery";
        
        public Guid RequestId { get; } = Guid.NewGuid();
        public DateTime RequestedAt { get; } = DateTime.UtcNow;
        public IReadOnlyDictionary<string, object> Metadata { get; } = 
            new Dictionary<string, object>();
    }

    private sealed record TestQueryWithTrace : IQuery<string>
    {
        public string Value { get; init; } = string.Empty;
        public string? TestTraceId { get; init; }
        public bool UseCache => true;
        public TimeSpan? CacheDuration => null;
        public string CacheKeyPrefix => "TestQueryWithTrace";
        
        public Guid RequestId { get; } = Guid.NewGuid();
        public DateTime RequestedAt { get; } = DateTime.UtcNow;
        public string? TraceId => TestTraceId;
        public IReadOnlyDictionary<string, object> Metadata { get; } = 
            new Dictionary<string, object>();
    }

    private sealed record TestQueryWithCustomPrefix : IQuery<string>
    {
        public string Value { get; init; } = string.Empty;
        public bool UseCache => true;
        public TimeSpan? CacheDuration => null;
        public string CacheKeyPrefix => "CustomTestPrefix";
        
        public Guid RequestId { get; } = Guid.NewGuid();
        public DateTime RequestedAt { get; } = DateTime.UtcNow;
        public IReadOnlyDictionary<string, object> Metadata { get; } = 
            new Dictionary<string, object>();
    }

    private sealed class NonQueryRequest
    {
        public string Data { get; init; } = string.Empty;
    }

    private sealed class NonSerializableQuery
    {
        public Func<int, string> NonSerializableFunc { get; } = x => x.ToString();
    }
}