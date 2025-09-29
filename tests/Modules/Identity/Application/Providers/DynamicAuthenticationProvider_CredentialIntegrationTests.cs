using Axon.Modules.Identity.Application.Contracts.ExternalServices;
using Axon.Modules.Identity.Application.Contracts.Providers;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Application.Tests.Providers._TestInfrastructure;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using NSubstitute;
using NUnit.Framework;
using Shouldly;
using System.Security.Claims;

namespace Axon.Modules.Identity.Application.Tests.Providers;

/// <summary>
/// Integration tests for DynamicAuthenticationProvider credential management flow.
/// Tests the interaction between provider and credential management without mocking infrastructure details.
/// Focuses on observable behavior rather than implementation verification.
/// </summary>
[TestFixture]
public class DynamicAuthenticationProvider_CredentialIntegrationTests : DynamicProviderTestBase
{
    [Test]
    public async Task Authentication_WithExistingCredential_Should_Succeed()
    {
        // Arrange - User authenticates with Dynamic credential that already exists
        var token = "valid-token-existing-credential";
        var userId = Guid.NewGuid().ToString();
        var issuer = "https://app.dynamic.xyz/test-env";

        var dynamicUserData = AuthenticationTestFixtures.ValidDynamicUserData(
            userId: userId,
            wallets: new List<WalletData>()); // No wallets = credential-only path

        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("iss", issuer),
            new Claim("sub", userId)
        }));

        DynamicAuthService.ValidateTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(Result.Success<DynamicUserData, Error>(dynamicUserData));

        DynamicAuthService.GetRawClaimsAsync(token, Arg.Any<CancellationToken>())
            .Returns(Result.Success<ClaimsPrincipal, Error>(claimsPrincipal));

        // Mock: Existing principal with matching Dynamic credential
        var principal = CreatePrincipalWithDynamicCredential(
            providerType: ProviderType.Dynamic,
            issuer: issuer,
            subject: userId);

        // Mock: Repository finds existing principal by credential (no-wallet path line 271)
        PrincipalRepo.FindByCredentialAsync(ProviderType.Dynamic, issuer, userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AxonPrincipal?>(principal));

        var request = new DynamicExchangeRequest(token);

        // Act
        var result = await Provider.AuthenticateAsync(request, CancellationToken.None);

        // Assert - Focus on observable outcomes
        result.IsSuccess.ShouldBeTrue();
        result.Value.User.ShouldNotBeNull();
        result.Value.ProviderType.ShouldBe("dynamic");

        // Verify authentication succeeded without errors
        result.Value.AdditionalClaims["dynamic_user_id"].ShouldBe(userId);
        result.Value.AdditionalClaims["created"].ShouldBe(false); // Existing principal

        // Business logic outcome: Credential management handled successfully
        // (UpdateLastSeen called internally, but we don't verify implementation)
    }

    [Test]
    public async Task Authentication_NewCredentialForExistingPrincipal_Should_Succeed()
    {
        // Arrange - Wallet-based principal wants to add Dynamic credential
        var token = "valid-token-new-credential-for-existing";
        var userId = Guid.NewGuid().ToString();
        var issuer = "https://app.dynamic.xyz/test-env";

        // User has wallets, so resolution will use wallet path
        var wallets = new List<WalletData>
        {
            AuthenticationTestFixtures.EthereumWalletData("0x1111111111111111111111111111111111111111")
        };

        var dynamicUserData = AuthenticationTestFixtures.ValidDynamicUserData(
            userId: userId,
            wallets: wallets);

        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("iss", issuer),
            new Claim("sub", userId)
        }));

        DynamicAuthService.ValidateTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(Result.Success<DynamicUserData, Error>(dynamicUserData));

        DynamicAuthService.GetRawClaimsAsync(token, Arg.Any<CancellationToken>())
            .Returns(Result.Success<ClaimsPrincipal, Error>(claimsPrincipal));

        // Mock: Address normalization
        AddressNormalizer.NormalizeAddress("ethereum", "0x1111111111111111111111111111111111111111")
            .Returns(Address.Create("0x1111111111111111111111111111111111111111"));

        // Mock: Existing principal found via wallet (not credential)
        var principal = CreatePrincipalWithDynamicCredential(
            providerType: ProviderType.Dynamic,
            issuer: "https://different-issuer.com", // Different credential
            subject: "different-subject");

        var resolutionResult = new PrincipalResolutionResult(
            Principal: principal,
            Path: ResolutionPath.Wallet, // Found via wallet, not credential
            WasAutoLinked: false);

        ConfigureResolutionServiceMock(resolutionResult);

        // Mock: Credential is not taken by another principal
        PrincipalRepo.IsCredentialTakenAsync(ProviderType.Dynamic, "", "", default)
            .ReturnsForAnyArgs(Task.FromResult(false));

        var request = new DynamicExchangeRequest(token);

        // Act
        var result = await Provider.AuthenticateAsync(request, CancellationToken.None);

        // Assert - Focus on observable outcomes
        result.IsSuccess.ShouldBeTrue();
        result.Value.User.ShouldNotBeNull();
        result.Value.ProviderType.ShouldBe("dynamic");

        // Verify authentication succeeded
        result.Value.AdditionalClaims["dynamic_user_id"].ShouldBe(userId);
        result.Value.AdditionalClaims["created"].ShouldBe(false); // Existing principal
        result.Value.AdditionalClaims["wallets_processed"].ShouldBe(1);

        // Business logic outcome: New Dynamic credential added to existing principal
        // (AddCredential called internally with IsCredentialTaken check)
        // We verify the happy path succeeds without verifying internal implementation
    }
}