using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Errors;
using Axon.Modules.Identity.Domain.Events;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Domain.Tests.TestData;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Domain.Tests.Aggregates;

[TestFixture]
public class AxonPrincipalCommandsTests
{
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    private AxonPrincipal CreateTestHumanPrincipal()
    {
        var result = AxonPrincipal.CreateHumanPrincipal(timeProvider: _timeProvider);
        result.IsSuccess.ShouldBeTrue();
        var principal = result.Value;
        principal.ClearDomainEvents(); // Clear creation event for cleaner test assertions
        return principal;
    }

    [TestFixture]
    public class LinkIdentityCredential : AxonPrincipalCommandsTests
    {
        [Test]
        public void WithValidData_ShouldSucceed()
        {
            // Arrange
            var principal = CreateTestHumanPrincipal();
            var providerType = Builders.DynamicProvider;
            var issuer = "https://test-issuer.com";
            var subject = "user123";
            var emailHash = EmailHash.From("test@example.com");

            // Act
            var result = principal.LinkIdentityCredential(
                providerType, issuer, subject, 
                emailHash: emailHash,
                timeProvider: _timeProvider);

            // Should
            result.IsSuccess.ShouldBeTrue();
            var credential = result.Value;
            credential.ProviderType.ShouldBe(providerType);
            credential.Issuer.ShouldBe(issuer);
            credential.Subject.ShouldBe(subject);
            credential.GetEmailHash().ShouldBe(emailHash);
            principal.Credentials.ShouldContain(credential);
        }

        [Test]
        public void ShouldRaiseIdentityCredentialLinkedEvent()
        {
            // Arrange
            var principal = CreateTestHumanPrincipal();
            var providerType = Builders.DynamicProvider;
            var issuer = "https://test-issuer.com";
            var subject = "user123";

            // Act
            var result = principal.LinkIdentityCredential(
                providerType, issuer, subject, timeProvider: _timeProvider);

            // Should
            result.IsSuccess.ShouldBeTrue();
            var events = principal.DomainEvents;
            events.ShouldHaveCount(1);
            var linkedEvent = events.First().ShouldBeOfType<IdentityCredentialLinkedEvent>().Subject;
            linkedEvent.AxonId.ShouldBe(principal.Id);
            linkedEvent.ProviderType.ShouldBe(providerType.Value);
            linkedEvent.Issuer.ShouldBe(issuer);
            linkedEvent.Subject.ShouldBe(subject);
        }

        [Test]
        public void WithDuplicateCredential_ShouldFail()
        {
            // Arrange
            var principal = CreateTestHumanPrincipal();
            var providerType = Builders.DynamicProvider;
            var issuer = "https://test-issuer.com";
            var subject = "user123";

            // First credential should succeed
            var firstResult = principal.LinkIdentityCredential(
                providerType, issuer, subject, timeProvider: _timeProvider);
            firstResult.IsSuccess.ShouldBeTrue();

            // Act - Second identical credential should fail
            var result = principal.LinkIdentityCredential(
                providerType, issuer, subject, timeProvider: _timeProvider);

            // Should
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.ShouldContain("UNIQUE");
        }

        [Test]
        public void WithInvalidIssuer_ShouldFail()
        {
            // Arrange
            var principal = CreateTestHumanPrincipal();

            // Act
            var result = principal.LinkIdentityCredential(
                Builders.DynamicProvider, "", "subject", timeProvider: _timeProvider);

            // Should
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.ShouldContain("ISSUER.REQUIRED");
        }

        [Test]
        public void WithInvalidSubject_ShouldFail()
        {
            // Arrange
            var principal = CreateTestHumanPrincipal();

            // Act
            var result = principal.LinkIdentityCredential(
                Builders.DynamicProvider, "issuer", "", timeProvider: _timeProvider);

            // Should
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.ShouldContain("SUBJECT.REQUIRED");
        }
    }

    [TestFixture]
    public class LinkWallet : AxonPrincipalCommandsTests
    {
        [Test]
        public void WithValidData_ShouldSucceed()
        {
            // Arrange
            var principal = CreateTestHumanPrincipal();
            var walletId = WalletId.New();
            var chainId = Builders.SolanaChain;
            var proofType = Builders.SignatureProof;
            var accessMode = AccessMode.Signing;
            var label = "My Main Wallet";

            // Act
            var result = principal.LinkWallet(
                walletId, chainId, proofType, accessMode, label, _timeProvider);

            // Should
            result.IsSuccess.ShouldBeTrue();
            var ownership = result.Value;
            ownership.WalletId.ShouldBe(walletId);
            ownership.ChainId.ShouldBe(chainId);
            ownership.ProofType.ShouldBe(proofType);
            ownership.AccessMode.ShouldBe(accessMode);
            ownership.Label.ShouldBe(label);
            principal.WalletOwnerships.ShouldContain(ownership);
        }

