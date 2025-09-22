using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.Events;
using Axon.Modules.Identity.Domain.Tests.Common;
using Axon.Modules.Identity.Domain.Tests.TestData;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Shouldly;

namespace Axon.Modules.Identity.Domain.Tests.Aggregates;

/// <summary>
/// Comprehensive test suite for AxonPrincipal aggregate root.
/// Tests domain logic, business rules, command operations, and event publishing.
/// </summary>
[TestFixture]
public class AxonPrincipalTests : IdentityTestBase
{
    #region Creation Tests

    [TestFixture]
    public class CreateHumanTests : AxonPrincipalTests
    {
        [Test]
        public void CreateHuman_WithDefaultId_Should_CreateValidPrincipal()
        {
            // Act
            var principal = AxonPrincipal.CreateHuman();

            // Assert
            principal.ShouldSatisfyAllConditions(
                p => p.Id.Value.ShouldNotBe(Guid.Empty),
                p => p.Type.ShouldBe(PrincipalType.Human),
                p => p.RiskTier.ShouldBe(RiskTier.Low),
                p => p.Credentials.ShouldBeEmpty(),
                p => p.WalletOwnerships.ShouldBeEmpty(),
                p => p.PrincipalChainDefaults.ShouldBeEmpty()
            );
        }

        [Test]
        public void CreateHuman_WithCustomId_Should_UseProvidedId()
        {
            // Arrange
            var customId = AxonUserId.New();

            // Act
            var principal = AxonPrincipal.CreateHuman(customId);

            // Assert
            principal.Id.ShouldBe(customId);
            principal.Type.ShouldBe(PrincipalType.Human);
        }
    }

    [TestFixture]
    public class CreateServiceTests : AxonPrincipalTests
    {
        [Test]
        public void CreateService_WithDefaultId_Should_CreateValidPrincipal()
        {
            // Act
            var principal = AxonPrincipal.CreateService();

            // Assert
            principal.ShouldSatisfyAllConditions(
                p => p.Id.Value.ShouldNotBe(Guid.Empty),
                p => p.Type.ShouldBe(PrincipalType.Service),
                p => p.RiskTier.ShouldBe(RiskTier.Low),
                p => p.Credentials.ShouldBeEmpty(),
                p => p.WalletOwnerships.ShouldBeEmpty(),
                p => p.PrincipalChainDefaults.ShouldBeEmpty()
            );
        }

        [Test]
        public void CreateService_WithCustomId_Should_UseProvidedId()
        {
            // Arrange
            var customId = AxonUserId.New();

            // Act
            var principal = AxonPrincipal.CreateService(customId);

            // Assert
            principal.Id.ShouldBe(customId);
            principal.Type.ShouldBe(PrincipalType.Service);
        }
    }

    [TestFixture]
    public class CreateWithDynamicCredentialTests : AxonPrincipalTests
    {
        [Test]
        public void CreateWithDynamicCredential_WithValidParameters_Should_CreatePrincipalWithCredential()
        {
            // Arrange
            var providerType = ProviderType.Create("dynamic").Value;
            var issuer = "test-issuer";
            var subject = "test-subject";

            // Act
            var result = AxonPrincipal.CreateWithDynamicCredential(providerType, issuer, subject);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            var principal = result.Value;

            principal.ShouldSatisfyAllConditions(
                p => p.Type.ShouldBe(PrincipalType.Human),
                p => p.RiskTier.ShouldBe(RiskTier.Low),
                p => p.Credentials.ShouldHaveSingleItem()
            );

            var credential = principal.Credentials.First();
            credential.ShouldSatisfyAllConditions(
                c => c.Provider.ShouldBe("dynamic"),
                c => c.Issuer.ShouldBe(issuer),
                c => c.Subject.ShouldBe(subject),
                c => c.PrincipalId.ShouldBe(principal.Id)
            );
        }

        [Test]
        public void CreateWithDynamicCredential_WithCustomId_Should_UseProvidedId()
        {
            // Arrange
            var customId = AxonUserId.New();
            var providerType = ProviderType.Create("dynamic").Value;
            var issuer = "test-issuer";
            var subject = "test-subject";

            // Act
            var result = AxonPrincipal.CreateWithDynamicCredential(providerType, issuer, subject, customId);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.Id.ShouldBe(customId);
        }
    }

