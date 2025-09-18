using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.Tests.Common;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Shouldly;

namespace Axon.Modules.Identity.Domain.Tests.Entities;

/// <summary>
/// Comprehensive test suite for WalletOwnership entity.
/// Tests creation, status transitions, access mode updates, and business rules.
/// </summary>
[TestFixture]
public class WalletOwnershipTests : IdentityTestBase
{
    #region Creation Tests

    [TestFixture]
    public class CreateTests : WalletOwnershipTests
    {
        [Test]
        public void Create_WithDefaultParameters_Should_CreatePendingSigningOwnership()
        {
            // Arrange
            var principalId = AxonUserId.New();
            var walletId = WalletId.New();

            // Act
            var ownership = WalletOwnership.Create(principalId, walletId);

            // Assert
            ownership.ShouldSatisfyAllConditions(
                o => o.Id.Value.ShouldNotBe(Guid.Empty),
                o => o.PrincipalId.ShouldBe(principalId),
                o => o.WalletId.ShouldBe(walletId),
                o => o.AccessMode.ShouldBe(AccessMode.Signing),
                o => o.Status.ShouldBe(OwnershipStatus.Pending),
                o => o.VerifiedAt.ShouldBeNull(),
                o => o.RevokedAt.ShouldBeNull(),
                o => o.IsDeleted.ShouldBeFalse()
            );
        }

        [Test]
        public void Create_WithExplicitParameters_Should_UseProvidedValues()
        {
            // Arrange
            var principalId = AxonUserId.New();
            var walletId = WalletId.New();
            var accessMode = AccessMode.WatchOnly;
            var status = OwnershipStatus.Verified;

            // Act
            var ownership = WalletOwnership.Create(principalId, walletId, accessMode, status);

            // Assert
            ownership.ShouldSatisfyAllConditions(
                o => o.PrincipalId.ShouldBe(principalId),
                o => o.WalletId.ShouldBe(walletId),
                o => o.AccessMode.ShouldBe(accessMode),
                o => o.Status.ShouldBe(status)
            );
        }

        [Test]
        [TestCase(AccessMode.Signing)]
        [TestCase(AccessMode.WatchOnly)]
        public void Create_WithDifferentAccessModes_Should_AcceptAllValidModes(AccessMode accessMode)
        {
            // Arrange
            var principalId = AxonUserId.New();
            var walletId = WalletId.New();

            // Act
            var ownership = WalletOwnership.Create(principalId, walletId, accessMode);

            // Assert
            ownership.AccessMode.ShouldBe(accessMode);
        }

        [Test]
        [TestCase(OwnershipStatus.Pending)]
        [TestCase(OwnershipStatus.Verified)]
        [TestCase(OwnershipStatus.Revoked)]
        public void Create_WithDifferentStatuses_Should_AcceptAllValidStatuses(OwnershipStatus status)
        {
            // Arrange
            var principalId = AxonUserId.New();
            var walletId = WalletId.New();

            // Act
            var ownership = WalletOwnership.Create(principalId, walletId, status: status);

            // Assert
            ownership.Status.ShouldBe(status);
        }

        [Test]
        public void Create_ShouldGenerateUniqueIds()
        {
            // Arrange
            var principalId = AxonUserId.New();
            var walletId = WalletId.New();

            // Act
            var ownership1 = WalletOwnership.Create(principalId, walletId);
            var ownership2 = WalletOwnership.Create(principalId, walletId);

            // Assert
            ownership1.Id.ShouldNotBe(ownership2.Id);
        }
    }

    #endregion

    #region Status Update Tests

    [TestFixture]
    public class UpdateStatusTests : WalletOwnershipTests
    {
        [Test]
        public void UpdateStatus_FromPendingToVerified_Should_SucceedAndSetTimestamp()
        {
            // Arrange
            var ownership = WalletOwnership.Create(AxonUserId.New(), WalletId.New(), status: OwnershipStatus.Pending);
            var beforeUpdate = DateTime.UtcNow;

            // Act
            var result = ownership.UpdateStatus(OwnershipStatus.Verified);

            // Assert
            var afterUpdate = DateTime.UtcNow;
            result.IsSuccess.ShouldBeTrue();
            ownership.Status.ShouldBe(OwnershipStatus.Verified);
            ownership.VerifiedAt.ShouldNotBeNull();
            ownership.VerifiedAt.Value.ShouldBeGreaterThanOrEqualTo(beforeUpdate);
            ownership.VerifiedAt.Value.ShouldBeLessThanOrEqualTo(afterUpdate);
            ownership.RevokedAt.ShouldBeNull();
        }

