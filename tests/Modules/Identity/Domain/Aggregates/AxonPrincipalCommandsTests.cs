using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using BuildingBlocks.Primitives.Ids;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Domain.Tests.Aggregates;

[TestFixture]
public class AxonPrincipalCommandsTests
{
    [TestFixture]
    public class CreateHuman : AxonPrincipalCommandsTests
    {
        [Test]
        public void WithoutId_ShouldGenerateNewId()
        {
            // Act
            var principal = AxonPrincipal.CreateHuman();

            // Assert
            principal.ShouldNotBeNull();
            principal.Id.ShouldNotBe(default(AxonId));
            principal.Type.ShouldBe(PrincipalType.Human);
            principal.RiskTier.ShouldBe(RiskTier.Low);
            principal.Credentials.ShouldBeEmpty();
            principal.WalletOwnerships.ShouldBeEmpty();
        }

        [Test]
        public void WithSpecificId_ShouldUseProvidedId()
        {
            // Arrange
            var id = AxonId.New();

            // Act
            var principal = AxonPrincipal.CreateHuman(id);

            // Assert
            principal.Id.ShouldBe(id);
            principal.Type.ShouldBe(PrincipalType.Human);
            principal.RiskTier.ShouldBe(RiskTier.Low);
        }
    }

    [TestFixture]
    public class CreateService : AxonPrincipalCommandsTests
    {
        [Test]
        public void WithoutId_ShouldGenerateNewId()
        {
            // Act
            var principal = AxonPrincipal.CreateService();

            // Assert
            principal.ShouldNotBeNull();
            principal.Id.ShouldNotBe(default(AxonId));
            principal.Type.ShouldBe(PrincipalType.Service);
            principal.RiskTier.ShouldBe(RiskTier.Low);
            principal.Credentials.ShouldBeEmpty();
            principal.WalletOwnerships.ShouldBeEmpty();
        }

        [Test]
        public void WithSpecificId_ShouldUseProvidedId()
        {
            // Arrange
            var id = AxonId.New();

            // Act
            var principal = AxonPrincipal.CreateService(id);

            // Assert
            principal.Id.ShouldBe(id);
            principal.Type.ShouldBe(PrincipalType.Service);
            principal.RiskTier.ShouldBe(RiskTier.Low);
        }
    }

    [TestFixture]
    public class UpdateRiskTier : AxonPrincipalCommandsTests
    {
        [Test]
        public void WithValidRiskTier_ShouldUpdate()
        {
            // Arrange
            var principal = AxonPrincipal.CreateHuman();
            principal.RiskTier.ShouldBe(RiskTier.Low);

            // Act
            principal.UpdateRiskTier(RiskTier.High);

            // Assert
            principal.RiskTier.ShouldBe(RiskTier.High);
        }

        [Test]
        public void WithMediumRiskTier_ShouldUpdate()
        {
            // Arrange
            var principal = AxonPrincipal.CreateHuman();

            // Act
            principal.UpdateRiskTier(RiskTier.Medium);

            // Assert
            principal.RiskTier.ShouldBe(RiskTier.Medium);
        }
    }

    [TestFixture]
    public class AddCredential : AxonPrincipalCommandsTests
    {
        [Test]
        public void WithValidCredential_ShouldAdd()
        {
            // Arrange
            var principal = AxonPrincipal.CreateHuman();
            var credential = IdentityCredential.Create(
                principal.Id,
                "dynamic",
                "https://test-issuer.com",
                "test-subject");

            // Act
            principal.AddCredential(credential);

            // Assert
            principal.Credentials.ShouldContain(credential);
            principal.Credentials.Count.ShouldBe(1);
        }

        [Test]
        public void WithMultipleCredentials_ShouldAddAll()
        {
            // Arrange
            var principal = AxonPrincipal.CreateHuman();
            var credential1 = IdentityCredential.Create(principal.Id, "dynamic", "issuer1", "subject1");
            var credential2 = IdentityCredential.Create(principal.Id, "oidc", "issuer2", "subject2");

            // Act
            principal.AddCredential(credential1);
            principal.AddCredential(credential2);

            // Assert
            principal.Credentials.Count.ShouldBe(2);
            principal.Credentials.ShouldContain(credential1);
            principal.Credentials.ShouldContain(credential2);
        }
    }

    [TestFixture]
    public class AddWalletOwnership : AxonPrincipalCommandsTests
    {
        [Test]
        public void WithValidOwnership_ShouldAdd()
        {
            // Arrange
            var principal = AxonPrincipal.CreateHuman();
            var walletId = WalletId.New();
            var ownership = WalletOwnership.Create(principal.Id, walletId);

            // Act
            principal.AddWalletOwnership(ownership);

            // Assert
            principal.WalletOwnerships.ShouldContain(ownership);
            principal.WalletOwnerships.Count.ShouldBe(1);
        }

        [Test]
        public void WithMultipleOwnerships_ShouldAddAll()
        {
            // Arrange
            var principal = AxonPrincipal.CreateHuman();
            var walletId1 = WalletId.New();
            var walletId2 = WalletId.New();
            var ownership1 = WalletOwnership.Create(principal.Id, walletId1, AccessMode.Signing);
            var ownership2 = WalletOwnership.Create(principal.Id, walletId2, AccessMode.WatchOnly);

            // Act
            principal.AddWalletOwnership(ownership1);
            principal.AddWalletOwnership(ownership2);

            // Assert
            principal.WalletOwnerships.Count.ShouldBe(2);
            principal.WalletOwnerships.ShouldContain(ownership1);
            principal.WalletOwnerships.ShouldContain(ownership2);
        }
    }
}