using Axon.Modules.Identity.Application.Contracts.ExternalServices;
using Axon.Modules.Identity.Application.Contracts.Providers;
using Axon.Modules.Identity.Application.Tests.Providers._TestInfrastructure;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using NSubstitute;
using NUnit.Framework;
using Shouldly;
using System.Security.Claims;

namespace Axon.Modules.Identity.Application.Tests.Providers;

/// <summary>
/// Tests for DynamicAuthenticationProvider - the provider layer where exchange logic lives.
/// 
/// ARCHITECTURE: This is the correct layer to test the Dynamic token exchange business logic.
/// - Token validation and user data extraction
/// - Wallet normalization
/// - Principal resolution (credential-first → wallet-fallback)
/// - Conflict detection
/// - Wallet processing and metrics
/// - Identity user management
/// - Cache warming
/// 
/// This replaces obsolete tests that were testing this logic at the wrong layer (handler/resolution service).
/// </summary>
[TestFixture]
public class DynamicAuthenticationProviderTests : DynamicProviderTestBase
{
    #region Token Validation Tests (3 tests)

    [Test]
    public async Task ValidToken_Should_ExtractUserData()
    {
        // Arrange
        var token = "valid-dynamic-jwt-token";
        var expectedUserId = Guid.NewGuid().ToString();
        var expectedEmail = "test@example.com";
        
        var dynamicUserData = AuthenticationTestFixtures.ValidDynamicUserData(
            userId: expectedUserId,
            email: expectedEmail);        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("iss", "https://app.dynamic.xyz/test-env"),
            new Claim("sub", expectedUserId)
        }));

        DynamicAuthService.ValidateTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(Result.Success<DynamicUserData, Error>(dynamicUserData));
        
        DynamicAuthService.GetRawClaimsAsync(token, Arg.Any<CancellationToken>())
            .Returns(Result.Success<ClaimsPrincipal, Error>(claimsPrincipal));

        // Mock resolution to return a new principal
        var principal = CreatePrincipalWithDynamicCredential(
            issuer: "https://app.dynamic.xyz/test-env",
            subject: expectedUserId);
        
        var resolutionResult = new PrincipalResolutionResult(
            Principal: principal,
            Path: ResolutionPath.Created,
            WasAutoLinked: false);
        
        ResolutionService.ResolveAsync(
            Arg.Any<ProviderType>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<ChainId>(),
            Arg.Any<Address>(),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success<PrincipalResolutionResult, Error>(resolutionResult));

        var request = AuthenticationTestFixtures.ValidDynamicExchangeRequest(token);

        // Act
        var result = await Provider.AuthenticateAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.User.ShouldNotBeNull();
        result.Value.ProviderType.ShouldBe("dynamic");
        result.Value.AdditionalClaims.ShouldContainKey("dynamic_user_id");
        result.Value.AdditionalClaims["dynamic_user_id"].ShouldBe(expectedUserId);
    }    [Test]
    public async Task InvalidToken_Should_ReturnUnauthorized()
    {
        // Arrange
        var token = "invalid-jwt-token";
        
        DynamicAuthService.ValidateTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<DynamicUserData, Error>(
                Error.Unauthorized("Invalid token signature", "AUTH.INVALID_TOKEN")));

        var request = AuthenticationTestFixtures.InvalidDynamicExchangeRequest();

        // Act
        var result = await Provider.AuthenticateAsync(request, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
        result.Error.Code.ShouldBe("AUTH.INVALID_TOKEN");
    }

    [Test]
    public async Task ExpiredToken_Should_ReturnUnauthorized()
    {
        // Arrange
        var token = "expired-jwt-token";
        
        DynamicAuthService.ValidateTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<DynamicUserData, Error>(
                Error.Unauthorized("Token has expired", "AUTH.TOKEN_EXPIRED")));

        var request = new DynamicExchangeRequest(token);

        // Act
        var result = await Provider.AuthenticateAsync(request, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
        result.Error.Message.ShouldContain("expired");
    }

    #endregion

    #region Wallet Normalization Tests (4 tests)

    [Test]
    public async Task ValidWallets_Should_NormalizeSuccessfully()
    {
        // Arrange
        var token = "valid-token-with-wallets";
        var userId = Guid.NewGuid().ToString();
        
        var wallets = new List<WalletData>
        {
            AuthenticationTestFixtures.EthereumWalletData("0x1111111111111111111111111111111111111111"),
            AuthenticationTestFixtures.SolanaWalletData("DYw8jCTfwHNRJhhmFcbXvVDTqWMEVFBX6ZKUmG5CNSKK")
        };
        
        var dynamicUserData = AuthenticationTestFixtures.ValidDynamicUserData(
            userId: userId,
            wallets: wallets);

        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("iss", "https://app.dynamic.xyz/test-env"),
            new Claim("sub", userId)
        }));

        DynamicAuthService.ValidateTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(Result.Success<DynamicUserData, Error>(dynamicUserData));
        
        DynamicAuthService.GetRawClaimsAsync(token, Arg.Any<CancellationToken>())
            .Returns(Result.Success<ClaimsPrincipal, Error>(claimsPrincipal));

        // Mock address normalization to succeed for both wallets
        AddressNormalizer.NormalizeAddress("evm-1", "0x1111111111111111111111111111111111111111")
            .Returns(Address.Create("0x1111111111111111111111111111111111111111"));
        
        AddressNormalizer.NormalizeAddress("solana-mainnet", "DYw8jCTfwHNRJhhmFcbXvVDTqWMEVFBX6ZKUmG5CNSKK")
            .Returns(Address.Create("DYw8jCTfwHNRJhhmFcbXvVDTqWMEVFBX6ZKUmG5CNSKK"));

        // Mock resolution to return a new principal
        var principal = CreatePrincipalWithDynamicCredential(subject: userId);
        var resolutionResult = new PrincipalResolutionResult(
            Principal: principal,
            Path: ResolutionPath.Created,
            WasAutoLinked: false);
        
        ResolutionService.ResolveAsync(
            Arg.Any<ProviderType>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<ChainId>(),
            Arg.Any<Address>(),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success<PrincipalResolutionResult, Error>(resolutionResult));

        var request = new DynamicExchangeRequest(token);

        // Act
        var result = await Provider.AuthenticateAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.AdditionalClaims["wallets_processed"].ShouldBe(2);
        
        // Verify normalization was called for both wallets
        AddressNormalizer.Received(1).NormalizeAddress("evm-1", "0x1111111111111111111111111111111111111111");
        AddressNormalizer.Received(1).NormalizeAddress("solana-mainnet", "DYw8jCTfwHNRJhhmFcbXvVDTqWMEVFBX6ZKUmG5CNSKK");
    }

    [Test]
    public async Task InvalidWalletAddress_Should_SkipWithWarning()
    {
        // Arrange
        var token = "valid-token-with-invalid-wallet";
        var userId = Guid.NewGuid().ToString();
        
        var wallets = new List<WalletData>
        {
            AuthenticationTestFixtures.EthereumWalletData("0x1111111111111111111111111111111111111111"),
            AuthenticationTestFixtures.InvalidAddressWalletData()
        };
        
        var dynamicUserData = AuthenticationTestFixtures.ValidDynamicUserData(
            userId: userId,
            wallets: wallets);

        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("iss", "https://app.dynamic.xyz/test-env"),
            new Claim("sub", userId)
        }));

        DynamicAuthService.ValidateTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(Result.Success<DynamicUserData, Error>(dynamicUserData));
        
        DynamicAuthService.GetRawClaimsAsync(token, Arg.Any<CancellationToken>())
            .Returns(Result.Success<ClaimsPrincipal, Error>(claimsPrincipal));

        // Mock: First wallet normalizes successfully
        AddressNormalizer.NormalizeAddress("evm-1", "0x1111111111111111111111111111111111111111")
            .Returns(Address.Create("0x1111111111111111111111111111111111111111"));
        
        // Mock: Second wallet fails normalization
        AddressNormalizer.NormalizeAddress("evm-1", "invalid-address-format")
            .Returns(Result.Failure<Address, Error>(
                Error.Validation("Invalid address format", "ADDRESS.INVALID_FORMAT")));

        // Mock resolution
        var principal = CreatePrincipalWithDynamicCredential(subject: userId);
        var resolutionResult = new PrincipalResolutionResult(
            Principal: principal,
            Path: ResolutionPath.Created,
            WasAutoLinked: false);
        
        ResolutionService.ResolveAsync(
            Arg.Any<ProviderType>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<ChainId>(),
            Arg.Any<Address>(),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success<PrincipalResolutionResult, Error>(resolutionResult));

        var request = new DynamicExchangeRequest(token);

        // Act
        var result = await Provider.AuthenticateAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        // Should have logged warning but continued processing
        // Only 1 wallet should have been normalized successfully
        Logger.Received().LogWarning(
            Arg.Is<string>(s => s.Contains("Failed to normalize address")),
            Arg.Any<object[]>());
    }    [Test]
    public async Task UnsupportedChain_Should_SkipWallet()
    {
        // Arrange
        var token = "valid-token-with-unsupported-chain";
        var userId = Guid.NewGuid().ToString();
        
        var wallets = new List<WalletData>
        {
            AuthenticationTestFixtures.EthereumWalletData("0x1111111111111111111111111111111111111111"),
            AuthenticationTestFixtures.InvalidChainWalletData()
        };
        
        var dynamicUserData = AuthenticationTestFixtures.ValidDynamicUserData(
            userId: userId,
            wallets: wallets);

        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("iss", "https://app.dynamic.xyz/test-env"),
            new Claim("sub", userId)
        }));

        DynamicAuthService.ValidateTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(Result.Success<DynamicUserData, Error>(dynamicUserData));
        
        DynamicAuthService.GetRawClaimsAsync(token, Arg.Any<CancellationToken>())
            .Returns(Result.Success<ClaimsPrincipal, Error>(claimsPrincipal));

        // Mock: First wallet succeeds
        AddressNormalizer.NormalizeAddress("evm-1", "0x1111111111111111111111111111111111111111")
            .Returns(Address.Create("0x1111111111111111111111111111111111111111"));
        
        // Mock: Unsupported chain fails normalization
        AddressNormalizer.NormalizeAddress("unsupported-chain-999", Arg.Any<string>())
            .Returns(Result.Failure<Address, Error>(
                Error.Validation("Unsupported chain", "CHAIN.UNSUPPORTED")));

        // Mock resolution
        var principal = CreatePrincipalWithDynamicCredential(subject: userId);
        var resolutionResult = new PrincipalResolutionResult(
            Principal: principal,
            Path: ResolutionPath.Created,
            WasAutoLinked: false);
        
        ResolutionService.ResolveAsync(
            Arg.Any<ProviderType>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<ChainId>(),
            Arg.Any<Address>(),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success<PrincipalResolutionResult, Error>(resolutionResult));

        var request = new DynamicExchangeRequest(token);

        // Act
        var result = await Provider.AuthenticateAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        // Should have logged warning and continued
        Logger.Received().LogWarning(
            Arg.Is<string>(s => s.Contains("Failed to normalize address")),
            Arg.Any<object[]>());
    }

    [Test]
    public async Task EmptyWallets_Should_ProcessSuccessfully()
    {
        // Arrange
        var token = "valid-token-without-wallets";
        var userId = Guid.NewGuid().ToString();
        
        var dynamicUserData = AuthenticationTestFixtures.ValidDynamicUserData(
            userId: userId,
            wallets: new List<WalletData>()); // Empty wallet list

        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("iss", "https://app.dynamic.xyz/test-env"),
            new Claim("sub", userId)
        }));

        DynamicAuthService.ValidateTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(Result.Success<DynamicUserData, Error>(dynamicUserData));
        
        DynamicAuthService.GetRawClaimsAsync(token, Arg.Any<CancellationToken>())
            .Returns(Result.Success<ClaimsPrincipal, Error>(claimsPrincipal));

        // Mock: FindByCredentialAsync returns null (no existing principal)
        PrincipalRepo.FindByCredentialAsync(
            Arg.Any<ProviderType>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AxonPrincipal?>(null));

        var request = new DynamicExchangeRequest(token);

        // Act
        var result = await Provider.AuthenticateAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.AdditionalClaims["wallets_processed"].ShouldBe(0);
        result.Value.AdditionalClaims["wallets_linked"].ShouldBe(0);
        
        // Should NOT have called resolution service (no wallets)
        await ResolutionService.DidNotReceive().ResolveAsync(
            Arg.Any<ProviderType>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<ChainId>(),
            Arg.Any<Address>(),
            Arg.Any<CancellationToken>());
    }

    #endregion
}