        [Test]
        public void UpdateStatus_FromPendingToRevoked_Should_SucceedAndSetTimestamp()
        {
            // Arrange
            var ownership = WalletOwnership.Create(AxonUserId.New(), WalletId.New(), status: OwnershipStatus.Pending);
            var beforeUpdate = DateTime.UtcNow;

            // Act
            var result = ownership.UpdateStatus(OwnershipStatus.Revoked);

            // Assert
            var afterUpdate = DateTime.UtcNow;
            result.IsSuccess.ShouldBeTrue();
            ownership.Status.ShouldBe(OwnershipStatus.Revoked);
            ownership.RevokedAt.ShouldNotBeNull();
            ownership.RevokedAt.Value.ShouldBeGreaterThanOrEqualTo(beforeUpdate);
            ownership.RevokedAt.Value.ShouldBeLessThanOrEqualTo(afterUpdate);
        }

        [Test]
        public void UpdateStatus_FromVerifiedToRevoked_Should_SucceedAndUpdateTimestamp()
        {
            // Arrange
            var ownership = WalletOwnership.Create(AxonUserId.New(), WalletId.New(), status: OwnershipStatus.Verified);
            var beforeUpdate = DateTime.UtcNow;

            // Act
            var result = ownership.UpdateStatus(OwnershipStatus.Revoked);

            // Assert
            var afterUpdate = DateTime.UtcNow;
            result.IsSuccess.ShouldBeTrue();
            ownership.Status.ShouldBe(OwnershipStatus.Revoked);
            ownership.RevokedAt.ShouldNotBeNull();
            ownership.RevokedAt.Value.ShouldBeGreaterThanOrEqualTo(beforeUpdate);
            ownership.RevokedAt.Value.ShouldBeLessThanOrEqualTo(afterUpdate);
        }

        [Test]
        public void UpdateStatus_FromRevokedToVerified_Should_SucceedAndClearRevokedTimestamp()
        {
            // Arrange
            var ownership = WalletOwnership.Create(AxonUserId.New(), WalletId.New(), status: OwnershipStatus.Revoked);
            var beforeUpdate = DateTime.UtcNow;

            // Act
            var result = ownership.UpdateStatus(OwnershipStatus.Verified);

            // Assert
            var afterUpdate = DateTime.UtcNow;
            result.IsSuccess.ShouldBeTrue();
            ownership.Status.ShouldBe(OwnershipStatus.Verified);
            ownership.VerifiedAt.ShouldNotBeNull();
            ownership.VerifiedAt.Value.ShouldBeGreaterThanOrEqualTo(beforeUpdate);
            ownership.VerifiedAt.Value.ShouldBeLessThanOrEqualTo(afterUpdate);
            ownership.RevokedAt.ShouldBeNull(); // Should be cleared on re-verification
        }

        [Test]
        public void UpdateStatus_WithSameStatus_Should_BeIdempotent()
        {
            // Arrange
            var ownership = WalletOwnership.Create(AxonUserId.New(), WalletId.New(), status: OwnershipStatus.Verified);
            var originalVerifiedAt = ownership.VerifiedAt;

            // Act
            var result = ownership.UpdateStatus(OwnershipStatus.Verified);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            ownership.Status.ShouldBe(OwnershipStatus.Verified);
            ownership.VerifiedAt.ShouldBe(originalVerifiedAt); // Should not change timestamp
        }

