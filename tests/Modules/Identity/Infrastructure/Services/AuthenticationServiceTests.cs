using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Axon.Modules.Identity.Application.Common;
using Axon.Modules.Identity.Application.Configuration;
using Axon.Modules.Identity.Application.Contracts.ExternalServices;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Application.DTOs.Exchange;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Infrastructure.Services;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Infrastructure.Services.Tests;

/// <summary>
/// Comprehensive tests for AuthenticationService covering all authentication flows,
/// token generation, validation, challenge/response mechanisms, and security features.
/// </summary>
[TestFixture]
public class AuthenticationServiceTests
{
    private AuthenticationService _authService = null!;
    private MemoryCache _memoryCache = null!;
    private IDynamicAuthService _mockDynamicAuthService = null!;
    private ILogger<AuthenticationService> _logger = null!;
    private AuthenticationOptions _options = null!;
    private AxonUserId _testAxonUserId;
    private ProviderType _testProviderType;

    [SetUp]
    public void SetUp()
    {
        // Create test options with valid configuration
        _options = new AuthenticationOptions
        {
            Issuer = "https://api.axon.test",
            Audience = "axon-users",
            DefaultAudience = "axon-default",
            SigningKey = "test-signing-key-32-chars-long!", // Minimum 32 characters
            HmacSecret = "test-hmac-secret-32-chars-long!!", // Minimum 32 characters
            DefaultTokenExpirySeconds = 3600,
            RefreshTokenExpirySeconds = 86400,
            ClockSkewSeconds = 300,
            MaxTtlSeconds = 86400,
            ReplayGuardBufferMinutes = 5
        };

        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _mockDynamicAuthService = Substitute.For<IDynamicAuthService>();
        _logger = Substitute.For<ILogger<AuthenticationService>>();

        _authService = new AuthenticationService(
            Options.Create(_options),
            _memoryCache,
            _mockDynamicAuthService,
            _logger);

        _testAxonUserId = new AxonUserId(Guid.NewGuid());
        _testProviderType = ProviderType.Dynamic;
    }

    [TearDown]
    public void TearDown()
    {
        _memoryCache?.Dispose();
    }

    #region Access Token Generation Tests

    [Test]
    public async Task GenerateAccessTokenAsync_WithValidParameters_ReturnsSuccess()
    {
        // Arrange
        var issuer = "https://app.dynamic.xyz/test";
        var subject = "user-123";
        var expiresIn = 1800; // 30 minutes

        // Act
        var result = await _authService.GenerateAccessTokenAsync(
            _testAxonUserId, _testProviderType, issuer, subject, expiresIn, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessToken.ShouldNotBeNullOrEmpty();
        result.Value.TokenType.ShouldBe("Bearer");
        result.Value.ExpiresIn.ShouldBe(expiresIn);
        result.Value.ExpiresAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
        result.Value.IssuedAt.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow);

        // Verify token structure and claims
        var tokenHandler = new JwtSecurityTokenHandler();
        tokenHandler.CanReadToken(result.Value.AccessToken).ShouldBeTrue();

        var jsonToken = tokenHandler.ReadJwtToken(result.Value.AccessToken);
        jsonToken.Claims.ShouldContain(c => c.Type == "sub" && c.Value == _testAxonUserId.Value.ToString());
        jsonToken.Claims.ShouldContain(c => c.Type == "axon_user_id" && c.Value == _testAxonUserId.Value.ToString());
        jsonToken.Claims.ShouldContain(c => c.Type == "provider_type" && c.Value == _testProviderType.Value);
        jsonToken.Claims.ShouldContain(c => c.Type == "original_issuer" && c.Value == issuer);
        jsonToken.Claims.ShouldContain(c => c.Type == "original_subject" && c.Value == subject);
        jsonToken.Issuer.ShouldBe(_options.Issuer);
        jsonToken.Audiences.ShouldContain(_options.Audience);
    }

