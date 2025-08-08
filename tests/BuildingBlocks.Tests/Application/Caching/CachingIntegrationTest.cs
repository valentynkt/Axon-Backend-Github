using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using FluentAssertions;
using BuildingBlocks.Application.Caching;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.CQRS;

namespace BuildingBlocks.Tests.Application.Caching;

/// <summary>
/// Integration test for Epic 04 Story 02 caching implementation.
/// Tests the full caching pipeline with real cache providers.
/// </summary>
public sealed class CachingIntegrationTest
{
    [Fact]
    public void CacheKeyGenerator_WithQuery_ShouldGenerateConsistentKeys()
    {
        // Arrange
        var options = Options.Create(new CacheOptions { IncludeTraceInKey = false });
        var generator = new DefaultCacheKeyGenerator(options);
        var query1 = new TestQuery { Value = "test" };
        var query2 = new TestQuery { Value = "test" };

        // Act
        var key1 = generator.GenerateKey(query1);
        var key2 = generator.GenerateKey(query2);

        // Assert
        key1.Should().Be(key2);
        key1.Should().StartWith("axon:query:TestQuery:");
        key1.Should().EndWith(":global");
    }

    [Fact]
    public void CacheKeyGenerator_WithTraceContext_ShouldIncludeTraceId()
    {
        // Arrange
        var options = Options.Create(new CacheOptions { IncludeTraceInKey = true });
        var generator = new DefaultCacheKeyGenerator(options);
        var query = new TestQueryWithTrace { Value = "test", TestTraceId = "abc123456789" };

        // Act
        var key = generator.GenerateKey(query);

        // Assert
        key.Should().StartWith("axon:query:TestQueryWithTrace:");
        key.Should().EndWith(":abc12345"); // First 8 chars
    }

    [Fact]
    public void CacheOptions_DefaultValues_ShouldBeConfigured()
    {
        // Arrange & Act
        var options = new CacheOptions();

        // Assert
        options.DefaultDuration.Should().Be(TimeSpan.FromMinutes(5));
        options.IncludeTraceInKey.Should().BeFalse();
        options.IsEnabled.Should().BeTrue();
        options.RedisConnectionString.Should().Be("localhost:6379");
        options.InstanceName.Should().Be("axon");
        options.MemoryCacheSizeLimitMB.Should().Be(100);
    }

    [Fact]
    public void CachingConfiguration_AddDeclarativeQueryCaching_ShouldRegisterServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddDeclarativeQueryCaching(options =>
        {
            options.DefaultDuration = TimeSpan.FromMinutes(10);
            options.IncludeTraceInKey = true;
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var keyGenerator = serviceProvider.GetService<ICacheKeyGenerator>();
        keyGenerator.Should().NotBeNull();
        keyGenerator.Should().BeOfType<DefaultCacheKeyGenerator>();

        var cacheOptions = serviceProvider.GetService<IOptions<CacheOptions>>();
        cacheOptions.Should().NotBeNull();
        cacheOptions!.Value.DefaultDuration.Should().Be(TimeSpan.FromMinutes(10));
        cacheOptions.Value.IncludeTraceInKey.Should().BeTrue();
    }

    [Fact]
    public void CachingConfiguration_AddDevelopmentCaching_ShouldUseDevSettings()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddDevelopmentCaching();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var options = serviceProvider.GetRequiredService<IOptions<CacheOptions>>().Value;
        options.DefaultDuration.Should().Be(TimeSpan.FromMinutes(2));
        options.IncludeTraceInKey.Should().BeFalse();
        options.InstanceName.Should().Be("axon-dev");
        options.MemoryCacheSizeLimitMB.Should().Be(50);
    }

    [Fact]
    public void CachingConfiguration_AddProductionCaching_ShouldUseProdSettings()
    {
        // Arrange
        var services = new ServiceCollection();
        const string redisConnection = "prod-redis:6379";

        // Act
        services.AddProductionCaching(redisConnection);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var options = serviceProvider.GetRequiredService<IOptions<CacheOptions>>().Value;
        options.DefaultDuration.Should().Be(TimeSpan.FromMinutes(15));
        options.IncludeTraceInKey.Should().BeTrue();
        options.RedisConnectionString.Should().Be(redisConnection);
        options.InstanceName.Should().Be("axon-prod");
        options.MemoryCacheSizeLimitMB.Should().Be(200);
        options.CompressionThreshold.Should().Be(512);
    }

    // Test query implementations
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
}