    #endregion

    #region Risk Tier Management Tests

    [TestFixture]
    public class UpdateRiskTierTests : AxonPrincipalTests
    {
        [Test]
        public void UpdateRiskTier_WithSameValue_Should_ReturnSuccessWithoutChange()
        {
            // Arrange
            var principal = AxonPrincipal.CreateHuman();
            var initialTier = principal.RiskTier;

            // Act
            var result = principal.UpdateRiskTier(RiskTier.Low);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            principal.RiskTier.ShouldBe(initialTier);
            principal.DomainEvents.ShouldBeEmpty(); // No event should be raised for no-op
        }

        [Test]
        public void UpdateRiskTier_WithDifferentValue_Should_UpdateAndRaiseEvent()
        {
            // Arrange
            var principal = AxonPrincipal.CreateHuman();
            principal.ClearDomainEvents(); // Clear creation events

            // Act
            var result = principal.UpdateRiskTier(RiskTier.Medium);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            principal.RiskTier.ShouldBe(RiskTier.Medium);

            principal.DomainEvents.ShouldHaveSingleItem()
                .ShouldBeOfType<PrincipalChangedEvent>()
                .ShouldSatisfyAllConditions(
                    e => e.PrincipalId.ShouldBe(principal.Id),
                    e => e.PropertyChanged.ShouldBe(nameof(principal.RiskTier)),
                    e => e.OldValue.ShouldBe("Low"),
                    e => e.NewValue.ShouldBe("Medium")
                );
        }

        [Test]
        public void UpdateRiskTier_ServicePrincipalWithNonLowTier_Should_ReturnFailure()
        {
            // Arrange
            var principal = AxonPrincipal.CreateService();

            // Act
            var result = principal.UpdateRiskTier(RiskTier.Medium);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.Validation);
            principal.RiskTier.ShouldBe(RiskTier.Low); // Should remain unchanged
        }

