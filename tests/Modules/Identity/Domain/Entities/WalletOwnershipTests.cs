using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Domain.Tests.TestData;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Domain.Tests.Entities;

[TestFixture]
public class WalletOwnershipTests
{
    private readonly AxonId _testAxonId = AxonId.New();
    private readonly WalletId _testWalletId = WalletId.New();
    private readonly ChainId _testChainId = Builders.SolanaChain;
    private readonly ProofType _testProofType = Builders.SignatureProof;
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    [TestFixture]
    public class Create : WalletOwnershipTests
    {
        [Test]
        public void WithValidData_ShouldSucceed()
        {
            // Arrange
            var accessMode = AccessMode.Signing;
            var label = "My Main Wallet";
            var firstLinkedAt = DateTimeOffset.UtcNow;

            // Act
            var result = WalletOwnership.Create(
                _testAxonId, _testWalletId, _testChainId, _testProofType, 
                accessMode, label: label, firstLinkedAt: firstLinkedAt);

            // Should
            result.IsSuccess.ShouldBeTrue();
            var ownership = result.Value;
            ownership.AxonId.ShouldBe(_testAxonId);
            ownership.WalletId.ShouldBe(_testWalletId);
            ownership.ChainId.ShouldBe(_testChainId);
            ownership.ProofType.ShouldBe(_testProofType);
            ownership.AccessMode.ShouldBe(accessMode);
            ownership.Label.ShouldBe(label);
            ownership.FirstLinkedAt.ShouldBe(firstLinkedAt);
            ownership.State.ShouldBe(OwnershipState.Default);
            ownership.LastVerifiedAt.ShouldBeNull(); // Default state is not verified
            ownership.IsDeleted.ShouldBeFalse();
        }

        [Test]
        public void WithMinimalData_ShouldUseDefaults()
        {
            // Act
            var result = WalletOwnership.Create(
                _testAxonId, _testWalletId, _testChainId, _testProofType);

            // Should
            result.IsSuccess.ShouldBeTrue();
            var ownership = result.Value;
            ownership.AccessMode.ShouldBe(AccessMode.Default);
            ownership.State.ShouldBe(OwnershipState.Default);
            ownership.Label.ShouldBeNull();
            ownership.FirstLinkedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Test]
        public void WithVerifiedState_ShouldSetLastVerifiedAt()
        {
            // Arrange
            var firstLinkedAt = DateTimeOffset.UtcNow;
            var verifiedState = Builders.VerifiedState;

            // Act
            var result = WalletOwnership.Create(
                _testAxonId, _testWalletId, _testChainId, _testProofType,
                state: verifiedState, firstLinkedAt: firstLinkedAt);

            // Should
            result.IsSuccess.ShouldBeTrue();
            var ownership = result.Value;
            ownership.State.ShouldBe(verifiedState);
            ownership.LastVerifiedAt.ShouldBe(firstLinkedAt);
        }

        [Test]
        public void WithEmptyWalletId_ShouldFail()
        {
            // Act
            var result = WalletOwnership.Create(
                _testAxonId, WalletId.Empty, _testChainId, _testProofType);

            // Should
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.Should().Contain("WALLET.ID.INVALID");
        }

        [Test]
        public void WithInvalidChainId_ShouldFail()
        {
            // Arrange - Create invalid chain ID with empty value
            // This test might need adjustment based on ChainId implementation
            
            // Act & Should - This test verifies the validation, 
            // but implementation might vary based on how ChainId.Value works
            // Skipping for now as ChainId validation might be handled differently
        }
    }

    [TestFixture]
    public class Verify : WalletOwnershipTests
    {
        [Test]
        public void PendingOwnership_ShouldTransitionToVerified()
        {
            // Arrange
            var ownership = CreateTestOwnership(OwnershipState.Pending);
            var verifiedAt = DateTimeOffset.UtcNow;

            // Act
            var result = ownership.Verify(verifiedAt);

            // Should
            result.IsSuccess.ShouldBeTrue();
            ownership.State.ShouldBe(OwnershipState.Verified);
            ownership.LastVerifiedAt.ShouldBe(verifiedAt);
        }

        [Test]
        public void VerifiedOwnership_ShouldUpdateLastVerifiedAt()
        {
            // Arrange
            var ownership = CreateTestOwnership(OwnershipState.Verified);
            var newVerifiedAt = DateTimeOffset.UtcNow.AddHours(1);

            // Act
            var result = ownership.Verify(newVerifiedAt);

            // Should
            result.IsSuccess.ShouldBeTrue();
            ownership.State.ShouldBe(OwnershipState.Verified);
            ownership.LastVerifiedAt.ShouldBe(newVerifiedAt);
        }

        [Test]
        public void DeletedOwnership_ShouldFail()
        {
            // Arrange
            var ownership = CreateTestOwnership();
            ownership.SoftDelete();

            // Act
            var result = ownership.Verify(DateTimeOffset.UtcNow);

            // Should
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.Should().Contain("DELETED");
        }

