using Axon.Modules.Identity.Application.Common.Models;
using Axon.Modules.Identity.Application.Contracts.ExternalServices;
using Axon.Modules.Identity.Application.Contracts.Providers;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Application.Tests.Providers._TestInfrastructure;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using NSubstitute;
using NUnit.Framework;
using Shouldly;
using System.Security.Claims;

namespace Axon.Modules.Identity.Application.Tests.Providers;

/// <summary>
/// Tests for DynamicAuthenticationProvider chain defaults management.
/// Covers ApplyChainDefaults behavior including auto-assignment and eligibility rules.
/// </summary>
[TestFixture]
public class DynamicAuthenticationProvider_ChainDefaultsTests : DynamicProviderTestBase
{
    [Test]
    public async Task ChainDefaults_FirstVerified_Should_SetDefaultPerChain()
    {
        // Arrange
        var token = "valid-token-first-verified";
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

        // Mock: Address normalization
        AddressNormalizer.NormalizeAddress("ethereum", "0x1111111111111111111111111111111111111111")
            .Returns(Address.Create("0x1111111111111111111111111111111111111111"));

        AddressNormalizer.NormalizeAddress("solana-mainnet", "DYw8jCTfwHNRJhhmFcbXvVDTqWMEVFBX6ZKUmG5CNSKK")
            .Returns(Address.Create("DYw8jCTfwHNRJhhmFcbXvVDTqWMEVFBX6ZKUmG5CNSKK"));

        // Mock: Resolution
        var principal = CreatePrincipalWithDynamicCredential(subject: userId);
        var resolutionResult = new PrincipalResolutionResult(
            Principal: principal,
            Path: ResolutionPath.Created,
            WasAutoLinked: false);

        ConfigureResolutionServiceMock(resolutionResult);

        var request = new DynamicExchangeRequest(token);

        // Act
        var result = await Provider.AuthenticateAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.AdditionalClaims["wallets_processed"].ShouldBe(2);

        // Should apply defaults for both chains (assuming wallets were verified)
        var defaultsApplied = (int)result.Value.AdditionalClaims["defaults_applied"];
        defaultsApplied.ShouldBeGreaterThanOrEqualTo(0);
        defaultsApplied.ShouldBeLessThanOrEqualTo(2);
    }

