using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Rules;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Domain.Tests.Rules;

[TestFixture]
public class PrincipalCanBeDeletedRuleTests
{
    private static WalletOwnership CreateWalletOwnership(bool isActive = true, bool isDeleted = false)
    {
        var axonId = AxonId.New();
        var walletId = WalletId.New();
        var chainId = ChainId.From("ethereum");
        var proofType = ProofType.From("direct_signature");
        var accessMode = AccessMode.From("signing");
        var state = isActive ? OwnershipState.Verified : OwnershipState.Pending;
        
        var ownershipResult = WalletOwnership.Create(
            axonId,
            walletId,
            chainId,
            proofType,
            accessMode,
            state);

        var ownership = ownershipResult.Value;
        
        if (isDeleted)
        {
            ownership.SoftDelete();
        }

        return ownership;
    }

    private static IdentityCredential CreateCredential(bool isDeleted = false)
    {
        var axonId = AxonId.New();
        var credentialResult = IdentityCredential.Create(
            axonId,
            ProviderType.From("oidc"),
            "google.com",
            "user123",
            null,
            DateTimeOffset.UtcNow);

        var credential = credentialResult.Value;
        
        if (isDeleted)
        {
            credential.SoftDelete();
        }

        return credential;
    }

    [Test]
    public void IsBroken_WhenNoOwnershipOrCredentials_ReturnsFalse()
    {
        // Arrange
        var rule = new PrincipalCanBeDeletedRule(
            new List<WalletOwnership>(),
            new List<IdentityCredential>());

        // Act
        var result = rule.IsBroken();

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public void IsBroken_WhenHasActiveWalletOwnership_ReturnsTrue()
    {
        // Arrange
        var activeOwnership = CreateWalletOwnership(isActive: true);
        var rule = new PrincipalCanBeDeletedRule(
            new[] { activeOwnership },
            new List<IdentityCredential>());

        // Act
        var result = rule.IsBroken();

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public void IsBroken_WhenHasActiveCredential_ReturnsTrue()
    {
        // Arrange
        var activeCredential = CreateCredential(isDeleted: false);
        var rule = new PrincipalCanBeDeletedRule(
            new List<WalletOwnership>(),
            new[] { activeCredential });

        // Act
        var result = rule.IsBroken();

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public void IsBroken_WhenHasBothActiveOwnershipAndCredential_ReturnsTrue()
    {
        // Arrange
        var activeOwnership = CreateWalletOwnership(isActive: true);
        var activeCredential = CreateCredential(isDeleted: false);
        
        var rule = new PrincipalCanBeDeletedRule(
            new[] { activeOwnership },
            new[] { activeCredential });

        // Act
        var result = rule.IsBroken();

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public void IsBroken_WhenAllOwnershipsInactive_AndNoCredentials_ReturnsFalse()
    {
        // Arrange
        var inactiveOwnership = CreateWalletOwnership(isActive: false);
        var rule = new PrincipalCanBeDeletedRule(
            new[] { inactiveOwnership },
            new List<IdentityCredential>());

        // Act
        var result = rule.IsBroken();

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public void IsBroken_WhenAllCredentialsDeleted_AndNoOwnerships_ReturnsFalse()
    {
        // Arrange
        var deletedCredential = CreateCredential(isDeleted: true);
        var rule = new PrincipalCanBeDeletedRule(
            new List<WalletOwnership>(),
            new[] { deletedCredential });

        // Act
        var result = rule.IsBroken();

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public void IsBroken_WhenMixedActiveAndInactive_ReturnsTrue()
    {
        // Arrange
        var activeOwnership = CreateWalletOwnership(isActive: true);
        var inactiveOwnership = CreateWalletOwnership(isActive: false);
        var activeCredential = CreateCredential(isDeleted: false);
        var deletedCredential = CreateCredential(isDeleted: true);
        
        var rule = new PrincipalCanBeDeletedRule(
            new[] { activeOwnership, inactiveOwnership },
            new[] { activeCredential, deletedCredential });

        // Act
        var result = rule.IsBroken();

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public void IsBroken_WhenAllInactiveAndDeleted_ReturnsFalse()
    {
        // Arrange
        var inactiveOwnership1 = CreateWalletOwnership(isActive: false);
        var inactiveOwnership2 = CreateWalletOwnership(isActive: false);
        var deletedCredential1 = CreateCredential(isDeleted: true);
        var deletedCredential2 = CreateCredential(isDeleted: true);
        
        var rule = new PrincipalCanBeDeletedRule(
            new[] { inactiveOwnership1, inactiveOwnership2 },
            new[] { deletedCredential1, deletedCredential2 });

        // Act
        var result = rule.IsBroken();

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public void Message_ContainsExpectedText()
    {
        // Arrange
        var rule = new PrincipalCanBeDeletedRule(
            new List<WalletOwnership>(),
            new List<IdentityCredential>());

        // Act & Assert
        rule.Message.ShouldBe("Principal cannot be deleted while having active wallet ownerships or credentials.");
    }

    [Test]
    public void Code_ContainsExpectedValue()
    {
        // Arrange
        var rule = new PrincipalCanBeDeletedRule(
            new List<WalletOwnership>(),
            new List<IdentityCredential>());

        // Act & Assert
        rule.Code.ShouldBe("IDENTITY.PRINCIPAL.CANNOT_BE_DELETED");
    }

    [Test]
    public async Task IsBrokenAsync_ReturnsConsistentWithIsBroken()
    {
        // Arrange
        var activeOwnership = CreateWalletOwnership(isActive: true);
        var activeCredential = CreateCredential(isDeleted: false);
        
        var rule = new PrincipalCanBeDeletedRule(
            new[] { activeOwnership },
            new[] { activeCredential });

        // Act
        var syncResult = rule.IsBroken();
        var asyncResult = await rule.IsBrokenAsync();

        // Assert
        asyncResult.ShouldBe(syncResult);
        asyncResult.ShouldBeTrue();
    }

    [TestCase(true, false, true)]  // Active ownership, no credentials = cannot delete
    [TestCase(false, true, true)]  // No ownership, active credential = cannot delete
    [TestCase(true, true, true)]   // Both active = cannot delete
    [TestCase(false, false, false)] // Neither active = can delete
    public void IsBroken_WithVariousCombinations_ReturnsExpectedResult(
        bool hasActiveOwnership,
        bool hasActiveCredential,
        bool expectedBroken)
    {
        // Arrange
        var ownerships = hasActiveOwnership 
            ? new[] { CreateWalletOwnership(isActive: true) }
            : new WalletOwnership[0];
            
        var credentials = hasActiveCredential
            ? new[] { CreateCredential(isDeleted: false) }
            : new IdentityCredential[0];
        
        var rule = new PrincipalCanBeDeletedRule(ownerships, credentials);

        // Act
        var result = rule.IsBroken();

        // Assert
        result.ShouldBe(expectedBroken);
    }
}