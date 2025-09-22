using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.Errors;
using Axon.Modules.Identity.Domain.Tests.Common;
using Axon.Modules.Identity.Domain.Tests.TestData;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Domain.Tests.Aggregates;

/// <summary>
/// Stabilized test suite for AxonPrincipal aggregate using actual domain API.
/// </summary>
[TestFixture]
public class AxonPrincipalStabilizedTests : IdentityTestBase
{
    private static readonly Func<string, string, string, Result<bool, Error>> NoCredentialConflict = 
        (p, i, s) => Result.Success<bool, Error>(false);
    
    private static readonly Func<string, string, string, Result<bool, Error>> CredentialConflict = 
        (p, i, s) => Result.Success<bool, Error>(true);

    [TestCase(PrincipalType.Human)]
    [TestCase(PrincipalType.Service)]
    public void CreatePrincipal_ShouldHaveCorrectDefaults(PrincipalType type)
    {
        // Act
        var principal = CreatePrincipal(type);

        // Assert
        principal.ShouldNotBeNull();
        principal.Id.ShouldNotBe(default(AxonUserId));
        principal.Type.ShouldBe(type);
        principal.RiskTier.ShouldBe(RiskTier.Low);
        principal.Credentials.ShouldBeEmpty();
        principal.WalletOwnerships.ShouldBeEmpty();
    }

    [TestCase(PrincipalType.Human, RiskTier.Low, RiskTier.Medium, true)]
    [TestCase(PrincipalType.Human, RiskTier.Medium, RiskTier.High, true)]
    [TestCase(PrincipalType.Service, RiskTier.Low, RiskTier.Medium, false)]
    [TestCase(PrincipalType.Service, RiskTier.Low, RiskTier.High, false)]
    public void UpdateRiskTier_ShouldRespectBusinessRules(
        PrincipalType type, RiskTier current, RiskTier newTier, bool shouldSucceed)
    {
        // Arrange
        var principal = CreatePrincipal(type);
        if (current != RiskTier.Low)
            principal.UpdateRiskTier(current);

        // Act
        var result = principal.UpdateRiskTier(newTier);

        // Assert
        result.IsSuccess.ShouldBe(shouldSucceed);
        if (shouldSucceed)
            principal.RiskTier.ShouldBe(newTier);
        else
            principal.RiskTier.ShouldBe(current);
    }

    [Test]
    public void UpdateRiskTier_WithSameValue_ShouldBeNoOp()
    {
        // Arrange
        var principal = CreatePrincipal();
        principal.UpdateRiskTier(RiskTier.Medium);
        var eventCount = principal.DomainEvents.Count;

        // Act
        var result = principal.UpdateRiskTier(RiskTier.Medium);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        principal.DomainEvents.Count.ShouldBe(eventCount); // No new event
    }

    [Test]
    public void LinkWalletOwnership_WithValidOwnership_ShouldSucceed()
    {
        // Arrange
        var principal = CreatePrincipal();
        var ownership = CreateOwnership(principal.Id);

        // Act
        var result = principal.LinkWalletOwnership(ownership, NoConflictResolver);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        principal.WalletOwnerships.ShouldContain(ownership);
    }

    [Test]
    public void LinkWalletOwnership_WithVerifiedSigningConflict_ShouldFail()
    {
        // Arrange
        var principal = CreatePrincipal();
        var ownership = CreateOwnership(principal.Id, status: OwnershipStatus.Verified, accessMode: AccessMode.Signing);

        // Act
        var result = principal.LinkWalletOwnership(ownership, ConflictResolver);

        // Assert
        result.IsFailure.ShouldBeTrue();
        principal.WalletOwnerships.ShouldNotContain(ownership);
    }

    [Test]
    public void LinkWalletOwnership_Duplicate_ShouldBeIdempotent()
    {
        // Arrange
        var principal = CreatePrincipal();
        var ownership = CreateOwnership(principal.Id);
        principal.LinkWalletOwnership(ownership, NoConflictResolver);
        var count = principal.WalletOwnerships.Count;

        // Act
        var result = principal.LinkWalletOwnership(ownership, NoConflictResolver);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        principal.WalletOwnerships.Count.ShouldBe(count); // No duplicate
    }

    [Test]
    public void AddCredential_WithValidCredential_ShouldSucceed()
    {
        // Arrange
        var principal = CreatePrincipal();
        var credential = CreateCredential(principal.Id);

        // Act
        var result = principal.AddCredential(credential, NoCredentialConflict);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        principal.Credentials.ShouldContain(credential);
    }

    [Test]
    public void AddCredential_WithConflict_ShouldFail()
    {
        // Arrange
        var principal = CreatePrincipal();
        var credential = CreateCredential(principal.Id);

        // Act
        var result = principal.AddCredential(credential, CredentialConflict);

        // Assert
        result.IsFailure.ShouldBeTrue();
        principal.Credentials.ShouldNotContain(credential);
    }

    [Test]
    public void AddCredential_Duplicate_ShouldBeIdempotent()
    {
        // Arrange
        var principal = CreatePrincipal();
        var credential = CreateCredential(principal.Id);
        principal.AddCredential(credential, NoCredentialConflict);
        var count = principal.Credentials.Count;

        // Act
        var result = principal.AddCredential(credential, NoCredentialConflict);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        principal.Credentials.Count.ShouldBe(count); // No duplicate
    }

    [Test]
    public void ApplyChainDefault_WithOwnedWallet_ShouldSucceed()
    {
        // Arrange
        var principal = CreatePrincipal();
        var walletId = WalletId.New();
        var ownership = WalletOwnership.Create(principal.Id, walletId, AccessMode.Signing, OwnershipStatus.Verified);
        principal.LinkWalletOwnership(ownership, NoConflictResolver);

        // Act
        var result = principal.ApplyChainDefault(NetworkEnvironment.Mainnet, TestConstants.SolanaChain, walletId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Test]
    public void ApplyChainDefault_WithUnownedWallet_ShouldFail()
    {
        // Arrange
        var principal = CreatePrincipal();
        var walletId = WalletId.New();

        // Act
        var result = principal.ApplyChainDefault(NetworkEnvironment.Mainnet, TestConstants.SolanaChain, walletId);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }
}