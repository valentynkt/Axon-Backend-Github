using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.Tests.Common;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Domain.Tests;

/// <summary>
/// Domain-level tests for ownership state transitions.
/// Validates business rules for state transitions independent of persistence concerns.
/// Complements integration tests by focusing on pure domain logic validation.
/// </summary>
[TestFixture]
public class OwnershipStateTransitionTests : IdentityTestBase
{
    #region Valid State Transition Tests

    [Test]
    public void StateTransition_PendingToVerified_ShouldSucceedAndUpdateTimestamps()
    {
        // Arrange
        var ownership = CreatePendingOwnership();
        var originalCreatedAt = ownership.CreatedAt;

        // Act
        var result = ownership.UpdateStatus(OwnershipStatus.Verified);

        // Assert
        result.IsSuccess.ShouldBeTrue("Pending → Verified should be valid transition");
        ownership.Status.ShouldBe(OwnershipStatus.Verified);
        ownership.VerifiedAt.ShouldNotBeNull("VerifiedAt should be set");
        ownership.VerifiedAt.Value.ShouldBeGreaterThan(originalCreatedAt.DateTime);
        ownership.RevokedAt.ShouldBeNull("RevokedAt should remain null");
    }

    [Test]
    public void StateTransition_PendingToRevoked_ShouldSucceedAndUpdateTimestamps()
    {
        // Arrange
        var ownership = CreatePendingOwnership();
        var originalCreatedAt = ownership.CreatedAt;

        // Act
        var result = ownership.UpdateStatus(OwnershipStatus.Revoked);

        // Assert
        result.IsSuccess.ShouldBeTrue("Pending → Revoked should be valid transition");
        ownership.Status.ShouldBe(OwnershipStatus.Revoked);
        ownership.RevokedAt.ShouldNotBeNull("RevokedAt should be set");
        ownership.RevokedAt.Value.ShouldBeGreaterThan(originalCreatedAt.DateTime);
        ownership.VerifiedAt.ShouldBeNull("VerifiedAt should remain null");
    }

    [Test]
    public void StateTransition_VerifiedToRevoked_ShouldSucceedAndPreserveVerifiedAt()
    {
        // Arrange
        var ownership = CreateVerifiedOwnership();
        var originalVerifiedAt = ownership.VerifiedAt;

        // Act
        var result = ownership.UpdateStatus(OwnershipStatus.Revoked);

        // Assert
        result.IsSuccess.ShouldBeTrue("Verified → Revoked should be valid transition");
        ownership.Status.ShouldBe(OwnershipStatus.Revoked);
        ownership.RevokedAt.ShouldNotBeNull("RevokedAt should be set");
        ownership.VerifiedAt.ShouldBe(originalVerifiedAt, "VerifiedAt should be preserved");
    }

    [Test]
    public void StateTransition_RevokedToVerified_ShouldSucceedAndClearRevokedAt()
    {
        // Arrange
        var ownership = CreateRevokedOwnership();
        var originalRevokedAt = ownership.RevokedAt;

        // Act
        var result = ownership.UpdateStatus(OwnershipStatus.Verified);

        // Assert
        result.IsSuccess.ShouldBeTrue("Revoked → Verified should be valid transition (re-verification)");
        ownership.Status.ShouldBe(OwnershipStatus.Verified);
        ownership.VerifiedAt.ShouldNotBeNull("VerifiedAt should be set on re-verification");
        ownership.VerifiedAt.Value.ShouldBeGreaterThan(originalRevokedAt!.Value);
        ownership.RevokedAt.ShouldBeNull("RevokedAt should be cleared on re-verification");
    }

    [Test]
    public void StateTransition_SameStatus_ShouldBeIdempotentNoOp()
    {
        // Arrange
        var ownership = CreateVerifiedOwnership();
        var originalVerifiedAt = ownership.VerifiedAt;
        var originalStatus = ownership.Status;

        // Act
        var result = ownership.UpdateStatus(OwnershipStatus.Verified);

        // Assert
        result.IsSuccess.ShouldBeTrue("Same status update should be no-op");
        ownership.Status.ShouldBe(originalStatus);
        ownership.VerifiedAt.ShouldBe(originalVerifiedAt, "Timestamps should not change for no-op");
    }