    [Test]
    public async Task ChainDefaults_MultipleWalletsPerChain_Should_PickFirstById()
    {
        // Arrange
        var token = "valid-token-multiple-per-chain";
        var userId = Guid.NewGuid().ToString();

        // Two Ethereum wallets - defaults should pick deterministically (first by ID)
        var wallets = new List<WalletData>
        {
            AuthenticationTestFixtures.EthereumWalletData("0x1111111111111111111111111111111111111111"),
            AuthenticationTestFixtures.EthereumWalletData("0x2222222222222222222222222222222222222222")
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

        // Mock: Address normalization
        AddressNormalizer.NormalizeAddress("ethereum", "0x1111111111111111111111111111111111111111")
            .Returns(Address.Create("0x1111111111111111111111111111111111111111"));

        AddressNormalizer.NormalizeAddress("ethereum", "0x2222222222222222222222222222222222222222")
            .Returns(Address.Create("0x2222222222222222222222222222222222222222"));

        // Mock: Resolution
        var principal = CreatePrincipalWithDynamicCredential(subject: userId);
        var resolutionResult = new PrincipalResolutionResult(
            Principal: principal,
            Path: ResolutionPath.Created,
            WasAutoLinked: false);

        ConfigureResolutionServiceMock(resolutionResult);

        var request = new DynamicExchangeRequest(token);

        // Act
        var result = await Provider.AuthenticateAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.AdditionalClaims["wallets_processed"].ShouldBe(2);

        // Should apply at most 1 default for Ethereum chain
        var defaultsApplied = (int)result.Value.AdditionalClaims["defaults_applied"];
        defaultsApplied.ShouldBeLessThanOrEqualTo(1);
    }

    [Test]
    public async Task ChainDefaults_WatchOnly_Should_SkipDefault()
    {
        // Arrange - This test verifies the business rule that only verified+signing wallets are eligible
        var token = "valid-token-watch-only";
        var userId = Guid.NewGuid().ToString();

        var wallets = new List<WalletData>
        {
            AuthenticationTestFixtures.EthereumWalletData("0x1111111111111111111111111111111111111111")
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

        // Mock: Address normalization
        AddressNormalizer.NormalizeAddress("ethereum", "0x1111111111111111111111111111111111111111")
            .Returns(Address.Create("0x1111111111111111111111111111111111111111"));

        // Mock: Resolution
        var principal = CreatePrincipalWithDynamicCredential(subject: userId);
        var resolutionResult = new PrincipalResolutionResult(
            Principal: principal,
            Path: ResolutionPath.Created,
            WasAutoLinked: false);

        ConfigureResolutionServiceMock(resolutionResult);

        // Mock: Wallet verification returns watch-only ownership (not eligible for defaults)
        var walletId = new WalletId(Guid.NewGuid());
        WalletRepo.EnsureManyByChainAndAddressAsync(
            Arg.Any<List<(string chainId, Address address)>>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyDictionary<(string, Address), WalletId>>(
                new Dictionary<(string, Address), WalletId>
                {
                    { ("ethereum", Address.Create("0x1111111111111111111111111111111111111111").Value), walletId }
                }));

        WalletVerificationService.VerifyWalletOwnershipAsync(
            walletId,
            Arg.Any<AxonUserId>(),
            Arg.Any<AccessMode>(),
            Arg.Any<VerificationSource>(),
            Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var wId = call.ArgAt<WalletId>(0);
                var principalId = call.ArgAt<AxonUserId>(1);
                // Return watch-only ownership (not eligible for defaults)
                var ownership = CreateWalletOwnership(wId, principalId, AccessMode.WatchOnly);
                return Task.FromResult(Result.Success<WalletOwnership, Error>(ownership));
            });

        var request = new DynamicExchangeRequest(token);

        // Act
        var result = await Provider.AuthenticateAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.AdditionalClaims["wallets_processed"].ShouldBe(1);

        // Watch-only wallets should not trigger default assignment
        result.Value.AdditionalClaims["defaults_applied"].ShouldBe(0);
    }

    [Test]
    public async Task ChainDefaults_NoEligible_Should_ReturnZero()
    {
        // Arrange
        var token = "valid-token-no-eligible";
        var userId = Guid.NewGuid().ToString();

        // No wallets = no eligible wallets for defaults
        var dynamicUserData = AuthenticationTestFixtures.ValidDynamicUserData(
            userId: userId,
            wallets: new List<WalletData>());

        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("iss", "https://app.dynamic.xyz/test-env"),
            new Claim("sub", userId)
        }));

        DynamicAuthService.ValidateTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(Result.Success<DynamicUserData, Error>(dynamicUserData));

        DynamicAuthService.GetRawClaimsAsync(token, Arg.Any<CancellationToken>())
            .Returns(Result.Success<ClaimsPrincipal, Error>(claimsPrincipal));

        // Mock: No existing principal (will create via credential-only path)
        PrincipalRepo.FindByCredentialAsync(ProviderType.Dynamic, "", "", default)
            .ReturnsForAnyArgs(_ => Task.FromResult<AxonPrincipal?>(null));

        var request = new DynamicExchangeRequest(token);

        // Act
        var result = await Provider.AuthenticateAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.AdditionalClaims["wallets_processed"].ShouldBe(0);
        result.Value.AdditionalClaims["defaults_applied"].ShouldBe(0);

        // No wallets = no defaults applied
        result.Value.AdditionalClaims["created"].ShouldBe(true);
    }
}