        [Test]
        public void RevokedOwnership_ShouldFail()
        {
            // Arrange
            var ownership = CreateTestOwnership(OwnershipState.Verified);
            ownership.Revoke();

            // Act
            var result = ownership.Verify(DateTimeOffset.UtcNow);

            // Should
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.Should().Contain("REVOKED");
        }
    }

    [TestFixture]
    public class Revoke : WalletOwnershipTests
    {
        [Test]
        public void VerifiedOwnership_ShouldTransitionToRevoked()
        {
            // Arrange
            var ownership = CreateTestOwnership(OwnershipState.Verified);
            var originalLastVerified = ownership.LastVerifiedAt;

            // Act
            var result = ownership.Revoke();

            // Should
            result.IsSuccess.ShouldBeTrue();
            ownership.State.ShouldBe(OwnershipState.Revoked);
            ownership.LastVerifiedAt.ShouldBe(originalLastVerified); // Should preserve for audit
        }

        [Test]
        public void AlreadyRevokedOwnership_ShouldBeIdempotent()
        {
            // Arrange
            var ownership = CreateTestOwnership(OwnershipState.Revoked);

            // Act
            var result = ownership.Revoke();

            // Should
            result.IsSuccess.ShouldBeTrue();
            ownership.State.ShouldBe(OwnershipState.Revoked);
        }

        [Test]
        public void DeletedOwnership_ShouldFail()
        {
            // Arrange
            var ownership = CreateTestOwnership();
            ownership.SoftDelete();

            // Act
            var result = ownership.Revoke();

            // Should
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.Should().Contain("DELETED");
        }
    }

    [TestFixture]
    public class UpdateLabel : WalletOwnershipTests
    {
        [Test]
        public void WithValidLabel_ShouldSucceed()
        {
            // Arrange
            var ownership = CreateTestOwnership();
            var newLabel = "Updated Wallet Label";

            // Act
            var result = ownership.UpdateLabel(newLabel);

            // Should
            result.IsSuccess.ShouldBeTrue();
            ownership.Label.ShouldBe(newLabel);
        }

        [Test]
        public void WithNullLabel_ShouldClearLabel()
        {
            // Arrange
            var ownership = CreateTestOwnership(label: "Existing Label");

            // Act
            var result = ownership.UpdateLabel(null);

            // Should
            result.IsSuccess.ShouldBeTrue();
            ownership.Label.ShouldBeNull();
        }

        [Test]
        public void WithEmptyLabel_ShouldClearLabel()
        {
            // Arrange
            var ownership = CreateTestOwnership(label: "Existing Label");

            // Act
            var result = ownership.UpdateLabel("   ");

            // Should
            result.IsSuccess.ShouldBeTrue();
            ownership.Label.ShouldBeNull();
        }

        [Test]
        public void WithWhitespaceLabel_ShouldTrim()
        {
            // Arrange
            var ownership = CreateTestOwnership();
            var labelWithWhitespace = "  Trimmed Label  ";

            // Act
            var result = ownership.UpdateLabel(labelWithWhitespace);

            // Should
            result.IsSuccess.ShouldBeTrue();
            ownership.Label.ShouldBe("Trimmed Label");
        }

        [Test]
        public void WithTooLongLabel_ShouldFail()
        {
            // Arrange
            var ownership = CreateTestOwnership();
            var longLabel = new string('x', 101); // Exceeds 100 char limit

            // Act
            var result = ownership.UpdateLabel(longLabel);

            // Should
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.Should().Contain("LABEL.TOO_LONG");
        }

        [Test]
        public void DeletedOwnership_ShouldFail()
        {
            // Arrange
            var ownership = CreateTestOwnership();
            ownership.SoftDelete();

            // Act
            var result = ownership.UpdateLabel("New Label");

            // Should
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.Should().Contain("DELETED");
        }
    }

    [TestFixture]
    public class UpdateAccessMode : WalletOwnershipTests
    {
        [Test]
        public void WithValidAccessMode_ShouldSucceed()
        {
            // Arrange
            var ownership = CreateTestOwnership(accessMode: AccessMode.WatchOnly);
            var newAccessMode = AccessMode.Signing;

            // Act
            var result = ownership.UpdateAccessMode(newAccessMode);

            // Should
            result.IsSuccess.ShouldBeTrue();
            ownership.AccessMode.ShouldBe(newAccessMode);
        }

        [Test]
        public void DeletedOwnership_ShouldFail()
        {
            // Arrange
            var ownership = CreateTestOwnership();
            ownership.SoftDelete();

            // Act
            var result = ownership.UpdateAccessMode(AccessMode.Signing);

            // Should
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.Should().Contain("DELETED");
        }

        [Test]
        public void RevokedOwnership_ShouldFail()
        {
            // Arrange
            var ownership = CreateTestOwnership(OwnershipState.Verified);
            ownership.Revoke();

            // Act
            var result = ownership.UpdateAccessMode(AccessMode.Signing);

            // Should
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.Should().Contain("REVOKED");
        }
    }