    #endregion

    #region Invalid State Transition Tests

    [Test]
    public void StateTransition_VerifiedToPending_ShouldFailWithBusinessRuleError()
    {
        // Arrange
        var ownership = CreateVerifiedOwnership();

        // Act
        var result = ownership.UpdateStatus(OwnershipStatus.Pending);

        // Assert
        result.IsFailure.ShouldBeTrue("Verified → Pending should be invalid transition");
        result.Error.Type.ShouldBe(ErrorType.BusinessRule);
        result.Error.Code.ShouldContain("INVALID_TRANSITION");
        ownership.Status.ShouldBe(OwnershipStatus.Verified, "Status should remain unchanged");
    }

    [Test]
    public void StateTransition_RevokedToPending_ShouldFailWithBusinessRuleError()
    {
        // Arrange
        var ownership = CreateRevokedOwnership();

        // Act
        var result = ownership.UpdateStatus(OwnershipStatus.Pending);

        // Assert
        result.IsFailure.ShouldBeTrue("Revoked → Pending should be invalid transition");
        result.Error.Type.ShouldBe(ErrorType.BusinessRule);
        result.Error.Code.ShouldContain("INVALID_TRANSITION");
        ownership.Status.ShouldBe(OwnershipStatus.Revoked, "Status should remain unchanged");
    }

    #endregion

    #region Access Mode Update Tests

    [Test]
    public void AccessModeUpdate_PendingOwnership_ShouldAllowModeChange()
    {
        // Arrange
        var ownership = CreatePendingOwnership(AccessMode.WatchOnly);

        // Act
        var result = ownership.UpdateAccessMode(AccessMode.Signing);

        // Assert
        result.IsSuccess.ShouldBeTrue("Access mode change should be allowed for pending ownership");
        ownership.AccessMode.ShouldBe(AccessMode.Signing);
    }

    [Test]
    public void AccessModeUpdate_VerifiedOwnership_ShouldAllowModeChange()
    {
        // Arrange
        var ownership = CreateVerifiedOwnership(AccessMode.WatchOnly);

        // Act
        var result = ownership.UpdateAccessMode(AccessMode.Signing);

        // Assert
        result.IsSuccess.ShouldBeTrue("Access mode change should be allowed for verified ownership");
        ownership.AccessMode.ShouldBe(AccessMode.Signing);
    }

    [Test]
    public void AccessModeUpdate_RevokedOwnership_ShouldFailWithOwnershipRevokedError()
    {
        // Arrange
        var ownership = CreateRevokedOwnership(AccessMode.Signing);

        // Act
        var result = ownership.UpdateAccessMode(AccessMode.WatchOnly);

        // Assert
        result.IsFailure.ShouldBeTrue("Access mode change should not be allowed for revoked ownership");
        result.Error.Code.ShouldContain("OWNERSHIP_REVOKED");
        ownership.AccessMode.ShouldBe(AccessMode.Signing, "Access mode should remain unchanged");
    }

    [Test]
    public void AccessModeUpdate_SameMode_ShouldBeIdempotentNoOp()
    {
        // Arrange
        var ownership = CreateVerifiedOwnership(AccessMode.Signing);

        // Act
        var result = ownership.UpdateAccessMode(AccessMode.Signing);

        // Assert
        result.IsSuccess.ShouldBeTrue("Same access mode update should be no-op");
        ownership.AccessMode.ShouldBe(AccessMode.Signing);
    }

    #endregion

    #region Computed Property Tests

    [Test]
    public void IsVerifiedSigning_VerifiedSigningOwnership_ShouldReturnTrue()
    {
        // Arrange
        var ownership = CreateVerifiedOwnership(AccessMode.Signing);

        // Act & Assert
        ownership.IsVerifiedSigning.ShouldBeTrue();
    }

    [Test]
    public void IsVerifiedSigning_VerifiedWatchOnlyOwnership_ShouldReturnFalse()
    {
        // Arrange
        var ownership = CreateVerifiedOwnership(AccessMode.WatchOnly);

        // Act & Assert
        ownership.IsVerifiedSigning.ShouldBeFalse();
    }

