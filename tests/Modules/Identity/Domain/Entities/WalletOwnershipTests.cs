using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.Tests.TestData;
using BuildingBlocks.Primitives.Ids;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Domain.Tests.Entities;

[TestFixture]
public class WalletOwnershipTests
{
    [TestFixture]
    public class Create : WalletOwnershipTests
    {
        [Test]
        public void WithDefaults_ShouldCreatePendingSigningOwnership()
        {
            // Arrange
            var principalId = AxonId.New();
            var walletId = WalletId.New();

            // Act
            var ownership = WalletOwnership.Create(principalId, walletId);

            // Should
            ownership.PrincipalId.ShouldBe(principalId);
            ownership.WalletId.ShouldBe(walletId);
            ownership.AccessMode.ShouldBe(AccessMode.Signing);
            ownership.Status.ShouldBe(OwnershipStatus.Pending);
            ownership.IsVerifiedSigning.ShouldBeFalse();
        }

        [Test]
        public void WithCustomValues_ShouldUseProvidedValues()
        {
            // Arrange
            var principalId = AxonId.New();
            var walletId = WalletId.New();

            // Act
            var ownership = WalletOwnership.Create(principalId, walletId, AccessMode.WatchOnly, OwnershipStatus.Verified);

            // Should
            ownership.AccessMode.ShouldBe(AccessMode.WatchOnly);
            ownership.Status.ShouldBe(OwnershipStatus.Verified);
            ownership.IsVerifiedSigning.ShouldBeFalse(); // Not signing mode
        }
    }

    [TestFixture]
    public class UpdateStatus : WalletOwnershipTests
    {
        [Test]
        public void ShouldChangeStatusCorrectly()
        {
            // Arrange
            var ownership = Builders.CreateWalletOwnership(status: OwnershipStatus.Pending);

            // Act
            ownership.UpdateStatus(OwnershipStatus.Verified);

            // Should
            ownership.Status.ShouldBe(OwnershipStatus.Verified);
        }

        [Test]
        public void CanChangeToAllStatuses()
        {
            // Arrange
            var ownership = Builders.CreateWalletOwnership();

            // Act & Should - Test all status transitions
            ownership.UpdateStatus(OwnershipStatus.Pending);
            ownership.Status.ShouldBe(OwnershipStatus.Pending);

            ownership.UpdateStatus(OwnershipStatus.Verified);
            ownership.Status.ShouldBe(OwnershipStatus.Verified);

            ownership.UpdateStatus(OwnershipStatus.Revoked);
            ownership.Status.ShouldBe(OwnershipStatus.Revoked);
        }
    }

    [TestFixture]
    public class UpdateAccessMode : WalletOwnershipTests
    {
        [Test]
        public void ShouldChangeAccessModeCorrectly()
        {
            // Arrange
            var ownership = Builders.CreateWalletOwnership(accessMode: AccessMode.WatchOnly);

            // Act
            ownership.UpdateAccessMode(AccessMode.Signing);

            // Should
            ownership.AccessMode.ShouldBe(AccessMode.Signing);
        }

        [Test]
        public void CanChangeBetweenAllAccessModes()
        {
            // Arrange
            var ownership = Builders.CreateWalletOwnership();

            // Act & Should
            ownership.UpdateAccessMode(AccessMode.Signing);
            ownership.AccessMode.ShouldBe(AccessMode.Signing);

            ownership.UpdateAccessMode(AccessMode.WatchOnly);
            ownership.AccessMode.ShouldBe(AccessMode.WatchOnly);
        }
    }

    [TestFixture]
    public class IsVerifiedSigningProperty : WalletOwnershipTests
    {
        [Test]
        public void WithVerifiedAndSigning_ShouldReturnTrue()
        {
            // Arrange
            var ownership = Builders.CreateWalletOwnership(
                accessMode: AccessMode.Signing,
                status: OwnershipStatus.Verified);

            // Should
            ownership.IsVerifiedSigning.ShouldBeTrue();
        }

        [Test]
        public void WithVerifiedButWatchOnly_ShouldReturnFalse()
        {
            // Arrange
            var ownership = Builders.CreateWalletOwnership(
                accessMode: AccessMode.WatchOnly,
                status: OwnershipStatus.Verified);

            // Should
            ownership.IsVerifiedSigning.ShouldBeFalse();
        }

        [Test]
        public void WithSigningButPending_ShouldReturnFalse()
        {
            // Arrange
            var ownership = Builders.CreateWalletOwnership(
                accessMode: AccessMode.Signing,
                status: OwnershipStatus.Pending);

            // Should
            ownership.IsVerifiedSigning.ShouldBeFalse();
        }

        [Test]
        public void WithSigningButRevoked_ShouldReturnFalse()
        {
            // Arrange
            var ownership = Builders.CreateWalletOwnership(
                accessMode: AccessMode.Signing,
                status: OwnershipStatus.Revoked);

            // Should
            ownership.IsVerifiedSigning.ShouldBeFalse();
        }
    }
}