        [Test]
        public void ShouldRaiseWalletOwnershipLinkedEvent()
        {
            // Arrange
            var principal = CreateTestHumanPrincipal();
            var walletId = WalletId.New();
            var chainId = Builders.SolanaChain;
            var proofType = Builders.SignatureProof;

            // Act
            var result = principal.LinkWallet(
                walletId, chainId, proofType, timeProvider: _timeProvider);

            // Should
            result.IsSuccess.ShouldBeTrue();
            var events = principal.DomainEvents;
            events.ShouldHaveCount(1);
            var linkedEvent = events.First().ShouldBeOfType<WalletOwnershipLinkedEvent>().Subject;
            linkedEvent.PrincipalId.ShouldBe(principal.Id.Value.ToString());
            linkedEvent.WalletId.ShouldBe(walletId.Value.ToString());
            linkedEvent.ProofType.ShouldBe(proofType.Value);
        }

        [Test]
        public void WithDuplicateWallet_ShouldFail()
        {
            // Arrange
            var principal = CreateTestHumanPrincipal();
            var walletId = WalletId.New();
            var chainId = Builders.SolanaChain;
            var proofType = Builders.SignatureProof;

            // First link should succeed
            var firstResult = principal.LinkWallet(
                walletId, chainId, proofType, timeProvider: _timeProvider);
            firstResult.IsSuccess.ShouldBeTrue();

            // Act - Second link of same wallet should fail
            var result = principal.LinkWallet(
                walletId, chainId, proofType, timeProvider: _timeProvider);

            // Should
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.ShouldContain("OWNED");
        }
    }

    [TestFixture]
    public class UpdatePreferredLanguage : AxonPrincipalCommandsTests
    {
        [Test]
        public void WithValidLanguage_ShouldSucceed()
        {
            // Arrange
            var principal = CreateTestHumanPrincipal();
            var newLanguage = Builders.SpanishLanguage;

            // Act
            var result = principal.UpdatePreferredLanguage(newLanguage, _timeProvider);

            // Should
            result.IsSuccess.ShouldBeTrue();
            principal.Profile.PreferredLanguage.ShouldBe(newLanguage);
        }

        [Test]
        public void ShouldRaiseProfileLanguageChangedEvent()
        {
            // Arrange
            var principal = CreateTestHumanPrincipal();
            var newLanguage = Builders.SpanishLanguage;

            // Act
            var result = principal.UpdatePreferredLanguage(newLanguage, _timeProvider);

            // Should
            result.IsSuccess.ShouldBeTrue();
            var events = principal.DomainEvents;
            events.ShouldHaveCount(1);
            var languageEvent = events.First().ShouldBeOfType<ProfileLanguageChangedEvent>().Subject;
            languageEvent.PrincipalId.ShouldBe(principal.Id);
            languageEvent.NewLanguage.ShouldBe(newLanguage.Value);
        }
    }

    [TestFixture]
    public class UpdateRiskTier : AxonPrincipalCommandsTests
    {
        [Test]
        public void WithValidRiskTier_ShouldSucceed()
        {
            // Arrange
            var principal = CreateTestHumanPrincipal();
            var newRiskTier = Builders.HighRiskTier;

            // Act
            var result = principal.UpdateRiskTier(newRiskTier, _timeProvider);

            // Should
            result.IsSuccess.ShouldBeTrue();
            principal.Profile.RiskTier.ShouldBe(newRiskTier);
        }

        [Test]
        public void ShouldRaiseProfileRiskTierChangedEvent()
        {
            // Arrange
            var principal = CreateTestHumanPrincipal();
            var newRiskTier = Builders.MediumRiskTier;

            // Act
            var result = principal.UpdateRiskTier(newRiskTier, _timeProvider);

            // Should
            result.IsSuccess.ShouldBeTrue();
            var events = principal.DomainEvents;
            events.ShouldHaveCount(1);
            var riskTierEvent = events.First().ShouldBeOfType<ProfileRiskTierChangedEvent>().Subject;
            riskTierEvent.PrincipalId.ShouldBe(principal.Id);
            riskTierEvent.NewRiskTier.ShouldBe(newRiskTier.Value);
        }
    }