    [Test]
    public void IsVerifiedSigning_PendingSigningOwnership_ShouldReturnFalse()
    {
        // Arrange
        var ownership = CreatePendingOwnership(AccessMode.Signing);

        // Act & Assert
        ownership.IsVerifiedSigning.ShouldBeFalse();
    }

    [Test]
    public void IsVerifiedSigning_RevokedSigningOwnership_ShouldReturnFalse()
    {
        // Arrange
        var ownership = CreateRevokedOwnership(AccessMode.Signing);

        // Act & Assert
        ownership.IsVerifiedSigning.ShouldBeFalse();
    }

    [Test]
    public void CanBeDefault_VerifiedSigningOwnership_ShouldReturnTrue()
    {
        // Arrange
        var ownership = CreateVerifiedOwnership(AccessMode.Signing);

        // Act & Assert
        ownership.CanBeDefault.ShouldBeTrue("Verified+Signing ownership should be eligible as default");
    }

    [Test]
    public void CanBeDefault_NonVerifiedSigningOwnership_ShouldReturnFalse()
    {
        // Arrange
        var ownership = CreatePendingOwnership(AccessMode.Signing);

        // Act & Assert
        ownership.CanBeDefault.ShouldBeFalse("Non-verified ownership should not be eligible as default");
    }

    [Test]
    public void CanBeDefault_VerifiedWatchOnlyOwnership_ShouldReturnFalse()
    {
        // Arrange
        var ownership = CreateVerifiedOwnership(AccessMode.WatchOnly);

        // Act & Assert
        ownership.CanBeDefault.ShouldBeFalse("Watch-only ownership should not be eligible as default");
    }

    [Test]
    public void IsActive_PendingOwnership_ShouldReturnTrue()
    {
        // Arrange
        var ownership = CreatePendingOwnership();

        // Act & Assert
        ownership.IsActive.ShouldBeTrue("Pending ownership should be active");
    }

    [Test]
    public void IsActive_VerifiedOwnership_ShouldReturnTrue()
    {
        // Arrange
        var ownership = CreateVerifiedOwnership();

        // Act & Assert
        ownership.IsActive.ShouldBeTrue("Verified ownership should be active");
    }

    [Test]
    public void IsActive_RevokedOwnership_ShouldReturnFalse()
    {
        // Arrange
        var ownership = CreateRevokedOwnership();

        // Act & Assert
        ownership.IsActive.ShouldBeFalse("Revoked ownership should not be active");
    }

    #endregion

    #region Comprehensive State Transition Matrix Tests

    [Test]
    [TestCase(OwnershipStatus.Pending, OwnershipStatus.Verified, true)]
    [TestCase(OwnershipStatus.Pending, OwnershipStatus.Revoked, true)]
    [TestCase(OwnershipStatus.Pending, OwnershipStatus.Pending, true)]
    [TestCase(OwnershipStatus.Verified, OwnershipStatus.Revoked, true)]
    [TestCase(OwnershipStatus.Verified, OwnershipStatus.Verified, true)]
    [TestCase(OwnershipStatus.Verified, OwnershipStatus.Pending, false)]
    [TestCase(OwnershipStatus.Revoked, OwnershipStatus.Verified, true)]
    [TestCase(OwnershipStatus.Revoked, OwnershipStatus.Revoked, true)]
    [TestCase(OwnershipStatus.Revoked, OwnershipStatus.Pending, false)]
    public void StateTransition_AllTransitionCombinations_ShouldFollowBusinessRules(
        OwnershipStatus fromStatus,
        OwnershipStatus toStatus,
        bool shouldSucceed)
    {
        // Arrange
        var ownership = CreateOwnershipWithStatus(fromStatus);

        // Act
        var result = ownership.UpdateStatus(toStatus);

        // Assert
        if (shouldSucceed)
        {
            result.IsSuccess.ShouldBeTrue($"{fromStatus} → {toStatus} should be valid transition");
            ownership.Status.ShouldBe(toStatus);
        }
        else
        {
            result.IsFailure.ShouldBeTrue($"{fromStatus} → {toStatus} should be invalid transition");
            result.Error.Type.ShouldBe(ErrorType.BusinessRule);
            ownership.Status.ShouldBe(fromStatus, "Status should remain unchanged for invalid transition");
        }
    }