    [Test]
    public async Task GenerateAccessTokenAsync_WithZeroExpiresIn_UsesDefaultExpiry()
    {
        // Arrange
        var issuer = "https://app.dynamic.xyz/test";
        var subject = "user-123";
        var expiresIn = 0; // Should use default

        // Act
        var result = await _authService.GenerateAccessTokenAsync(
            _testAxonUserId, _testProviderType, issuer, subject, expiresIn, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ExpiresIn.ShouldBe(_options.DefaultTokenExpirySeconds);
    }

    [Test]
    public async Task GenerateAccessTokenAsync_WithNegativeExpiresIn_UsesDefaultExpiry()
    {
        // Arrange
        var issuer = "https://app.dynamic.xyz/test";
        var subject = "user-123";
        var expiresIn = -100; // Should use default

        // Act
        var result = await _authService.GenerateAccessTokenAsync(
            _testAxonUserId, _testProviderType, issuer, subject, expiresIn, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ExpiresIn.ShouldBe(_options.DefaultTokenExpirySeconds);
    }

    #endregion

    #region Refresh Token Generation Tests

    [Test]
    public async Task GenerateRefreshTokenAsync_WithValidParameters_ReturnsCompleteTokenPair()
    {
        // Arrange
        var issuer = "https://app.dynamic.xyz/test";
        var subject = "user-123";

        // Act
        var result = await _authService.GenerateRefreshTokenAsync(
            _testAxonUserId, _testProviderType, issuer, subject, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessToken.ShouldNotBeNullOrEmpty();
        result.Value.RefreshToken.ShouldNotBeNullOrEmpty();
        result.Value.TokenType.ShouldBe("Bearer");
        result.Value.ExpiresIn.ShouldBe(_options.DefaultTokenExpirySeconds);
        result.Value.AccessTokenExpiresAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
        result.Value.RefreshTokenExpiresAt.ShouldBeGreaterThan(result.Value.AccessTokenExpiresAt);

        // Verify refresh token structure
        var tokenHandler = new JwtSecurityTokenHandler();
        tokenHandler.CanReadToken(result.Value.RefreshToken).ShouldBeTrue();

        var refreshToken = tokenHandler.ReadJwtToken(result.Value.RefreshToken);
        refreshToken.Claims.ShouldContain(c => c.Type == "token_type" && c.Value == "refresh");
        refreshToken.Claims.ShouldContain(c => c.Type == "axon_user_id" && c.Value == _testAxonUserId.Value.ToString());
        refreshToken.Audiences.ShouldContain($"{_options.Audience}:refresh");

        // Verify refresh token is cached
        var jtiClaim = refreshToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
        jtiClaim.ShouldNotBeNullOrEmpty();
        var cacheKey = $"refresh_token_{jtiClaim}";
        _memoryCache.TryGetValue(cacheKey, out var cachedValue).ShouldBeTrue();
        cachedValue.ShouldBe(_testAxonUserId.Value);
    }

    #endregion

    #region Refresh Access Token Tests

    [Test]
    public async Task RefreshAccessTokenAsync_WithValidRefreshToken_ReturnsNewTokenPair()
    {
        // Arrange - Generate initial refresh token
        var issuer = "https://app.dynamic.xyz/test";
        var subject = "user-123";
        var initialResult = await _authService.GenerateRefreshTokenAsync(
            _testAxonUserId, _testProviderType, issuer, subject, CancellationToken.None);
        initialResult.IsSuccess.ShouldBeTrue();

        var refreshToken = initialResult.Value.RefreshToken;

        // Act - Use refresh token to get new access token
        var result = await _authService.RefreshAccessTokenAsync(refreshToken, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessToken.ShouldNotBeNullOrEmpty();
        result.Value.RefreshToken.ShouldNotBeNullOrEmpty();
        result.Value.RefreshToken.ShouldNotBe(refreshToken); // Should be a new refresh token

        // Old refresh token should be invalidated
        var oldRefreshResult = await _authService.RefreshAccessTokenAsync(refreshToken, CancellationToken.None);
        oldRefreshResult.IsFailure.ShouldBeTrue();
        oldRefreshResult.Error.Type.ShouldBe(ErrorType.Unauthorized);
    }

    [Test]
    public async Task RefreshAccessTokenAsync_WithInvalidRefreshToken_ReturnsUnauthorized()
    {
        // Arrange
        var invalidRefreshToken = "invalid.refresh.token";

        // Act
        var result = await _authService.RefreshAccessTokenAsync(invalidRefreshToken, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
        result.Error.Code.ShouldBe(AuthErrors.TokenInvalid);
    }

    [Test]
    public async Task RefreshAccessTokenAsync_WithRevokedRefreshToken_ReturnsUnauthorized()
    {
        // Arrange - Generate and then manually revoke refresh token
        var issuer = "https://app.dynamic.xyz/test";
        var subject = "user-123";
        var tokenResult = await _authService.GenerateRefreshTokenAsync(
            _testAxonUserId, _testProviderType, issuer, subject, CancellationToken.None);
        tokenResult.IsSuccess.ShouldBeTrue();

        var refreshToken = tokenResult.Value.RefreshToken;

        // Extract JTI and remove from cache to simulate revocation
        var tokenHandler = new JwtSecurityTokenHandler();
        var jsonToken = tokenHandler.ReadJwtToken(refreshToken);
        var jti = jsonToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
        var cacheKey = $"refresh_token_{jti}";
        _memoryCache.Remove(cacheKey);

        // Act
        var result = await _authService.RefreshAccessTokenAsync(refreshToken, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
        result.Error.Code.ShouldBe(AuthErrors.TokenInvalid);
    }

    #endregion

    #region Token Validation Tests

    [Test]
    public async Task ValidateTokenAsync_WithValidAxonToken_ReturnsAuthenticatedContext()
    {
        // Arrange - Generate valid Axon token
        var issuer = "https://app.dynamic.xyz/test";
        var subject = "user-123";
        var tokenResult = await _authService.GenerateAccessTokenAsync(
            _testAxonUserId, _testProviderType, issuer, subject, 3600, CancellationToken.None);
        tokenResult.IsSuccess.ShouldBeTrue();

        var token = tokenResult.Value.AccessToken;

        // Act
        var result = await _authService.ValidateTokenAsync(token, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TokenType.ShouldBe(Axon.Modules.Identity.Application.Contracts.Services.TokenType.AxonAccessToken);
        result.Value.AxonUserId.ShouldBe(_testAxonUserId);
        result.Value.ProviderType.ShouldBe(_testProviderType);
        result.Value.Issuer.ShouldBe(issuer);
        result.Value.Subject.ShouldBe(subject);
        result.Value.Principal.ShouldNotBeNull();
        result.Value.ExpiresAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
    }

    [Test]
    public async Task ValidateTokenAsync_WithValidDynamicToken_ReturnsAuthenticatedContext()
    {
        // Arrange
        var dynamicToken = "valid.dynamic.jwt.token";
        var dynamicUserId = _testAxonUserId.Value.ToString();

        var mockDynamicUserData = new DynamicUserData(
            AxonUserId: dynamicUserId,
            Email: "test@example.com",
            EnvironmentId: "test-env",
            Wallets: new List<WalletData>(),
            FirstVisitUtc: DateTimeOffset.UtcNow,
            LastVisitUtc: DateTimeOffset.UtcNow,
            IsNewUser: false);

        var mockClaims = new[]
        {
            new Claim("sub", "dynamic-subject-123"),
            new Claim("iss", "https://app.dynamic.xyz/test"),
            new Claim("aud", "test-audience"),
            new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
            new Claim("exp", DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString())
        };
        var mockPrincipal = new ClaimsPrincipal(new ClaimsIdentity(mockClaims));

        _mockDynamicAuthService.ValidateTokenAsync(dynamicToken, Arg.Any<CancellationToken>())
            .Returns(Result.Success<DynamicUserData, Error>(mockDynamicUserData));

        _mockDynamicAuthService.GetRawClaimsAsync(dynamicToken, Arg.Any<CancellationToken>())
            .Returns(Result.Success<ClaimsPrincipal, Error>(mockPrincipal));

        // Act
        var result = await _authService.ValidateTokenAsync(dynamicToken, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TokenType.ShouldBe(Axon.Modules.Identity.Application.Contracts.Services.TokenType.DynamicJwt);
        result.Value.AxonUserId.ShouldBe(_testAxonUserId);
        result.Value.ProviderType.Value.ShouldBe("dynamic");
        result.Value.Issuer.ShouldBe("https://app.dynamic.xyz/test");
        result.Value.Subject.ShouldBe("dynamic-subject-123");
        result.Value.Principal.ShouldBe(mockPrincipal);
    }

    [Test]
    public async Task ValidateTokenAsync_WithExpiredToken_ReturnsUnauthorized()
    {
        // Arrange - Generate token with very short expiry and wait for it to expire
        var issuer = "https://app.dynamic.xyz/test";
        var subject = "user-123";
        var tokenResult = await _authService.GenerateAccessTokenAsync(
            _testAxonUserId, _testProviderType, issuer, subject, 1, CancellationToken.None); // 1 second expiry
        tokenResult.IsSuccess.ShouldBeTrue();

        var token = tokenResult.Value.AccessToken;

        // Wait for token to expire
        await Task.Delay(1100); // Wait a bit longer than expiry

        // Act
        var result = await _authService.ValidateTokenAsync(token, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
        result.Error.Code.ShouldBe(AuthErrors.TokenExpired);
    }

    [Test]
    public async Task ValidateTokenAsync_WithEmptyToken_ReturnsUnauthorized()
    {
        // Act
        var result = await _authService.ValidateTokenAsync("", CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
        result.Error.Code.ShouldBe(AuthErrors.TokenRequired);
    }

    [Test]
    public async Task ValidateTokenAsync_WithMalformedToken_ReturnsUnauthorized()
    {
        // Act
        var result = await _authService.ValidateTokenAsync("malformed.token", CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBeOneOf(ErrorType.Unauthorized, ErrorType.Internal);
    }

    #endregion

    #region Challenge Generation Tests

    [Test]
    public async Task GenerateChallengeAsync_WithValidParameters_ReturnsChallenge()
    {
        // Arrange
        var compoundChainId = "ethereum-mainnet";
        var walletAddress = "0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e41";
        var audience = "test-audience";

        // Act
        var result = await _authService.GenerateChallengeAsync(
            compoundChainId, walletAddress, audience, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ChainId.ShouldBe(compoundChainId);
        result.Value.Address.ShouldBe(walletAddress);
        result.Value.Aud.ShouldBe(audience);
        result.Value.Nonce.ShouldNotBeNullOrEmpty();
        result.Value.Message.ShouldNotBeNullOrEmpty();
        result.Value.Exp.ShouldBeGreaterThan(result.Value.IssuedAt);

        // Verify message structure
        var messageDoc = JsonDocument.Parse(result.Value.Message);
        var root = messageDoc.RootElement;
        root.GetProperty("network_environment").GetString().ShouldBe("mainnet");
        root.GetProperty("chain_id").GetString().ShouldBe("ethereum");
        root.GetProperty("address").GetString().ShouldBe(walletAddress);
        root.GetProperty("aud").GetString().ShouldBe(audience);
        root.GetProperty("nonce").GetString().ShouldBe(result.Value.Nonce);
    }

    [Test]
    public async Task GenerateChallengeAsync_WithEmptyAudience_UsesDefaultAudience()
    {
        // Arrange
        var compoundChainId = "ethereum-mainnet";
        var walletAddress = "0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e41";
        var audience = ""; // Empty audience

        // Act
        var result = await _authService.GenerateChallengeAsync(
            compoundChainId, walletAddress, audience, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Aud.ShouldBe(_options.DefaultAudience);
    }

    [Test]
    public async Task GenerateChallengeAsync_WithInvalidChainId_ReturnsValidationError()
    {
        // Arrange
        var compoundChainId = ""; // Empty chain ID
        var walletAddress = "0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e41";
        var audience = "test-audience";

        // Act
        var result = await _authService.GenerateChallengeAsync(
            compoundChainId, walletAddress, audience, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
    }

    [Test]
    public async Task GenerateChallengeAsync_WithInvalidAddress_ReturnsValidationError()
    {
        // Arrange
        var compoundChainId = "ethereum-mainnet";
        var invalidAddress = "invalid-address";
        var audience = "test-audience";

        // Act
        var result = await _authService.GenerateChallengeAsync(
            compoundChainId, invalidAddress, audience, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
    }

    #endregion

    #region Challenge Validation Tests

    [Test]
    public async Task ValidateChallenge_WithValidChallenge_ReturnsSuccess()
    {
        // Arrange - Generate challenge first
        var compoundChainId = "ethereum-mainnet";
        var walletAddress = "0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e41";
        var audience = "test-audience";

        var challengeResult = await _authService.GenerateChallengeAsync(
            compoundChainId, walletAddress, audience, CancellationToken.None);
        challengeResult.IsSuccess.ShouldBeTrue();

        var challenge = challengeResult.Value;

        // Act
        var result = _authService.ValidateChallenge(
            challenge.Message, compoundChainId, walletAddress, audience);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
    }

    [Test]
    public void ValidateChallenge_WithNetworkMismatch_ReturnsValidationError()
    {
        // Arrange
        var message = CreateTestChallengeMessage("mainnet", "ethereum", "0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e41", "test");
        var expectedChainId = "ethereum-testnet"; // Different network

        // Act
        var result = _authService.ValidateChallenge(
            message, expectedChainId, "0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e41", "test");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Code.ShouldBe(AuthErrors.ChallengeNetworkMismatch);
    }

    [Test]
    public void ValidateChallenge_WithChainMismatch_ReturnsValidationError()
    {
        // Arrange
        var message = CreateTestChallengeMessage("mainnet", "ethereum", "0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e41", "test");
        var expectedChain = "polygon"; // Different chain

        // Act
        var result = _authService.ValidateChallenge(
            message, $"{expectedChain}-mainnet", "0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e41", "test");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Code.ShouldBe(AuthErrors.ChallengeChainMismatch);
    }

    [Test]
    public void ValidateChallenge_WithExpiredChallenge_ReturnsValidationError()
    {
        // Arrange - Create expired challenge
        var expiredTime = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds();
        var issuedTime = DateTimeOffset.UtcNow.AddMinutes(-15).ToUnixTimeSeconds();
        var message = CreateTestChallengeMessage("mainnet", "ethereum", "0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e41", "test", issuedTime, expiredTime);

        // Act
        var result = _authService.ValidateChallenge(
            message, "ethereum-mainnet", "0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e41", "test");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Code.ShouldBe(AuthErrors.ChallengeExpired);
    }

    [Test]
    public void ValidateChallenge_WithInvalidJson_ReturnsValidationError()
    {
        // Arrange
        var invalidMessage = "{ invalid json }";

        // Act
        var result = _authService.ValidateChallenge(
            invalidMessage, "ethereum-mainnet", "0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e41", "test");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Code.ShouldBe(AuthErrors.ChallengeInvalidJson);
    }

    #endregion

    #region JWT Replay Protection Tests

    [Test]
    public async Task CheckAndMarkTokenUsedAsync_WithValidJti_ReturnsSuccess()
    {
        // Arrange
        var jti = Guid.NewGuid().ToString();
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);

        // Act
        var result = await _authService.CheckAndMarkTokenUsedAsync(jti, expiresAt, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        // Verify token is marked as used in cache
        var cacheKey = $"jwt_used_{jti}";
        _memoryCache.TryGetValue(cacheKey, out _).ShouldBeTrue();
    }

    [Test]
    public async Task CheckAndMarkTokenUsedAsync_WithReusedJti_ReturnsUnauthorized()
    {
        // Arrange
        var jti = Guid.NewGuid().ToString();
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);

        // First use
        var firstResult = await _authService.CheckAndMarkTokenUsedAsync(jti, expiresAt, CancellationToken.None);
        firstResult.IsSuccess.ShouldBeTrue();

        // Act - Second use (replay attempt)
        var result = await _authService.CheckAndMarkTokenUsedAsync(jti, expiresAt, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
        result.Error.Code.ShouldBe(AuthErrors.TokenReplayed);
    }

    [Test]
    public async Task CheckAndMarkTokenUsedAsync_WithExpiredToken_ReturnsUnauthorized()
    {
        // Arrange
        var jti = Guid.NewGuid().ToString();
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(-10); // Already expired

        // Act
        var result = await _authService.CheckAndMarkTokenUsedAsync(jti, expiresAt, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
        result.Error.Code.ShouldBe(AuthErrors.TokenExpired);
    }

    [Test]
    public async Task CheckAndMarkTokenUsedAsync_WithEmptyJti_ReturnsValidationError()
    {
        // Arrange
        var emptyJti = "";
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);

        // Act
        var result = await _authService.CheckAndMarkTokenUsedAsync(emptyJti, expiresAt, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Code.ShouldBe(AuthErrors.TokenRequired);
    }

    #endregion

    #region Token Type Detection Tests

    [Test]
    public async Task ValidateTokenAsync_CorrectlyDetectsAxonTokenType()
    {
        // Arrange - Generate Axon token
        var tokenResult = await _authService.GenerateAccessTokenAsync(
            _testAxonUserId, _testProviderType, "test-issuer", "test-subject", 3600, CancellationToken.None);
        tokenResult.IsSuccess.ShouldBeTrue();

        // Act
        var result = await _authService.ValidateTokenAsync(tokenResult.Value.AccessToken, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TokenType.ShouldBe(Axon.Modules.Identity.Application.Contracts.Services.TokenType.AxonAccessToken);
    }

    [Test]
    public async Task ValidateTokenAsync_CorrectlyDetectsDynamicTokenType()
    {
        // Arrange - Mock Dynamic token
        var dynamicToken = "dynamic.jwt.token";
        var mockDynamicUserData = new DynamicUserData(
            AxonUserId: _testAxonUserId.Value.ToString(),
            Email: "test@example.com",
            EnvironmentId: "test-env",
            Wallets: new List<WalletData>(),
            FirstVisitUtc: DateTimeOffset.UtcNow,
            LastVisitUtc: DateTimeOffset.UtcNow,
            IsNewUser: false);

        var mockClaims = new[]
        {
            new Claim("sub", "dynamic-subject"),
            new Claim("iss", "https://app.dynamic.xyz"),
            new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
            new Claim("exp", DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString())
        };
        var mockPrincipal = new ClaimsPrincipal(new ClaimsIdentity(mockClaims));

        _mockDynamicAuthService.ValidateTokenAsync(dynamicToken, Arg.Any<CancellationToken>())
            .Returns(Result.Success<DynamicUserData, Error>(mockDynamicUserData));

        _mockDynamicAuthService.GetRawClaimsAsync(dynamicToken, Arg.Any<CancellationToken>())
            .Returns(Result.Success<ClaimsPrincipal, Error>(mockPrincipal));

        // Act
        var result = await _authService.ValidateTokenAsync(dynamicToken, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TokenType.ShouldBe(Axon.Modules.Identity.Application.Contracts.Services.TokenType.DynamicJwt);
    }

    #endregion

    #region Helper Methods

    private static string CreateTestChallengeMessage(
        string networkEnvironment,
        string chainId,
        string address,
        string audience,
        long? issuedAt = null,
        long? exp = null)
    {
        var now = DateTimeOffset.UtcNow;
        var iat = issuedAt ?? now.ToUnixTimeSeconds();
        var expiry = exp ?? now.AddMinutes(15).ToUnixTimeSeconds();

        var challenge = new
        {
            network_environment = networkEnvironment,
            chain_id = chainId,
            address = address,
            issued_at = iat,
            exp = expiry,
            nonce = "test-nonce-12345",
            aud = audience
        };

        return JsonSerializer.Serialize(challenge);
    }

    #endregion
}