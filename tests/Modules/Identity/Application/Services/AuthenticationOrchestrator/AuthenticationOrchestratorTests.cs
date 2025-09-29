using Axon.Modules.Identity.Application.Common;
using Axon.Modules.Identity.Application.Common.Models;
using Axon.Modules.Identity.Application.Contracts.Providers;
using Axon.Modules.Identity.Application.Tests.Services.AuthenticationOrchestrator._TestInfrastructure;
using Axon.Modules.Identity.Domain.Entities;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Application.Tests.Services.AuthenticationOrchestrator;

/// <summary>
/// Core tests for AuthenticationOrchestrator.
/// Tests the orchestration layer where provider coordination happens.
///
/// ARCHITECTURE: Orchestrator is the entry point for all authentication.
/// - Coordinates between multiple providers (Dynamic, Wallet, etc.)
/// - Generates Axon tokens after successful provider authentication
/// - Manages token refresh and session invalidation
/// - Delegates challenge operations to ChallengeService
///
/// TESTING PHILOSOPHY (from Sprint 2):
/// - Focus on observable behavior (authentication succeeds/fails, correct tokens returned)
/// - Minimal infrastructure mocking
/// - Integration-style tests that validate end-to-end orchestrator flows
/// </summary>
[TestFixture]
public class AuthenticationOrchestratorTests : AuthenticationOrchestratorTestBase
{
    #region Dynamic Token Exchange Tests (3 tests)