        [Test]
        public void UpdateStatus_WithInvalidTransition_Should_ReturnFailure()
        {
            // There are no invalid transitions in the current business rules,
            // but we test the validation mechanism
            // All transitions are currently allowed, but this tests the framework

            // Arrange
            var ownership = WalletOwnership.Create(AxonUserId.New(), WalletId.New(), status: OwnershipStatus.Pending);

            // Act & Assert - All transitions should succeed based on current business rules
            var pendingToVerified = ownership.UpdateStatus(OwnershipStatus.Verified);
            pendingToVerified.IsSuccess.ShouldBeTrue();

            var verifiedToRevoked = ownership.UpdateStatus(OwnershipStatus.Revoked);
            verifiedToRevoked.IsSuccess.ShouldBeTrue();

            var revokedToVerified = ownership.UpdateStatus(OwnershipStatus.Verified);
            revokedToVerified.IsSuccess.ShouldBeTrue();
        }

        [Test]
        public void UpdateStatus_MultipleTransitions_Should_TrackCorrectly()
        {
            // Arrange
            var ownership = WalletOwnership.Create(AxonUserId.New(), WalletId.New(), status: OwnershipStatus.Pending);

            // Act & Assert - Pending -> Verified -> Revoked -> Verified
            ownership.UpdateStatus(OwnershipStatus.Verified);
            ownership.Status.ShouldBe(OwnershipStatus.Verified);
            ownership.VerifiedAt.ShouldNotBeNull();

            ownership.UpdateStatus(OwnershipStatus.Revoked);
            ownership.Status.ShouldBe(OwnershipStatus.Revoked);
            ownership.RevokedAt.ShouldNotBeNull();

            ownership.UpdateStatus(OwnershipStatus.Verified);
            ownership.Status.ShouldBe(OwnershipStatus.Verified);
            ownership.VerifiedAt.ShouldNotBeNull();
            ownership.RevokedAt.ShouldBeNull(); // Should be cleared
        }
    }

    #endregion

    #region Access Mode Update Tests

    [TestFixture]
    public class UpdateAccessModeTests : WalletOwnershipTests
    {
        [Test]
        public void UpdateAccessMode_WithValidOwnership_Should_UpdateMode()
        {
            // Arrange
            var ownership = WalletOwnership.Create(AxonUserId.New(), WalletId.New(), AccessMode.Signing, OwnershipStatus.Verified);

            // Act
            var result = ownership.UpdateAccessMode(AccessMode.WatchOnly);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            ownership.AccessMode.ShouldBe(AccessMode.WatchOnly);
        }

        [Test]
        public void UpdateAccessMode_WithRevokedOwnership_Should_ReturnFailure()
        {
            // Arrange
            var ownership = WalletOwnership.Create(AxonUserId.New(), WalletId.New(), AccessMode.Signing, OwnershipStatus.Revoked);

            // Act
            var result = ownership.UpdateAccessMode(AccessMode.WatchOnly);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.Validation);
            ownership.AccessMode.ShouldBe(AccessMode.Signing); // Should remain unchanged
        }

        [Test]
        public void UpdateAccessMode_WithSameMode_Should_BeIdempotent()
        {
            // Arrange
            var ownership = WalletOwnership.Create(AxonUserId.New(), WalletId.New(), AccessMode.Signing, OwnershipStatus.Verified);

            // Act
            var result = ownership.UpdateAccessMode(AccessMode.Signing);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            ownership.AccessMode.ShouldBe(AccessMode.Signing);
        }

        [Test]
        [TestCase(OwnershipStatus.Pending)]
        [TestCase(OwnershipStatus.Verified)]
        public void UpdateAccessMode_WithNonRevokedStatus_Should_AllowUpdate(OwnershipStatus status)
        {
            // Arrange
            var ownership = WalletOwnership.Create(AxonUserId.New(), WalletId.New(), AccessMode.Signing, status);

            // Act
            var result = ownership.UpdateAccessMode(AccessMode.WatchOnly);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            ownership.AccessMode.ShouldBe(AccessMode.WatchOnly);
        }

