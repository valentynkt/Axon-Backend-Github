using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.Tests.TestData;
using BuildingBlocks.Primitives.Ids;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Domain.Tests.Aggregates;

[TestFixture]
public class AxonPrincipalTests
{
    [TestFixture] 
    public class CreateHuman : AxonPrincipalTests
    {
        [Test]
        public void ShouldCreateWithValidDefaults()
        {
            // Act
            var principal = AxonPrincipal.CreateHuman();

            // Should
            principal.ShouldNotBeNull();
            principal.Id.ShouldNotBe(default(AxonId));
            principal.Type.ShouldBe(PrincipalType.Human);
            principal.RiskTier.ShouldBe(RiskTier.Low);
            principal.Credentials.ShouldBeEmpty();
            principal.WalletOwnerships.ShouldBeEmpty();
        }

        [Test]
        public void WithCustomId_ShouldUseProvidedId()
        {
            // Arrange
            var customId = AxonId.New();

            // Act
            var principal = AxonPrincipal.CreateHuman(customId);

            // Should
            principal.Id.ShouldBe(customId);
            principal.Type.ShouldBe(PrincipalType.Human);
        }

        [Test]
        public void ShouldStartWithLowRiskTier()
        {
            // Act
            var principal = AxonPrincipal.CreateHuman();

            // Should
            principal.RiskTier.ShouldBe(RiskTier.Low);
        }
    }

    [TestFixture]
    public class CreateService : AxonPrincipalTests
    {
        [Test]
        public void ShouldCreateWithValidDefaults()
        {
            // Act
            var principal = AxonPrincipal.CreateService();

            // Should
            principal.ShouldNotBeNull();
            principal.Id.ShouldNotBe(default(AxonId));
            principal.Type.ShouldBe(PrincipalType.Service);
            principal.RiskTier.ShouldBe(RiskTier.Low);
            principal.Credentials.ShouldBeEmpty();
            principal.WalletOwnerships.ShouldBeEmpty();
        }

        [Test]
        public void WithCustomId_ShouldUseProvidedId()
        {
            // Arrange
            var customId = AxonId.New();

            // Act
            var principal = AxonPrincipal.CreateService(customId);

            // Should
            principal.Id.ShouldBe(customId);
            principal.Type.ShouldBe(PrincipalType.Service);
        }
    }

    [TestFixture]
    public class RiskTierOperations : AxonPrincipalTests
    {
        [Test]
        public void UpdateRiskTier_ShouldChangeRiskLevel()
        {
            // Arrange
            var principal = AxonPrincipal.CreateHuman();
            principal.RiskTier.ShouldBe(RiskTier.Low);

            // Act
            principal.UpdateRiskTier(RiskTier.High);

            // Should
            principal.RiskTier.ShouldBe(RiskTier.High);
        }

        [Test]
        public void UpdateRiskTier_CanSetAllLevels()
        {
            // Arrange
            var principal = AxonPrincipal.CreateHuman();

            // Act & Should - Test all risk tiers
            principal.UpdateRiskTier(RiskTier.Low);
            principal.RiskTier.ShouldBe(RiskTier.Low);

            principal.UpdateRiskTier(RiskTier.Medium);
            principal.RiskTier.ShouldBe(RiskTier.Medium);

            principal.UpdateRiskTier(RiskTier.High);
            principal.RiskTier.ShouldBe(RiskTier.High);
        }
    }

    [TestFixture]
    public class CollectionOperations : AxonPrincipalTests
    {
        [Test]
        public void NewPrincipal_ShouldHaveEmptyCollections()
        {
            // Arrange
            var principal = AxonPrincipal.CreateHuman();

            // Should
            principal.Credentials.ShouldBeEmpty();
            principal.WalletOwnerships.ShouldBeEmpty();
        }

        [Test]
        public void AddCredential_ShouldIncreaseCredentialsCollection()
        {
            // Arrange
            var principal = AxonPrincipal.CreateHuman();
            var credential = Builders.CreateIdentityCredential();

            // Act
            principal.AddCredential(credential);

            // Should
            principal.Credentials.ShouldContain(credential);
            principal.Credentials.Count.ShouldBe(1);
        }

        [Test]
        public void AddWalletOwnership_ShouldIncreaseWalletOwnershipsCollection()
        {
            // Arrange
            var principal = AxonPrincipal.CreateHuman();
            var ownership = Builders.CreateWalletOwnership();

            // Act
            principal.AddWalletOwnership(ownership);

            // Should
            principal.WalletOwnerships.ShouldContain(ownership);
            principal.WalletOwnerships.Count.ShouldBe(1);
        }
    }
}