    [Test]
    public async Task ExchangeDynamicToken_WithValidToken_Should_ReturnAuthResponse()
    {
        // Arrange
        var dynamicToken = "valid-dynamic-jwt-token";
        var principalId = new AxonUserId(Guid.NewGuid());
        var user = CreateAxonUserAuth(principalId: principalId, email: "test@example.com");

        var authData = CreateAuthenticationData(
            user: user,
            providerType: "dynamic",
            additionalClaims: new Dictionary<string, object>
            {
                ["created"] = false,
                ["wallets_processed"] = 3,
                ["wallets_linked"] = 3
            });

        // Mock Dynamic provider authentication
        DynamicProvider.AuthenticateAsync(
            Arg.Is<AuthenticationRequest>(r => r.RequestType == "dynamic"),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success<AuthenticationData, Error>(authData));

        // Act
        var result = await Orchestrator.ExchangeDynamicTokenAsync(dynamicToken, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldSatisfyAllConditions(
            response => response.AccessToken.ShouldNotBeNullOrEmpty(),
            response => response.UserId.ShouldBe(user.Id),
            response => response.ProviderType.ShouldBe("dynamic"),
            response => response.ExpiresAt.ShouldBeGreaterThan(DateTime.UtcNow),
            response => response.AdditionalData.ShouldNotBeNull()
        );

        // Check additional data separately to avoid nullable warning
        result.Value.AdditionalData!["wallets_processed"].ShouldBe(3);
        result.Value.AdditionalData["wallets_linked"].ShouldBe(3);
    }

    [Test]
    public async Task ExchangeDynamicToken_WhenProviderFails_Should_ReturnFailure()
    {
        // Arrange
        var dynamicToken = "invalid-dynamic-jwt-token";
        var expectedError = Error.Unauthorized("Invalid token");

        // Mock Dynamic provider to return failure
        DynamicProvider.AuthenticateAsync(
            Arg.Any<DynamicExchangeRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(Result.Failure<AuthenticationData, Error>(expectedError));

        // Act
        var result = await Orchestrator.ExchangeDynamicTokenAsync(dynamicToken, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(expectedError);
    }

    [Test]
    public async Task ExchangeDynamicToken_WhenTokenServiceFails_Should_ReturnFailure()
    {
        // Arrange
        var dynamicToken = "valid-token-but-token-service-fails";
        var user = CreateAxonUserAuth();
        var authData = CreateAuthenticationData(user: user);

        // Mock provider succeeds
        DynamicProvider.AuthenticateAsync(
            Arg.Any<DynamicExchangeRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success<AuthenticationData, Error>(authData));

        // Mock TokenService fails - Use ReturnsForAnyArgs to avoid Vogen uninitialized exception
        var tokenError = Error.Internal("Token generation failed");
        TokenService.GenerateAccessTokenAsync(
            new AxonUserId(Guid.NewGuid()),
            Domain.ValueObjects.ProviderType.Dynamic,
            "",
            "",
            30,
            default)
            .ReturnsForAnyArgs(Result.Failure<AxonToken, Error>(tokenError));

        // Act
        var result = await Orchestrator.ExchangeDynamicTokenAsync(dynamicToken, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(tokenError);
    }

    #endregion

    #region Wallet Authentication Tests (2 tests)

    [Test]
    public async Task AuthenticateWithWallet_WithValidRequest_Should_ReturnAuthResponse()
    {
        // Arrange
        var request = new WalletAuthenticationRequest(
            ChainId: "solana",
            Address: "7xKXtg2CW87d97TXJSDpbD5jBkheTqA83TZRuJosgAsU",
            SignedMessage: "test-signed-message",
            Signature: "test-signature",
            Mac: "test-mac",
            Mkv: "test-mkv");

        var principalId = new AxonUserId(Guid.NewGuid());
        var user = CreateAxonUserAuth(principalId: principalId, providerType: "manual");

        var authData = CreateAuthenticationData(
            user: user,
            providerType: "manual");

        // Mock Wallet provider authentication
        WalletProvider.AuthenticateAsync(
            Arg.Is<WalletAuthenticationRequest>(r =>
                r.ChainId == request.ChainId &&
                r.Address == request.Address),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success<AuthenticationData, Error>(authData));

        // Act
        var result = await Orchestrator.AuthenticateWithWalletAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldSatisfyAllConditions(
            response => response.AccessToken.ShouldNotBeNullOrEmpty(),
            response => response.UserId.ShouldBe(user.Id),
            response => response.ProviderType.ShouldBe("wallet"),
            response => response.ExpiresAt.ShouldBeGreaterThan(DateTime.UtcNow)
        );
    }

    [Test]
    public async Task AuthenticateWithWallet_WhenProviderFails_Should_ReturnFailure()
    {
        // Arrange
        var request = new WalletAuthenticationRequest(
            ChainId: "solana",
            Address: "invalid-address",
            SignedMessage: "test-signed-message",
            Signature: "invalid-signature",
            Mac: "test-mac",
            Mkv: "test-mkv");

        var expectedError = Error.Unauthorized("Invalid signature");

        // Mock Wallet provider to return failure
        WalletProvider.AuthenticateAsync(
            Arg.Any<WalletAuthenticationRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(Result.Failure<AuthenticationData, Error>(expectedError));

        // Act
        var result = await Orchestrator.AuthenticateWithWalletAsync(request, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(expectedError);
    }

    #endregion

    #region Token Refresh Tests (3 tests)

    [Test]
    public async Task RefreshToken_WithValidToken_Should_ReturnNewTokens()
    {
        // Arrange
        var refreshToken = "valid-refresh-token";
        var userId = new AxonUserId(Guid.NewGuid());
        var user = CreateAxonUserAuth(principalId: userId);

        // Mock RefreshTokenProvider to return user ID and validate successfully
        RefreshTokenProvider.GetJtiFromToken(refreshToken)
            .Returns("test-jti-123");

        RefreshTokenProvider.GetUserIdFromToken(refreshToken)
            .Returns(userId.Value);

        // Mock UserManager to find user
        UserManager.FindByIdAsync(userId.Value.ToString())
            .Returns(user);

        // Mock validation succeeds
        RefreshTokenProvider.ValidateAsync(
            "RefreshToken",
            refreshToken,
            UserManager,
            user)
            .Returns(true);

        // Mock new refresh token generation
        var newRefreshToken = "new-refresh-token";
        RefreshTokenProvider.GenerateAsync(
            "RefreshToken",
            UserManager,
            user)
            .Returns(newRefreshToken);

        // Act
        var result = await Orchestrator.RefreshTokenAsync(refreshToken, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldSatisfyAllConditions(
            response => response.AccessToken.ShouldNotBeNullOrEmpty(),
            response => response.RefreshToken.ShouldBe(newRefreshToken),
            response => response.TokenType.ShouldBe("Bearer"),
            response => response.ExpiresIn.ShouldBe(1800), // 30 minutes in seconds
            response => response.AccessTokenExpiresAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow),
            response => response.RefreshTokenExpiresAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow)
        );
    }

    [Test]
    public async Task RefreshToken_WithInvalidToken_Should_ReturnFailure()
    {
        // Arrange
        var refreshToken = "invalid-refresh-token";

        // Mock RefreshTokenProvider to return empty JTI (invalid)
        RefreshTokenProvider.GetJtiFromToken(refreshToken)
            .Returns(string.Empty);

        // Act
        var result = await Orchestrator.RefreshTokenAsync(refreshToken, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
        result.Error.Message.ShouldBe("Invalid refresh token");
    }

    [Test]
    public async Task RefreshToken_WhenUserNotFound_Should_ReturnFailure()
    {
        // Arrange
        var refreshToken = "valid-token-but-user-gone";
        var userId = new AxonUserId(Guid.NewGuid());

        RefreshTokenProvider.GetJtiFromToken(refreshToken)
            .Returns("test-jti-123");

        RefreshTokenProvider.GetUserIdFromToken(refreshToken)
            .Returns(userId.Value);

        // Mock UserManager to return null (user not found)
        UserManager.FindByIdAsync(userId.Value.ToString())
            .Returns(Task.FromResult<AxonUserAuth?>(null));

        // Act
        var result = await Orchestrator.RefreshTokenAsync(refreshToken, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
        result.Error.Message.ShouldBe("User not found");
    }

    #endregion

    #region Session Management Tests (1 test)

    [Test]
    public async Task InvalidateSession_WithValidUserId_Should_Succeed()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateAxonUserAuth(principalId: new AxonUserId(userId));

        // Mock UserManager to find user
        UserManager.FindByIdAsync(userId.ToString())
            .Returns(user);

        // Mock UserManager UpdateSecurityStampAsync succeeds
        UserManager.UpdateSecurityStampAsync(user)
            .Returns(Microsoft.AspNetCore.Identity.IdentityResult.Success);

        // Act
        var result = await Orchestrator.InvalidateSessionAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    #endregion

    #region Challenge Delegation Tests (1 test)

    [Test]
    public async Task GenerateChallenge_Should_DelegateToChallengeService()
    {
        // Arrange
        var chainId = "solana";
        var walletAddress = "7xKXtg2CW87d97TXJSDpbD5jBkheTqA83TZRuJosgAsU";
        var audience = "test-app";

        var expectedChallenge = new AuthenticationChallenge(
            ChainId: chainId,
            Address: walletAddress,
            IssuedAt: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Exp: DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds(),
            Nonce: "nonce-123",
            Aud: audience,
            Message: "Sign this message",
            Mac: "test-mac",
            Mkv: "test-mkv");

        ChallengeService.GenerateChallengeAsync(chainId, walletAddress, audience, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success<AuthenticationChallenge, Error>(expectedChallenge)));

        // Act
        var result = await Orchestrator.GenerateChallengeAsync(chainId, walletAddress, audience, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(expectedChallenge);

        // Verify delegation occurred
        await ChallengeService.Received(1).GenerateChallengeAsync(chainId, walletAddress, audience, Arg.Any<CancellationToken>());
    }

    #endregion
}