        [Test]
        [TestCase(RiskTier.Low)]
        [TestCase(RiskTier.Medium)]
        [TestCase(RiskTier.High)]
        public void UpdateRiskTier_HumanPrincipal_Should_AcceptAllTiers(RiskTier newTier)
        {
            // Arrange
            var principal = AxonPrincipal.CreateHuman();

            // Act
            var result = principal.UpdateRiskTier(newTier);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            principal.RiskTier.ShouldBe(newTier);
        }
    }

    #endregion

    #region Wallet Ownership Management Tests

    [TestFixture]
    public class LinkWalletOwnershipTests : AxonPrincipalTests
    {
        [Test]
        public void LinkWalletOwnership_WithValidOwnership_Should_AddOwnershipAndRaiseEvent()
        {
            // Arrange
            var principal = AxonPrincipal.CreateHuman();
            principal.ClearDomainEvents();
            var ownership = CreateOwnership(principal.Id, accessMode: AccessMode.Signing, status: OwnershipStatus.Verified);

            // Act
            var result = principal.LinkWalletOwnership(ownership, NoConflictResolver);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            principal.WalletOwnerships.ShouldHaveSingleItem().ShouldBe(ownership);

            principal.DomainEvents.ShouldHaveSingleItem()
                .ShouldBeOfType<OwnershipChangedEvent>()
                .ShouldSatisfyAllConditions(
                    e => e.PrincipalId.ShouldBe(principal.Id),
                    e => e.WalletId.ShouldBe(ownership.WalletId),
                    e => e.ChangeType.ShouldBe("linked"),
                    e => e.AccessMode.ShouldBe("Signing"),
                    e => e.Status.ShouldBe("Verified")
                );
        }

        [Test]
        public void LinkWalletOwnership_WithExistingOwnership_Should_BeIdempotent()
        {
            // Arrange
            var principal = AxonPrincipal.CreateHuman();
            var ownership = CreateOwnership(principal.Id, accessMode: AccessMode.Signing, status: OwnershipStatus.Verified);
            principal.LinkWalletOwnership(ownership, NoConflictResolver);
            principal.ClearDomainEvents();

            // Act
            var result = principal.LinkWalletOwnership(ownership, NoConflictResolver);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            principal.WalletOwnerships.ShouldHaveSingleItem();
            principal.DomainEvents.ShouldBeEmpty(); // No event for idempotent operation
        }

        [Test]
        public void LinkWalletOwnership_WithConflictingVerifiedSigningOwnership_Should_ReturnFailure()
        {
            // Arrange
            var principal = AxonPrincipal.CreateHuman();
            var ownership = CreateOwnership(principal.Id, accessMode: AccessMode.Signing, status: OwnershipStatus.Verified);

            // Act
            var result = principal.LinkWalletOwnership(ownership, ConflictResolver);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.Conflict);
            principal.WalletOwnerships.ShouldBeEmpty();
        }

        [Test]
        public void LinkWalletOwnership_ExceedingMaxWallets_Should_ReturnFailure()
        {
            // Arrange
            var principal = AxonPrincipal.CreateHuman();

            // Add 10 wallets (the maximum)
            for (int i = 0; i < 10; i++)
            {
                var ownership = CreateOwnership(principal.Id, WalletId.New());
                principal.LinkWalletOwnership(ownership, NoConflictResolver);
            }

            var extraOwnership = CreateOwnership(principal.Id, WalletId.New());

            // Act
            var result = principal.LinkWalletOwnership(extraOwnership, NoConflictResolver);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.Validation);
            principal.WalletOwnerships.Count.ShouldBe(10);
        }

        [Test]
        public void LinkWalletOwnership_WithNullOwnership_Should_ThrowArgumentNullException()
        {
            // Arrange
            var principal = AxonPrincipal.CreateHuman();

            // Act & Assert
            Should.Throw<ArgumentNullException>(() =>
                principal.LinkWalletOwnership(null!, NoConflictResolver));
        }

        [Test]
        public void LinkWalletOwnership_WithNullCheckFunction_Should_ThrowArgumentNullException()
        {
            // Arrange
            var principal = AxonPrincipal.CreateHuman();
            var ownership = CreateOwnership(principal.Id);

            // Act & Assert
            Should.Throw<ArgumentNullException>(() =>
                principal.LinkWalletOwnership(ownership, null!));
        }

        [Test]
        public void LinkWalletOwnership_WhenConflictCheckFails_Should_ReturnFailure()
        {
            // Arrange
            var principal = AxonPrincipal.CreateHuman();
            var ownership = CreateOwnership(principal.Id, accessMode: AccessMode.Signing, status: OwnershipStatus.Verified);

            // Act
            var result = principal.LinkWalletOwnership(ownership, ErrorResolver);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.Internal);
            principal.WalletOwnerships.ShouldBeEmpty();
        }
    }

    [TestFixture]
    public class RemoveWalletOwnershipTests : AxonPrincipalTests
    {
        [Test]
        public void RemoveWalletOwnership_WithExistingOwnership_Should_RemoveAndRaiseEvent()
        {
            // Arrange
            var principal = AxonPrincipal.CreateHuman();
            var ownership = CreateOwnership(principal.Id);
            principal.LinkWalletOwnership(ownership, NoConflictResolver);
            principal.ClearDomainEvents();

            // Act
            var result = principal.RemoveWalletOwnership(ownership.WalletId);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            principal.WalletOwnerships.ShouldBeEmpty();

            principal.DomainEvents.ShouldHaveSingleItem()
                .ShouldBeOfType<OwnershipChangedEvent>()
                .ShouldSatisfyAllConditions(
                    e => e.PrincipalId.ShouldBe(principal.Id),
                    e => e.WalletId.ShouldBe(ownership.WalletId),
                    e => e.ChangeType.ShouldBe("removed")
                );
        }

        [Test]
        public void RemoveWalletOwnership_WithNonExistentOwnership_Should_ReturnFailure()
        {
            // Arrange
            var principal = AxonPrincipal.CreateHuman();
            var nonExistentWalletId = WalletId.New();

            // Act
            var result = principal.RemoveWalletOwnership(nonExistentWalletId);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.NotFound);
        }

        [Test]
        public void RemoveWalletOwnership_ShouldRemoveFromChainDefaults()
        {
            // Arrange
            var principal = AxonPrincipal.CreateHuman();
            var ownership = CreateOwnership(principal.Id, accessMode: AccessMode.Signing, status: OwnershipStatus.Verified);
            var walletId = ownership.WalletId;

            principal.LinkWalletOwnership(ownership, NoConflictResolver);
            principal.ApplyChainDefault(NetworkEnvironment.Mainnet, "ethereum-mainnet", walletId);
            principal.ClearDomainEvents();

            // Act
            var result = principal.RemoveWalletOwnership(walletId);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            principal.WalletOwnerships.ShouldBeEmpty();
            principal.PrincipalChainDefaults.ShouldBeEmpty();
        }
    }

    #endregion

    #region Credential Management Tests

    [TestFixture]
    public class AddCredentialTests : AxonPrincipalTests
    {
        [Test]
        public void AddCredential_WithValidCredential_Should_AddAndRaiseEvent()
        {
            // Arrange
            var principal = AxonPrincipal.CreateHuman();
            principal.ClearDomainEvents();
            var credential = CreateCredential(principal.Id, provider: "github", issuer: "github.com", subject: "user123");

            Func<string, string, string, Result<bool, Error>> uniquenessCheck = (_, _, _) => Result.Success<bool, Error>(false);

            // Act
            var result = principal.AddCredential(credential, uniquenessCheck);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            principal.Credentials.ShouldHaveSingleItem().ShouldBe(credential);

            principal.DomainEvents.ShouldHaveSingleItem()
                .ShouldBeOfType<CredentialChangedEvent>()
                .ShouldSatisfyAllConditions(
                    e => e.PrincipalId.ShouldBe(principal.Id),
                    e => e.CredentialId.ShouldBe(credential.Id),
                    e => e.ChangeType.ShouldBe("added"),
                    e => e.Provider.ShouldBe("github")
                );
        }

        [Test]
        public void AddCredential_WithExistingCredential_Should_BeIdempotentAndUpdateLastSeen()
        {
            // Arrange
            var principal = AxonPrincipal.CreateHuman();
            var credential = CreateCredential(principal.Id, provider: "github", issuer: "github.com", subject: "user123");

            Func<string, string, string, Result<bool, Error>> uniquenessCheck = (_, _, _) => Result.Success<bool, Error>(false);
            principal.AddCredential(credential, uniquenessCheck);

            var originalLastSeen = credential.LastSeenAt;
            var newerCredential = CreateCredential(principal.Id, provider: "github", issuer: "github.com", subject: "user123",
                timestamp: DateTime.UtcNow.AddMinutes(5));

            principal.ClearDomainEvents();

            // Act
            var result = principal.AddCredential(newerCredential, uniquenessCheck);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            principal.Credentials.ShouldHaveSingleItem();
            principal.Credentials.First().LastSeenAt.ShouldBeGreaterThan(originalLastSeen);
            principal.DomainEvents.ShouldBeEmpty(); // No event for idempotent operation
        }

        [Test]
        public void AddCredential_WhenCredentialBelongsToOtherPrincipal_Should_ReturnFailure()
        {
            // Arrange
            var principal = AxonPrincipal.CreateHuman();
            var credential = CreateCredential(principal.Id);

            Func<string, string, string, Result<bool, Error>> uniquenessCheck = (_, _, _) => Result.Success<bool, Error>(true);

            // Act
            var result = principal.AddCredential(credential, uniquenessCheck);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.Conflict);
            principal.Credentials.ShouldBeEmpty();
        }

        [Test]
        public void AddCredential_WhenUniquenessCheckFails_Should_ReturnFailure()
        {
            // Arrange
            var principal = AxonPrincipal.CreateHuman();
            var credential = CreateCredential(principal.Id);

            Func<string, string, string, Result<bool, Error>> uniquenessCheck = (_, _, _) =>
                Result.Failure<bool, Error>(Error.Failure("Database error", "DB_ERROR"));

            // Act
            var result = principal.AddCredential(credential, uniquenessCheck);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.Internal);
            principal.Credentials.ShouldBeEmpty();
        }
    }

    #endregion

    #region Chain Default Management Tests

    [TestFixture]
    public class ApplyChainDefaultTests : AxonPrincipalTests
    {
        [Test]
        public void ApplyChainDefault_WithVerifiedSigningWallet_Should_SetDefaultAndRaiseEvent()
        {
            // Arrange
            var principal = AxonPrincipal.CreateHuman();
            var ownership = CreateOwnership(principal.Id, accessMode: AccessMode.Signing, status: OwnershipStatus.Verified);
            principal.LinkWalletOwnership(ownership, NoConflictResolver);
            principal.ClearDomainEvents();

            // Act
            var result = principal.ApplyChainDefault(NetworkEnvironment.Mainnet, "ethereum-mainnet", ownership.WalletId);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            principal.PrincipalChainDefaults.ShouldHaveSingleItem()
                .ShouldSatisfyAllConditions(
                    pcd => pcd.ChainId.ShouldBe("ethereum-mainnet"),
                    pcd => pcd.WalletId.ShouldBe(ownership.WalletId),
                    pcd => pcd.PrincipalId.ShouldBe(principal.Id)
                );

            principal.DomainEvents.ShouldHaveSingleItem()
                .ShouldBeOfType<PrincipalChangedEvent>()
                .ShouldSatisfyAllConditions(
                    e => e.PrincipalId.ShouldBe(principal.Id),
                    e => e.PropertyChanged.ShouldBe("ChainDefault.ethereum-mainnet"),
                    e => e.OldValue.ShouldBe("none"),
                    e => e.NewValue.ShouldBe(ownership.WalletId.ToString())
                );
        }

        [Test]
        public void ApplyChainDefault_WithSameWalletId_Should_BeIdempotent()
        {
            // Arrange
            var principal = AxonPrincipal.CreateHuman();
            var ownership = CreateOwnership(principal.Id, accessMode: AccessMode.Signing, status: OwnershipStatus.Verified);
            principal.LinkWalletOwnership(ownership, NoConflictResolver);
            principal.ApplyChainDefault(NetworkEnvironment.Mainnet, "ethereum-mainnet", ownership.WalletId);
            principal.ClearDomainEvents();

            // Act
            var result = principal.ApplyChainDefault(NetworkEnvironment.Mainnet, "ethereum-mainnet", ownership.WalletId);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            principal.PrincipalChainDefaults.ShouldHaveSingleItem();
            principal.DomainEvents.ShouldBeEmpty(); // No event for idempotent operation
        }

        [Test]
        public void ApplyChainDefault_WithNonOwnedWallet_Should_ReturnFailure()
        {
            // Arrange
            var principal = AxonPrincipal.CreateHuman();
            var nonOwnedWalletId = WalletId.New();

            // Act
            var result = principal.ApplyChainDefault(NetworkEnvironment.Mainnet, "ethereum-mainnet", nonOwnedWalletId);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.NotFound);
        }

        [Test]
        public void ApplyChainDefault_WithWatchOnlyWallet_Should_ReturnFailure()
        {
            // Arrange
            var principal = AxonPrincipal.CreateHuman();
            var watchOnlyOwnership = CreateOwnership(principal.Id, accessMode: AccessMode.WatchOnly, status: OwnershipStatus.Verified);
            principal.LinkWalletOwnership(watchOnlyOwnership, NoConflictResolver);

            // Act
            var result = principal.ApplyChainDefault(NetworkEnvironment.Mainnet, "ethereum-mainnet", watchOnlyOwnership.WalletId);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.Validation);
        }

        [Test]
        public void ApplyChainDefault_WithPendingWallet_Should_ReturnFailure()
        {
            // Arrange
            var principal = AxonPrincipal.CreateHuman();
            var pendingOwnership = CreateOwnership(principal.Id, accessMode: AccessMode.Signing, status: OwnershipStatus.Pending);
            principal.LinkWalletOwnership(pendingOwnership, NoConflictResolver);

            // Act
            var result = principal.ApplyChainDefault(NetworkEnvironment.Mainnet, "ethereum-mainnet", pendingOwnership.WalletId);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.Validation);
        }

        [Test]
        public void ApplyChainDefault_WithNullChainId_Should_ThrowArgumentNullException()
        {
            // Arrange
            var principal = AxonPrincipal.CreateHuman();
            var walletId = WalletId.New();

            // Act & Assert
            Should.Throw<ArgumentNullException>(() =>
                principal.ApplyChainDefault(NetworkEnvironment.Mainnet, null!, walletId));
        }

        [Test]
        public void ApplyChainDefault_UpdateExistingDefault_Should_UpdateAndRaiseEvent()
        {
            // Arrange
            var principal = AxonPrincipal.CreateHuman();
            var firstOwnership = CreateOwnership(principal.Id, WalletId.New(), AccessMode.Signing, OwnershipStatus.Verified);
            var secondOwnership = CreateOwnership(principal.Id, WalletId.New(), AccessMode.Signing, OwnershipStatus.Verified);

            principal.LinkWalletOwnership(firstOwnership, NoConflictResolver);
            principal.LinkWalletOwnership(secondOwnership, NoConflictResolver);
            principal.ApplyChainDefault(NetworkEnvironment.Mainnet, "ethereum-mainnet", firstOwnership.WalletId);
            principal.ClearDomainEvents();

            // Act
            var result = principal.ApplyChainDefault(NetworkEnvironment.Mainnet, "ethereum-mainnet", secondOwnership.WalletId);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            principal.PrincipalChainDefaults.ShouldHaveSingleItem()
                .WalletId.ShouldBe(secondOwnership.WalletId);

            principal.DomainEvents.ShouldHaveSingleItem()
                .ShouldBeOfType<PrincipalChangedEvent>()
                .ShouldSatisfyAllConditions(
                    e => e.OldValue.ShouldBe(firstOwnership.WalletId.ToString()),
                    e => e.NewValue.ShouldBe(secondOwnership.WalletId.ToString())
                );
        }
    }

    #endregion

    #region Risk Tier Mapping Tests

    [TestFixture]
    public class RiskTierMappingTests : AxonPrincipalTests
    {
        [Test]
        [TestCase("low", RiskTier.Low)]
        [TestCase("conservative", RiskTier.Low)]
        [TestCase("medium", RiskTier.Medium)]
        [TestCase("balanced", RiskTier.Medium)]
        [TestCase("high", RiskTier.High)]
        [TestCase("aggressive", RiskTier.High)]
        [TestCase("LOW", RiskTier.Low)]
        [TestCase("MEDIUM", RiskTier.Medium)]
        [TestCase("HIGH", RiskTier.High)]
        public void MapRiskTierFromWire_WithValidValues_Should_ReturnCorrectTier(string wireValue, RiskTier expectedTier)
        {
            // Act
            var result = AxonPrincipal.MapRiskTierFromWire(wireValue);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldBe(expectedTier);
        }

        [Test]
        [TestCase("")]
        [TestCase("invalid")]
        [TestCase("unknown")]
        [TestCase(null)]
        public void MapRiskTierFromWire_WithInvalidValues_Should_ReturnFailure(string? wireValue)
        {
            // Act
            var result = AxonPrincipal.MapRiskTierFromWire(wireValue!);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.Validation);
        }

        [Test]
        [TestCase(RiskTier.Low, false, "low")]
        [TestCase(RiskTier.Medium, false, "medium")]
        [TestCase(RiskTier.High, false, "high")]
        [TestCase(RiskTier.Low, true, "conservative")]
        [TestCase(RiskTier.Medium, true, "balanced")]
        [TestCase(RiskTier.High, true, "aggressive")]
        public void MapRiskTierToWire_Should_ReturnCorrectWireValue(RiskTier tier, bool useProductTerms, string expectedWire)
        {
            // Act
            var result = AxonPrincipal.MapRiskTierToWire(tier, useProductTerms);

            // Assert
            result.ShouldBe(expectedWire);
        }
    }

    #endregion
}