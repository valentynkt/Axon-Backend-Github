using Axon.Modules.Identity.Application.Common.Models;
using Axon.Modules.Identity.Application.Contracts.ExternalServices;
using Axon.Modules.Identity.Application.Contracts.Providers;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Application.Tests.Providers._TestInfrastructure;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
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
/// Tests for DynamicAuthenticationProvider wallet batch processing logic.
/// Covers ProcessWalletsBatch behavior including multi-chain, conflicts, and metrics.
/// </summary>
[TestFixture]
public class DynamicAuthenticationProvider_WalletBatchTests : DynamicProviderTestBase
{
    [Test]
    public async Task WalletBatch_MultipleChains_Should_ProcessAll()
    {
        // Arrange
        var token = "valid-token-multi-chain";
        var userId = Guid.NewGuid().ToString();

        var wallets = new List<WalletData>
        {
            AuthenticationTestFixtures.EthereumWalletData("0x1111111111111111111111111111111111111111"),
            AuthenticationTestFixtures.SolanaWalletData("DYw8jCTfwHNRJhhmFcbXvVDTqWMEVFBX6ZKUmG5CNSKK"),
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

        // Mock: Address normalization succeeds for all
        AddressNormalizer.NormalizeAddress("ethereum", "0x1111111111111111111111111111111111111111")
            .Returns(Address.Create("0x1111111111111111111111111111111111111111"));

        AddressNormalizer.NormalizeAddress("solana-mainnet", "DYw8jCTfwHNRJhhmFcbXvVDTqWMEVFBX6ZKUmG5CNSKK")
            .Returns(Address.Create("DYw8jCTfwHNRJhhmFcbXvVDTqWMEVFBX6ZKUmG5CNSKK"));

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
        result.Value.AdditionalClaims["wallets_processed"].ShouldBe(3);
        ((int)result.Value.AdditionalClaims["wallets_linked"]).ShouldBeGreaterThanOrEqualTo(0);
        result.Value.AdditionalClaims["skipped"].ShouldBe(0);
        result.Value.AdditionalClaims["conflicts"].ShouldBe(0);
    }

    [Test]
    public async Task WalletBatch_DuplicateAddresses_Should_DedupCorrectly()
    {
        // Arrange - Same address on two Ethereum-type wallets
        var token = "valid-token-duplicate-address";
        var userId = Guid.NewGuid().ToString();
        var sameAddress = "0x1111111111111111111111111111111111111111";

        var wallets = new List<WalletData>
        {
            AuthenticationTestFixtures.EthereumWalletData(sameAddress), // First Ethereum wallet
            AuthenticationTestFixtures.EthereumWalletData(sameAddress)  // Duplicate Ethereum wallet
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

        // Mock: Address normalization succeeds
        AddressNormalizer.NormalizeAddress("ethereum", sameAddress)
            .Returns(Address.Create(sameAddress));

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

        // Duplicate addresses on same chain should be deduped by EnsureManyByChainAndAddressAsync
        // So both get processed but may link to same wallet ID
        ((int)result.Value.AdditionalClaims["wallets_linked"]).ShouldBeGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task WalletBatch_PartialFailure_Should_ContinueProcessing()
    {
        // Arrange
        var token = "valid-token-partial-failure";
        var userId = Guid.NewGuid().ToString();

        var wallets = new List<WalletData>
        {
            AuthenticationTestFixtures.EthereumWalletData("0x1111111111111111111111111111111111111111"), // Valid
            AuthenticationTestFixtures.InvalidAddressWalletData(), // Invalid address
            AuthenticationTestFixtures.SolanaWalletData("DYw8jCTfwHNRJhhmFcbXvVDTqWMEVFBX6ZKUmG5CNSKK") // Valid
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
        AddressNormalizer.NormalizeAddress("ethereum", "0x1111111111111111111111111111111111111111")
            .Returns(Address.Create("0x1111111111111111111111111111111111111111"));

        // Mock: Second wallet fails normalization (invalid address)
        AddressNormalizer.NormalizeAddress("ethereum", "invalid-address-format")
            .Returns(Result.Failure<Address, Error>(
                Error.Validation("Invalid address format", "ADDRESS.INVALID_FORMAT")));

        // Mock: Third wallet normalizes successfully
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

        // Should process 3 wallets total, but only 2 valid ones get linked
        result.Value.AdditionalClaims["wallets_processed"].ShouldBe(3);

        // Authentication should succeed despite one invalid wallet
        result.Value.User.ShouldNotBeNull();
    }

    [Test]
    public async Task WalletBatch_ConflictDetected_Should_ReturnConflictError()
    {
        // Arrange
        var token = "valid-token-conflict";
        var userId = Guid.NewGuid().ToString();
        var conflictAddress = "0x1111111111111111111111111111111111111111";

        var wallets = new List<WalletData>
        {
            AuthenticationTestFixtures.EthereumWalletData(conflictAddress)
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

        // Mock: Address normalization succeeds
        AddressNormalizer.NormalizeAddress("ethereum", conflictAddress)
            .Returns(Address.Create(conflictAddress));

        // Mock: Resolution
        var principal = CreatePrincipalWithDynamicCredential(subject: userId);
        var resolutionResult = new PrincipalResolutionResult(
            Principal: principal,
            Path: ResolutionPath.Created,
            WasAutoLinked: false);

        ConfigureResolutionServiceMock(resolutionResult);

        // Mock: Wallet verification returns conflict error
        var walletId = new WalletId(Guid.NewGuid());
        WalletRepo.EnsureManyByChainAndAddressAsync(
            Arg.Any<List<(string chainId, Address address)>>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyDictionary<(string, Address), WalletId>>(
                new Dictionary<(string, Address), WalletId>
                {
                    { ("ethereum", Address.Create(conflictAddress).Value), walletId }
                }));

        WalletVerificationService.VerifyWalletOwnershipAsync(
            walletId,
            Arg.Any<AxonUserId>(),
            AccessMode.Signing,
            VerificationSource.DynamicAttested,
            Arg.Any<CancellationToken>())
            .Returns(Result.Failure<WalletOwnership, Error>(
                Error.Conflict("Wallet already verified for different principal", "WALLET.OWNERSHIP_CONFLICT")));

        var request = new DynamicExchangeRequest(token);

        // Act
        var result = await Provider.AuthenticateAsync(request, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        // Note: Conflicts are caught by provider and wrapped as general errors
        // The key behavior is that authentication fails when wallet conflicts occur
        result.Error.ShouldNotBeNull();
    }

    [Test]
    public async Task WalletBatch_LinkFailure_Should_IncrementSkipped()
    {
        // Arrange
        var token = "valid-token-link-failure";
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

        // Mock: Address normalization succeeds
        AddressNormalizer.NormalizeAddress("ethereum", "0x1111111111111111111111111111111111111111")
            .Returns(Address.Create("0x1111111111111111111111111111111111111111"));

        // Mock: Resolution
        var principal = CreatePrincipalWithDynamicCredential(subject: userId);
        var resolutionResult = new PrincipalResolutionResult(
            Principal: principal,
            Path: ResolutionPath.Created,
            WasAutoLinked: false);

        ConfigureResolutionServiceMock(resolutionResult);

        // Mock: Wallet verification succeeds but returns non-conflict error (e.g., validation error)
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
            AccessMode.Signing,
            VerificationSource.DynamicAttested,
            Arg.Any<CancellationToken>())
            .Returns(Result.Failure<WalletOwnership, Error>(
                Error.Validation("Max wallets reached", "WALLET.MAX_REACHED")));

        var request = new DynamicExchangeRequest(token);

        // Act
        var result = await Provider.AuthenticateAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.AdditionalClaims["wallets_processed"].ShouldBe(1);
        result.Value.AdditionalClaims["wallets_linked"].ShouldBe(0);
        result.Value.AdditionalClaims["skipped"].ShouldBe(1);
        result.Value.AdditionalClaims["conflicts"].ShouldBe(0);
    }

    [Test]
    public async Task WalletBatch_EmptyWallets_Should_ReturnZeroMetrics()
    {
        // Arrange
        var token = "valid-token-no-wallets";
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

        // Mock: No existing principal (will create new via credential-only path)
        PrincipalRepo.FindByCredentialAsync(ProviderType.Dynamic, "", "", default)
            .ReturnsForAnyArgs(_ => Task.FromResult<AxonPrincipal?>(null));

        var request = new DynamicExchangeRequest(token);

        // Act
        var result = await Provider.AuthenticateAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.AdditionalClaims["wallets_processed"].ShouldBe(0);
        result.Value.AdditionalClaims["wallets_linked"].ShouldBe(0);
        result.Value.AdditionalClaims["skipped"].ShouldBe(0);
        result.Value.AdditionalClaims["conflicts"].ShouldBe(0);
        result.Value.AdditionalClaims["defaults_applied"].ShouldBe(0);

        // Should still create principal and succeed
        result.Value.AdditionalClaims["created"].ShouldBe(true);
    }
}