using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using NUnit.Framework;
using Shouldly;
using Axon.Modules.Identity.Application.Contracts.ExternalServices;
using Axon.Modules.Identity.Application.Services;
using Axon.Modules.Identity.Infrastructure.ExternalServices;
using Axon.Modules.Identity.Infrastructure.ExternalServices.Configuration;
using Axon.Modules.Identity.Infrastructure.Services;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Infrastructure.Tests.ExternalServices;

[TestFixture]
public class SimpleDynamicAuthServiceTests
{
    private DynamicAuthService _dynamicAuthService;
    private MemoryCache _memoryCache;
    private ILogger<DynamicAuthService> _logger;
    private DynamicXyzOptions _options;
    private IDynamicClaimNormalizer _claimNormalizer;
    private IJwtReplayGuard _replayGuard;
    private IJwksService _jwksService;
    private ICollection<SecurityKey> _testKeys;

    [SetUp]
    public void SetUp()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _logger = Substitute.For<ILogger<DynamicAuthService>>();
        _claimNormalizer = Substitute.For<IDynamicClaimNormalizer>();
        _replayGuard = Substitute.For<IJwtReplayGuard>();
        _jwksService = Substitute.For<IJwksService>();
        
        _options = new DynamicXyzOptions
        {
            EnvironmentId = "test-env-id",
            Jwt = new JwtValidationOptions
            {
                ClockSkewMinutes = 5
            }
        };
        
        var optionsWrapper = Options.Create(_options);
        
        // Create test RSA key for JWT validation
        using var rsa = RSA.Create();
        var testKey = new RsaSecurityKey(rsa) { KeyId = "test-key-id" };
        _testKeys = new List<SecurityKey> { testKey };
        
        _dynamicAuthService = new DynamicAuthService(
            _memoryCache,
            _logger,
            optionsWrapper,
            _claimNormalizer,
            _replayGuard,
            _jwksService);
    }

    [TearDown]
    public void TearDown()
    {
        _memoryCache.Dispose();
    }

    [Test]
    public async Task ValidateTokenAsync_WhenTokenIsEmpty_ShouldReturnTokenRequiredError()
    {
        // Act
        var result = await _dynamicAuthService.ValidateTokenAsync("");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("AUTH.TOKEN_REQUIRED");
        result.Error.Message.ShouldBe("Token is required");
    }

    [Test]
    public async Task ValidateTokenAsync_WhenTokenIsNull_ShouldReturnTokenRequiredError()
    {
        // Act
        var result = await _dynamicAuthService.ValidateTokenAsync(null!);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("AUTH.TOKEN_REQUIRED");
        result.Error.Message.ShouldBe("Token is required");
    }

    [Test]
    public async Task ValidateTokenAsync_WhenCacheHit_ShouldReturnCachedUserData()
    {
        // Arrange
        var token = "test.jwt.token";
        var cachedUserData = new DynamicUserData(
            UserId: "test-user-id",
            Email: "test@example.com", 
            EnvironmentId: "test-env",
            Wallets: new List<WalletData>(),
            FirstVisitUtc: null,
            LastVisitUtc: null,
            IsNewUser: false
        );
        
        var cacheKey = $"dynamic_token_{GetTokenHash(token)}";
        _memoryCache.Set(cacheKey, cachedUserData);

        // Act
        var result = await _dynamicAuthService.ValidateTokenAsync(token);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(cachedUserData);
        
        // Verify no JWKS service call was made
        await _jwksService.DidNotReceive().GetJwksKeysAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ValidateTokenAsync_WhenJwksServiceFails_ShouldReturnJwksError()
    {
        // Arrange
        var token = "test.jwt.token";
        var jwksError = Error.External("JWKS fetch failed", "AUTH.JWKS_FETCH_ERROR");
        
        _jwksService.GetJwksKeysAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Failure<ICollection<SecurityKey>, Error>(jwksError));

        // Act
        var result = await _dynamicAuthService.ValidateTokenAsync(token);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("AUTH.JWKS_FETCH_ERROR");
        result.Error.Message.ShouldBe("JWKS fetch failed");
    }

    [Test]
    public async Task ValidateTokenAsync_WhenTokenHasInvalidFormat_ShouldReturnInvalidTokenFormatError()
    {
        // Arrange
        var invalidToken = "invalid.token.format";
        
        _jwksService.GetJwksKeysAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success<ICollection<SecurityKey>, Error>(_testKeys));

        // Act
        var result = await _dynamicAuthService.ValidateTokenAsync(invalidToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("AUTH.INVALID_TOKEN_FORMAT");
        result.Error.Message.ShouldBe("Invalid JWT token format");
    }

    private static string GetTokenHash(string token)
    {
        // Mirror the implementation in DynamicAuthService
        return token.Length > 8 ? token[^8..] : token;
    }
}