    #endregion

    #region Authority Ranking Tests

    [Test]
    public void AuthorityRanking_VerifiedSigningVsPendingSigning_VerifiedShouldRankHigher()
    {
        // Arrange
        var verifiedSigning = CreateVerifiedOwnership(AccessMode.Signing);
        var pendingSigning = CreatePendingOwnership(AccessMode.Signing);

        // Act & Assert
        verifiedSigning.IsVerifiedSigning.ShouldBeTrue();
        pendingSigning.IsVerifiedSigning.ShouldBeFalse();

        // Verified+Signing should have higher authority for resolution tie-breaking
        GetAuthorityScore(verifiedSigning).ShouldBeGreaterThan(GetAuthorityScore(pendingSigning));
    }

    [Test]
    public void AuthorityRanking_PendingSigningVsVerifiedWatchOnly_PendingSigningShouldRankHigher()
    {
        // Arrange
        var pendingSigning = CreatePendingOwnership(AccessMode.Signing);
        var verifiedWatchOnly = CreateVerifiedOwnership(AccessMode.WatchOnly);

        // Act & Assert
        // Pending+Signing should rank higher than Verified+WatchOnly for resolution
        GetAuthorityScore(pendingSigning).ShouldBeGreaterThan(GetAuthorityScore(verifiedWatchOnly));
    }

    [Test]
    public void AuthorityRanking_RevokedOwnership_ShouldHaveLowestAuthority()
    {
        // Arrange
        var revokedSigning = CreateRevokedOwnership(AccessMode.Signing);
        var pendingWatchOnly = CreatePendingOwnership(AccessMode.WatchOnly);

        // Act & Assert
        // Any active ownership should rank higher than revoked
        revokedSigning.IsActive.ShouldBeFalse();
        pendingWatchOnly.IsActive.ShouldBeTrue();
        GetAuthorityScore(pendingWatchOnly).ShouldBeGreaterThan(GetAuthorityScore(revokedSigning));
    }

    #endregion

    #region Test Helper Methods

    private static WalletOwnership CreatePendingOwnership(AccessMode accessMode = AccessMode.Signing)
    {
        return WalletOwnership.Create(
            AxonUserId.New(),
            WalletId.New(),
            accessMode,
            OwnershipStatus.Pending);
    }

    private static WalletOwnership CreateVerifiedOwnership(AccessMode accessMode = AccessMode.Signing)
    {
        var ownership = WalletOwnership.Create(
            AxonUserId.New(),
            WalletId.New(),
            accessMode,
            OwnershipStatus.Pending);

        ownership.UpdateStatus(OwnershipStatus.Verified);
        return ownership;
    }

    private static WalletOwnership CreateRevokedOwnership(AccessMode accessMode = AccessMode.Signing)
    {
        var ownership = WalletOwnership.Create(
            AxonUserId.New(),
            WalletId.New(),
            accessMode,
            OwnershipStatus.Pending);

        ownership.UpdateStatus(OwnershipStatus.Revoked);
        return ownership;
    }

    private static WalletOwnership CreateOwnershipWithStatus(OwnershipStatus status)
    {
        return status switch
        {
            OwnershipStatus.Pending => CreatePendingOwnership(),
            OwnershipStatus.Verified => CreateVerifiedOwnership(),
            OwnershipStatus.Revoked => CreateRevokedOwnership(),
            _ => throw new ArgumentException($"Unknown status: {status}")
        };
    }

    /// <summary>
    /// Helper method to calculate authority score for resolution tie-breaking.
    /// Higher score = higher authority in resolution algorithm.
    /// </summary>
    private static int GetAuthorityScore(WalletOwnership ownership)
    {
        if (!ownership.IsActive)
            return 0; // Revoked has no authority

        var statusScore = ownership.Status switch
        {
            OwnershipStatus.Verified => 100,
            OwnershipStatus.Pending => 50,
            _ => 0
        };

        var accessScore = ownership.AccessMode switch
        {
            AccessMode.Signing => 10,
            AccessMode.WatchOnly => 5,
            _ => 0
        };

        return statusScore + accessScore;
    }

    #endregion
}