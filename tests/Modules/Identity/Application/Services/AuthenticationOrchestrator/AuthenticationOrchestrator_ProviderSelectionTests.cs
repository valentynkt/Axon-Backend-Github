using Axon.Modules.Identity.Application.Common;
using Axon.Modules.Identity.Application.Contracts.Providers;
using Axon.Modules.Identity.Application.Tests.Services.AuthenticationOrchestrator._TestInfrastructure;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Application.Tests.Services.AuthenticationOrchestrator;

/// <summary>
/// Tests for AuthenticationOrchestrator provider selection logic.
/// Verifies that the orchestrator correctly selects and delegates to the appropriate provider.
///
/// TESTING FOCUS:
/// - Provider selection by type (case-insensitive)
/// - Handling of missing/unsupported providers
/// - Multiple provider scenarios
/// </summary>
[TestFixture]
public class AuthenticationOrchestrator_ProviderSelectionTests : AuthenticationOrchestratorTestBase
{
    [Test]
    public async Task ExchangeDynamicToken_Should_UseDynamicProvider()
    {
        // Arrange
        var token = "test-dynamic-token";
        var authData = CreateAuthenticationData(providerType: "dynamic");

        DynamicProvider.AuthenticateAsync(
            Arg.Any<DynamicExchangeRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success<AuthenticationData, Error>(authData));

        // Act
        var result = await Orchestrator.ExchangeDynamicTokenAsync(token, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        
        // Verify Dynamic provider was called
        await DynamicProvider.Received(1).AuthenticateAsync(
            Arg.Any<DynamicExchangeRequest>(),
            Arg.Any<CancellationToken>());
        
        // Verify Wallet provider was NOT called
        await WalletProvider.DidNotReceive().AuthenticateAsync(
            Arg.Any<AuthenticationRequest>(),
            Arg.Any<CancellationToken>());
    }
    [Test]
    public async Task AuthenticateWithWallet_Should_UseWalletProvider()
    {
        // Arrange
        var request = new WalletAuthenticationRequest(
            ChainId: "solana",
            Address: "7xKXtg2CW87d97TXJSDpbD5jBkheTqA83TZRuJosgAsU",
            SignedMessage: "test-signed-message",
            Signature: "test-signature",
            Mac: "test-mac",
            Mkv: "test-mkv");

        var authData = CreateAuthenticationData(providerType: "manual");

        WalletProvider.AuthenticateAsync(
            Arg.Any<WalletAuthenticationRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success<AuthenticationData, Error>(authData));

        // Act
        var result = await Orchestrator.AuthenticateWithWalletAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        
        // Verify Wallet provider was called
        await WalletProvider.Received(1).AuthenticateAsync(
            Arg.Any<WalletAuthenticationRequest>(),
            Arg.Any<CancellationToken>());        // Verify Dynamic provider was NOT called
        await DynamicProvider.DidNotReceive().AuthenticateAsync(
            Arg.Any<AuthenticationRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProviderSelection_Should_BeCaseInsensitive()
    {
        // Arrange - Provider registered as "dynamic" (lowercase)
        var token = "test-token";
        var authData = CreateAuthenticationData(providerType: "dynamic");

        DynamicProvider.ProviderType.Returns("DYNAMIC"); // Uppercase
        DynamicProvider.AuthenticateAsync(
            Arg.Any<DynamicExchangeRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success<AuthenticationData, Error>(authData));

        // Act
        var result = await Orchestrator.ExchangeDynamicTokenAsync(token, CancellationToken.None);

        // Assert - Should still find provider despite case difference
        result.IsSuccess.ShouldBeTrue();
        await DynamicProvider.Received(1).AuthenticateAsync(
            Arg.Any<DynamicExchangeRequest>(),
            Arg.Any<CancellationToken>());
    }
    [Test]
    public async Task AuthenticateWithWallet_WhenWalletProviderMissing_Should_ReturnNotSupported()
    {
        // Arrange - Only Dynamic provider available
        Providers = new[] { DynamicProvider };
        
        // Recreate orchestrator with only Dynamic provider
        Orchestrator = new Application.Services.AuthenticationOrchestrator(
            Providers,
            TokenService,
            UserManager,
            SignInManager,
            Cache,
            Logger,
            ChallengeService,
            RefreshTokenProvider,
            AuthOptions);

        var request = new WalletAuthenticationRequest(
            ChainId: "solana",
            Address: "test-address",
            SignedMessage: "test-message",
            Signature: "test-sig",
            Mac: "test-mac",
            Mkv: "test-mkv");

        // Act
        var result = await Orchestrator.AuthenticateWithWalletAsync(request, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Message.ShouldBe("Wallet authentication not configured");
    }

    [Test]
    public async Task ExchangeDynamicToken_WhenDynamicProviderMissing_Should_ReturnNotSupported()
    {        // Arrange - Only Wallet provider available
        Providers = new[] { WalletProvider };
        
        // Recreate orchestrator with only Wallet provider
        Orchestrator = new Application.Services.AuthenticationOrchestrator(
            Providers,
            TokenService,
            UserManager,
            SignInManager,
            Cache,
            Logger,
            ChallengeService,
            RefreshTokenProvider,
            AuthOptions);

        var token = "test-dynamic-token";

        // Act
        var result = await Orchestrator.ExchangeDynamicTokenAsync(token, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Message.ShouldBe("Dynamic authentication not configured");
    }

    [Test]
    public async Task MultipleProviders_Should_SelectCorrectProvider()
    {
        // Arrange - Both providers available (default setup)
        var dynamicToken = "dynamic-token";
        var dynamicAuthData = CreateAuthenticationData(providerType: "dynamic");

        DynamicProvider.AuthenticateAsync(
            Arg.Any<DynamicExchangeRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success<AuthenticationData, Error>(dynamicAuthData));

        var walletRequest = new WalletAuthenticationRequest(
            ChainId: "solana",
            Address: "test-address",
            SignedMessage: "test-message",
            Signature: "test-sig",
            Mac: "test-mac",
            Mkv: "test-mkv");

        var walletAuthData = CreateAuthenticationData(providerType: "manual");

        WalletProvider.AuthenticateAsync(
            Arg.Any<WalletAuthenticationRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success<AuthenticationData, Error>(walletAuthData));

        // Act - Call both authentication methods
        var dynamicResult = await Orchestrator.ExchangeDynamicTokenAsync(dynamicToken, CancellationToken.None);
        var walletResult = await Orchestrator.AuthenticateWithWalletAsync(walletRequest, CancellationToken.None);

        // Assert - Both should succeed with correct provider
        dynamicResult.IsSuccess.ShouldBeTrue();
        walletResult.IsSuccess.ShouldBeTrue();

        // Verify each provider was called exactly once with correct request type
        await DynamicProvider.Received(1).AuthenticateAsync(
            Arg.Any<DynamicExchangeRequest>(),
            Arg.Any<CancellationToken>());
        
        await WalletProvider.Received(1).AuthenticateAsync(
            Arg.Any<WalletAuthenticationRequest>(),
            Arg.Any<CancellationToken>());
    }
}