    [TestFixture]
    public class SetDefaultWalletForChain : AxonPrincipalCommandsTests
    {
        [Test]
        public void WithVerifiedSigningWallet_ShouldSucceed()
        {
            // Arrange
            var principal = CreateTestHumanPrincipal();
            var walletId = WalletId.New();
            var chainId = Builders.SolanaChain;
            var proofType = Builders.SignatureProof;

            // First link and verify wallet
            var linkResult = principal.LinkWallet(
                walletId, chainId, proofType, AccessMode.Signing, timeProvider: _timeProvider);
            linkResult.IsSuccess.ShouldBeTrue();
            
            var verifyResult = principal.VerifyWalletOwnership(walletId, timeProvider: _timeProvider);
            verifyResult.IsSuccess.ShouldBeTrue();

            principal.ClearDomainEvents();

            // Act
            var result = principal.SetDefaultWalletForChain(chainId, walletId, _timeProvider);

            // Should
            result.IsSuccess.ShouldBeTrue();
            principal.GetDefaultWalletForChain(chainId).ShouldBe(walletId);
            principal.HasDefaultWalletForChain(chainId).ShouldBeTrue();
        }

        [Test]
        public void ShouldRaiseDefaultWalletChangedEvent()
        {
            // Arrange
            var principal = CreateTestHumanPrincipal();
            var walletId = WalletId.New();
            var chainId = Builders.SolanaChain;

            // Setup verified signing wallet
            principal.LinkWallet(walletId, chainId, Builders.SignatureProof, AccessMode.Signing, timeProvider: _timeProvider);
            principal.VerifyWalletOwnership(walletId, timeProvider: _timeProvider);
            principal.ClearDomainEvents();

            // Act
            var result = principal.SetDefaultWalletForChain(chainId, walletId, _timeProvider);

            // Should
            result.IsSuccess.ShouldBeTrue();
            var events = principal.DomainEvents;
            events.ShouldHaveCount(1);
            var defaultEvent = events.First().ShouldBeOfType<DefaultWalletChangedEvent>().Subject;
            defaultEvent.PrincipalId.ShouldBe(principal.Id.Value.ToString());
            defaultEvent.ChainId.ShouldBe(chainId.Value);
            defaultEvent.NewDefaultWalletId.ShouldBe(walletId.Value.ToString());
        }

        [Test]
        public void WithWalletNotOwned_ShouldFail()
        {
            // Arrange
            var principal = CreateTestHumanPrincipal();
            var walletId = WalletId.New();
            var chainId = Builders.SolanaChain;

            // Act
            var result = principal.SetDefaultWalletForChain(chainId, walletId, _timeProvider);

            // Should
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.ShouldContain("NOT_OWNED");
        }
    }

    [TestFixture]
    public class SoftDelete : AxonPrincipalCommandsTests
    {
        [Test]
        public void WithNoActiveWallets_ShouldSucceed()
        {
            // Arrange
            var principal = CreateTestHumanPrincipal();

            // Act
            var result = principal.SoftDelete(_timeProvider);

            // Should
            result.IsSuccess.ShouldBeTrue();
            principal.IsDeleted.ShouldBeTrue();
        }

        [Test]
        public void ShouldRaisePrincipalSoftDeletedEvent()
        {
            // Arrange
            var principal = CreateTestHumanPrincipal();

            // Act
            var result = principal.SoftDelete(_timeProvider);

            // Should
            result.IsSuccess.ShouldBeTrue();
            var events = principal.DomainEvents;
            events.ShouldHaveCount(1);
            var deletedEvent = events.First().ShouldBeOfType<PrincipalSoftDeletedEvent>().Subject;
            deletedEvent.PrincipalId.ShouldBe(principal.Id);
        }
    }

    [TestFixture]
    public class Restore : AxonPrincipalCommandsTests
    {
        [Test]
        public void DeletedPrincipal_ShouldSucceed()
        {
            // Arrange
            var principal = CreateTestHumanPrincipal();
            principal.SoftDelete(_timeProvider);
            principal.ClearDomainEvents();

            // Act
            var result = principal.Restore("Test restoration", _timeProvider);

            // Should
            result.IsSuccess.ShouldBeTrue();
            principal.IsDeleted.ShouldBeFalse();
        }

        [Test]
        public void ShouldRaisePrincipalRestoredEvent()
        {
            // Arrange
            var principal = CreateTestHumanPrincipal();
            principal.SoftDelete(_timeProvider);
            principal.ClearDomainEvents();

            // Act
            var result = principal.Restore("Test restoration", _timeProvider);

            // Should
            result.IsSuccess.ShouldBeTrue();
            var events = principal.DomainEvents;
            events.ShouldHaveCount(1);
            var restoredEvent = events.First().ShouldBeOfType<PrincipalRestoredEvent>().Subject;
            restoredEvent.PrincipalId.ShouldBe(principal.Id);
            restoredEvent.Reason.ShouldBe("Test restoration");
        }
    }
}