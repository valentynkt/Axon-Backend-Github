using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using NUnit.Framework;
using Shouldly;
using Axon.Modules.Identity.Infrastructure.ExternalServices;
using Axon.Modules.Identity.Infrastructure.ExternalServices.Configuration;
using BuildingBlocks.Core.Diagnostics.Errors;

namespace Axon.Modules.Identity.Infrastructure.Tests.ExternalServices;

[TestFixture]
public class SimpleJwksServiceTests
{
    private JwksService _jwksService;
    private HttpClient _httpClient;
    private TestHttpMessageHandler _httpMessageHandler;
    private MemoryCache _memoryCache;
    private ILogger<JwksService> _logger;
    private DynamicXyzOptions _options;

    [SetUp]
    public void SetUp()
    {
        _httpMessageHandler = new TestHttpMessageHandler();
        _httpClient = new HttpClient(_httpMessageHandler);
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _logger = Substitute.For<ILogger<JwksService>>();
        
        _options = new DynamicXyzOptions
        {
            BaseUrl = "https://test.dynamicauth.com",
            ApiToken = "test-token",
            EnvironmentId = "test-env",
            Jwt = new JwtValidationOptions
            {
                JwksCacheMinutes = 10
            }
        };
        
        var optionsWrapper = Options.Create(_options);
        _jwksService = new JwksService(_httpClient, _memoryCache, _logger, optionsWrapper);
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient.Dispose();
        _httpMessageHandler.Dispose();
        _memoryCache.Dispose();
    }

    [Test]
    public async Task GetJwksKeysAsync_WhenCacheHit_ShouldReturnCachedKeys()
    {
        // Arrange
        using var rsa = RSA.Create();
        var cachedKeys = new List<SecurityKey> { new RsaSecurityKey(rsa) };
        _memoryCache.Set("dynamic_jwks_keys", (ICollection<SecurityKey>)cachedKeys);

        // Act
        var result = await _jwksService.GetJwksKeysAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(cachedKeys);
        _httpMessageHandler.RequestCount.ShouldBe(0); // No HTTP request should be made
    }

    [Test]
    public async Task GetJwksKeysAsync_WhenSuccessfulFetch_ShouldReturnKeys()
    {
        // Arrange
        var jwksResponse = CreateValidJwksResponse();
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, jwksResponse);

        // Act
        var result = await _jwksService.GetJwksKeysAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(1);
        _httpMessageHandler.RequestCount.ShouldBe(1);
        
        // Verify key is cached
        var cachedKeys = _memoryCache.Get<ICollection<SecurityKey>>("dynamic_jwks_keys");
        cachedKeys.ShouldNotBeNull();
        cachedKeys.Count.ShouldBe(1);
    }

    [Test]
    public async Task GetJwksKeysAsync_WhenEmptyKeysArray_ShouldReturnNoJwksKeysError()
    {
        // Arrange
        var emptyJwksResponse = """
            {
                "keys": []
            }
            """;
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, emptyJwksResponse);

        // Act
        var result = await _jwksService.GetJwksKeysAsync();

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("AUTH.NO_JWKS_KEYS");
        result.Error.Message.ShouldBe("No keys found in JWKS response");
    }

    [Test]
    public async Task GetJwksKeysAsync_WhenHttpRequestException_ShouldReturnJwksFetchError()
    {
        // Arrange
        _httpMessageHandler.SetException(new HttpRequestException("Network error"));

        // Act
        var result = await _jwksService.GetJwksKeysAsync();

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("AUTH.JWKS_FETCH_ERROR");
        result.Error.Message.ShouldBe("Failed to fetch JWKS keys due to HTTP error");
    }

    [Test]
    public async Task GetJwksKeysAsync_WhenInvalidJson_ShouldReturnJwksInvalidJsonError()
    {
        // Arrange
        var invalidJsonResponse = "{ invalid json";
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, invalidJsonResponse);

        // Act
        var result = await _jwksService.GetJwksKeysAsync();

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("AUTH.JWKS_INVALID_JSON");
        result.Error.Message.ShouldBe("Invalid JSON in JWKS response");
    }

    private static string CreateValidJwksResponse()
    {
        return """
            {
                "keys": [
                    {
                        "kty": "RSA",
                        "use": "sig",
                        "kid": "test-key-id",
                        "n": "0vx7agoebGcQSuuPiLJXZptN9nndrQmbPFRP_gdHPfQUhocKVhd3dKNLRAF6G8V5PQ4i1vAKiPd04QdJaByxQTh9q4Mv4WBFJzT-Vj6AVWy8bSPOG5AUWAf9P-z7-iQKQ7Hn7U_P4MtUlTzWR5YlTJ0aGhfpj-YE2l2C_hN4gBk",
                        "e": "AQAB",
                        "alg": "RS256"
                    }
                ]
            }
            """;
    }
}

/// <summary>
/// Test HTTP message handler for simulating HTTP responses and exceptions
/// </summary>
public class TestHttpMessageHandler : HttpMessageHandler
{
    private HttpStatusCode _statusCode = HttpStatusCode.OK;
    private string _content = "";
    private Exception? _exception;
    
    public int RequestCount { get; private set; }

    public void SetResponse(HttpStatusCode statusCode, string content)
    {
        _statusCode = statusCode;
        _content = content;
        _exception = null;
    }

    public void SetException(Exception exception)
    {
        _exception = exception;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        
        if (_exception != null)
        {
            throw _exception;
        }

        // Default single response
        return Task.FromResult(new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_content)
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Cleanup if needed
        }
        base.Dispose(disposing);
    }
}