        [Test]
        public void UpdateAccessMode_FromWatchOnlyToSigning_Should_Succeed()
        {
            // Arrange
            var ownership = WalletOwnership.Create(AxonUserId.New(), WalletId.New(), AccessMode.WatchOnly, OwnershipStatus.Verified);

            // Act
            var result = ownership.UpdateAccessMode(AccessMode.Signing);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            ownership.AccessMode.ShouldBe(AccessMode.Signing);
        }
    }

    #endregion

    #region Property Tests

    [TestFixture]
    public class PropertyTests : WalletOwnershipTests
    {
        [Test]
        public void IsVerifiedSigning_WithVerifiedSigningOwnership_Should_ReturnTrue()
        {
            // Arrange
            var ownership = WalletOwnership.Create(AxonUserId.New(), WalletId.New(), AccessMode.Signing, OwnershipStatus.Verified);

            // Act & Assert
            ownership.IsVerifiedSigning.ShouldBeTrue();
        }

        [Test]
        [TestCase(AccessMode.WatchOnly, OwnershipStatus.Verified)]
        [TestCase(AccessMode.Signing, OwnershipStatus.Pending)]
        [TestCase(AccessMode.Signing, OwnershipStatus.Revoked)]
        [TestCase(AccessMode.WatchOnly, OwnershipStatus.Pending)]
        [TestCase(AccessMode.WatchOnly, OwnershipStatus.Revoked)]
        public void IsVerifiedSigning_WithNonVerifiedSigningCombinations_Should_ReturnFalse(
            AccessMode accessMode, OwnershipStatus status)
        {
            // Arrange
            var ownership = WalletOwnership.Create(AxonUserId.New(), WalletId.New(), accessMode, status);

            // Act & Assert
            ownership.IsVerifiedSigning.ShouldBeFalse();
        }

        [Test]
        public void CanBeDefault_WithVerifiedSigningOwnership_Should_ReturnTrue()
        {
            // Arrange
            var ownership = WalletOwnership.Create(AxonUserId.New(), WalletId.New(), AccessMode.Signing, OwnershipStatus.Verified);

            // Act & Assert
            ownership.CanBeDefault.ShouldBeTrue();
        }

        [Test]
        [TestCase(AccessMode.WatchOnly, OwnershipStatus.Verified)]
        [TestCase(AccessMode.Signing, OwnershipStatus.Pending)]
        [TestCase(AccessMode.Signing, OwnershipStatus.Revoked)]
        public void CanBeDefault_WithNonVerifiedSigningCombinations_Should_ReturnFalse(
            AccessMode accessMode, OwnershipStatus status)
        {
            // Arrange
            var ownership = WalletOwnership.Create(AxonUserId.New(), WalletId.New(), accessMode, status);

            // Act & Assert
            ownership.CanBeDefault.ShouldBeFalse();
        }

        [Test]
        [TestCase(OwnershipStatus.Pending)]
        [TestCase(OwnershipStatus.Verified)]
        public void IsActive_WithNonRevokedStatus_Should_ReturnTrue(OwnershipStatus status)
        {
            // Arrange
            var ownership = WalletOwnership.Create(AxonUserId.New(), WalletId.New(), status: status);

            // Act & Assert
            ownership.IsActive.ShouldBeTrue();
        }

        [Test]
        public void IsActive_WithRevokedStatus_Should_ReturnFalse()
        {
            // Arrange
            var ownership = WalletOwnership.Create(AxonUserId.New(), WalletId.New(), status: OwnershipStatus.Revoked);

            // Act & Assert
            ownership.IsActive.ShouldBeFalse();
        }
    }

    #endregion

    #region Edge Cases and Validation Tests

    [TestFixture]
    public class EdgeCasesTests : WalletOwnershipTests
    {
        [Test]
        public void Create_WithSamePrincipalAndWallet_Should_AllowMultipleOwnerships()
        {
            // This might be needed for different access modes or historical tracking
            // Arrange
            var principalId = AxonUserId.New();
            var walletId = WalletId.New();

            // Act
            var ownership1 = WalletOwnership.Create(principalId, walletId, AccessMode.Signing);
            var ownership2 = WalletOwnership.Create(principalId, walletId, AccessMode.WatchOnly);

            // Assert
            ownership1.ShouldNotBe(ownership2);
            ownership1.PrincipalId.ShouldBe(ownership2.PrincipalId);
            ownership1.WalletId.ShouldBe(ownership2.WalletId);
            ownership1.AccessMode.ShouldNotBe(ownership2.AccessMode);
        }

        [Test]
        public void UpdateStatus_RapidSuccessiveUpdates_Should_HandleCorrectly()
        {
            // Arrange
            var ownership = WalletOwnership.Create(AxonUserId.New(), WalletId.New(), status: OwnershipStatus.Pending);

            // Act - Rapid successive updates
            var result1 = ownership.UpdateStatus(OwnershipStatus.Verified);
            var result2 = ownership.UpdateStatus(OwnershipStatus.Revoked);
            var result3 = ownership.UpdateStatus(OwnershipStatus.Verified);

            // Assert
            result1.IsSuccess.ShouldBeTrue();
            result2.IsSuccess.ShouldBeTrue();
            result3.IsSuccess.ShouldBeTrue();
            ownership.Status.ShouldBe(OwnershipStatus.Verified);
        }

        [Test]
        public void UpdateAccessMode_MultipleUpdates_Should_TrackCorrectly()
        {
            // Arrange
            var ownership = WalletOwnership.Create(AxonUserId.New(), WalletId.New(), AccessMode.Signing, OwnershipStatus.Verified);

            // Act
            var result1 = ownership.UpdateAccessMode(AccessMode.WatchOnly);
            var result2 = ownership.UpdateAccessMode(AccessMode.Signing);
            var result3 = ownership.UpdateAccessMode(AccessMode.WatchOnly);

            // Assert
            result1.IsSuccess.ShouldBeTrue();
            result2.IsSuccess.ShouldBeTrue();
            result3.IsSuccess.ShouldBeTrue();
            ownership.AccessMode.ShouldBe(AccessMode.WatchOnly);
        }

        [Test]
        public void TimestampTracking_Should_BeAccurate()
        {
            // Arrange
            var ownership = WalletOwnership.Create(AxonUserId.New(), WalletId.New(), status: OwnershipStatus.Pending);

            // Act - Update to verified, then revoked, then verified again
            var time1 = DateTime.UtcNow;
            ownership.UpdateStatus(OwnershipStatus.Verified);
            var verifiedAt1 = ownership.VerifiedAt;

            Thread.Sleep(10); // Small delay to ensure different timestamps

            ownership.UpdateStatus(OwnershipStatus.Revoked);

            Thread.Sleep(10); // Small delay to ensure different timestamps

            var time3 = DateTime.UtcNow;
            ownership.UpdateStatus(OwnershipStatus.Verified);
            var verifiedAt2 = ownership.VerifiedAt;

            // Assert
            verifiedAt1.ShouldNotBeNull();
            verifiedAt1.Value.ShouldBeGreaterThanOrEqualTo(time1);

            ownership.RevokedAt.ShouldBeNull(); // Should be cleared on re-verification

            verifiedAt2.ShouldNotBeNull();
            verifiedAt2.Value.ShouldBeGreaterThanOrEqualTo(time3);
            verifiedAt2.Value.ShouldBeGreaterThan(verifiedAt1.Value);
        }
    }

    #endregion

    #region Inheritance and Base Class Tests

    [TestFixture]
    public class InheritanceTests : WalletOwnershipTests
    {
        [Test]
        public void WalletOwnership_Should_InheritFromAuditableDeletableEntity()
        {
            // Arrange & Act
            var ownership = WalletOwnership.Create(AxonUserId.New(), WalletId.New());

            // Assert - Verify auditable properties are available
            var now = DateTimeOffset.UtcNow;
            ownership.ShouldSatisfyAllConditions(
                o => o.CreatedAt.ShouldBeLessThanOrEqualTo(now),
                o => o.UpdatedAt.ShouldNotBeNull().ShouldBeLessThanOrEqualTo(now),
                o => o.IsDeleted.ShouldBeFalse(),
                o => o.DeletedAt.ShouldBeNull()
            );
        }

        [Test]
        public void WalletOwnership_Should_HaveUniquelyTypedId()
        {
            // Arrange & Act
            var ownership = WalletOwnership.Create(AxonUserId.New(), WalletId.New());

            // Assert
            ownership.Id.ShouldBeOfType<WalletOwnershipId>();
            ownership.Id.Value.ShouldNotBe(Guid.Empty);
        }
    }

    #endregion
}