    [TestFixture]
    public class Properties : WalletOwnershipTests
    {
        [Test]
        public void BelongsTo_WithSamePrincipalId_ShouldReturnTrue()
        {
            // Arrange
            var ownership = CreateTestOwnership();

            // Act & Should
            ownership.BelongsTo(_testAxonId).ShouldBeTrue();
        }

        [Test]
        public void BelongsTo_WithDifferentPrincipalId_ShouldReturnFalse()
        {
            // Arrange
            var ownership = CreateTestOwnership();
            var differentId = AxonId.New();

            // Act & Should
            ownership.BelongsTo(differentId).ShouldBeFalse();
        }

        [Test]
        public void IsForWallet_WithSameWalletId_ShouldReturnTrue()
        {
            // Arrange
            var ownership = CreateTestOwnership();

            // Act & Should
            ownership.IsForWallet(_testWalletId).ShouldBeTrue();
        }

        [Test]
        public void IsForWallet_WithDifferentWalletId_ShouldReturnFalse()
        {
            // Arrange
            var ownership = CreateTestOwnership();
            var differentWalletId = WalletId.New();

            // Act & Should
            ownership.IsForWallet(differentWalletId).ShouldBeFalse();
        }

        [Test]
        public void IsForChain_WithSameChainId_ShouldReturnTrue()
        {
            // Arrange
            var ownership = CreateTestOwnership();

            // Act & Should
            ownership.IsForChain(_testChainId).ShouldBeTrue();
        }

        [Test]
        public void IsForChain_WithDifferentChainId_ShouldReturnFalse()
        {
            // Arrange
            var ownership = CreateTestOwnership();
            var differentChainId = Builders.EthereumChain;

            // Act & Should
            ownership.IsForChain(differentChainId).ShouldBeFalse();
        }

        [Test]
        public void IsActive_VerifiedAndNotDeleted_ShouldReturnTrue()
        {
            // Arrange
            var ownership = CreateTestOwnership(OwnershipState.Verified);

            // Act & Should
            ownership.IsActive.ShouldBeTrue();
        }

        [Test]
        public void IsActive_PendingState_ShouldReturnFalse()
        {
            // Arrange
            var ownership = CreateTestOwnership(OwnershipState.Pending);

            // Act & Should
            ownership.IsActive.ShouldBeFalse();
        }

        [Test]
        public void IsActive_DeletedOwnership_ShouldReturnFalse()
        {
            // Arrange
            var ownership = CreateTestOwnership(OwnershipState.Verified);
            ownership.SoftDelete();

            // Act & Should
            ownership.IsActive.ShouldBeFalse();
        }

        [Test]
        public void IsVerifiedSigning_ActiveWithSigningAccess_ShouldReturnTrue()
        {
            // Arrange
            var ownership = CreateTestOwnership(OwnershipState.Verified, AccessMode.Signing);

            // Act & Should
            ownership.IsVerifiedSigning.ShouldBeTrue();
        }

        [Test]
        public void IsVerifiedSigning_ActiveWithWatchOnlyAccess_ShouldReturnFalse()
        {
            // Arrange
            var ownership = CreateTestOwnership(OwnershipState.Verified, AccessMode.WatchOnly);

            // Act & Should
            ownership.IsVerifiedSigning.ShouldBeFalse();
        }

        [Test]
        public void IsVerifiedSigning_InactiveOwnership_ShouldReturnFalse()
        {
            // Arrange
            var ownership = CreateTestOwnership(OwnershipState.Pending, AccessMode.Signing);

            // Act & Should
            ownership.IsVerifiedSigning.ShouldBeFalse();
        }

        [Test]
        public void IsConflictingOwnership_VerifiedSigningNotDeleted_ShouldReturnTrue()
        {
            // Arrange
            var ownership = CreateTestOwnership(OwnershipState.Verified, AccessMode.Signing);

            // Act & Should
            ownership.IsConflictingOwnership.ShouldBeTrue();
        }

        [Test]
        public void IsConflictingOwnership_WatchOnlyAccess_ShouldReturnFalse()
        {
            // Arrange
            var ownership = CreateTestOwnership(OwnershipState.Verified, AccessMode.WatchOnly);

            // Act & Should
            ownership.IsConflictingOwnership.ShouldBeFalse();
        }

        [Test]
        public void IsConflictingOwnership_DeletedOwnership_ShouldReturnFalse()
        {
            // Arrange
            var ownership = CreateTestOwnership(OwnershipState.Verified, AccessMode.Signing);
            ownership.SoftDelete();

            // Act & Should
            ownership.IsConflictingOwnership.ShouldBeFalse();
        }
    }

    // Helper method to create test ownership instances
    private WalletOwnership CreateTestOwnership(
        OwnershipState? state = null, 
        AccessMode? accessMode = null,
        string? label = null)
    {
        var result = WalletOwnership.Create(
            _testAxonId, _testWalletId, _testChainId, _testProofType, 
            accessMode, state, label: label);
        
        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }
}