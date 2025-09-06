using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Rules;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Domain.Tests.Rules;

[TestFixture]
public class CredentialMustBeUniqueRuleTests
{
    private static IdentityCredential CreateCredential(
        ProviderType providerType,
        string issuer,
        string subject,
        bool isDeleted = false)
    {
        var axonId = AxonId.New();
        var credentialResult = IdentityCredential.Create(
            axonId,
            providerType,
            issuer,
            subject,
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
    public void IsBroken_WhenNoExistingCredentials_ReturnsFalse()
    {
        // Arrange
        var providerType = ProviderType.From("oidc");
        var existingCredentials = new List<IdentityCredential>();
        
        var rule = new CredentialMustBeUniqueRule(
            providerType,
            "google.com",
            "user123",
            existingCredentials);

        // Act
        var result = rule.IsBroken();

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public void IsBroken_WhenExactDuplicateExists_ReturnsTrue()
    {
        // Arrange
        var providerType = ProviderType.From("siws");
        var issuer = "github.com";
        var subject = "user456";
        
        var existingCredential = CreateCredential(providerType, issuer, subject);
        var existingCredentials = new List<IdentityCredential> { existingCredential };
        
        var rule = new CredentialMustBeUniqueRule(
            providerType,
            issuer,
            subject,
            existingCredentials);

        // Act
        var result = rule.IsBroken();

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public void IsBroken_WhenDuplicateIsDeleted_ReturnsFalse()
    {
        // Arrange
        var providerType = ProviderType.From("service_api");
        var issuer = "microsoft.com";
        var subject = "user789";
        
        var deletedCredential = CreateCredential(providerType, issuer, subject, isDeleted: true);
        var existingCredentials = new List<IdentityCredential> { deletedCredential };
        
        var rule = new CredentialMustBeUniqueRule(
            providerType,
            issuer,
            subject,
            existingCredentials);

        // Act
        var result = rule.IsBroken();

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public void IsBroken_WhenDifferentProvider_ReturnsFalse()
    {
        // Arrange
        var issuer = "example.com";
        var subject = "sameuser";
        
        var existingCredential = CreateCredential(
            ProviderType.From("oidc"), issuer, subject);
        var existingCredentials = new List<IdentityCredential> { existingCredential };
        
        var rule = new CredentialMustBeUniqueRule(
            ProviderType.From("siws"), // Different provider
            issuer,
            subject,
            existingCredentials);

        // Act
        var result = rule.IsBroken();

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public void IsBroken_WhenDifferentIssuer_ReturnsFalse()
    {
        // Arrange
        var providerType = ProviderType.From("dynamic");
        var subject = "sameuser";
        
        var existingCredential = CreateCredential(
            providerType, "issuer1.com", subject);
        var existingCredentials = new List<IdentityCredential> { existingCredential };
        
        var rule = new CredentialMustBeUniqueRule(
            providerType,
            "issuer2.com", // Different issuer
            subject,
            existingCredentials);

        // Act
        var result = rule.IsBroken();

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public void IsBroken_WhenDifferentSubject_ReturnsFalse()
    {
        // Arrange
        var providerType = ProviderType.From("dynamic");
        var issuer = "example.com";
        
        var existingCredential = CreateCredential(
            providerType, issuer, "user1");
        var existingCredentials = new List<IdentityCredential> { existingCredential };
        
        var rule = new CredentialMustBeUniqueRule(
            providerType,
            issuer,
            "user2", // Different subject
            existingCredentials);

        // Act
        var result = rule.IsBroken();

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public void IsBroken_WithMultipleCredentials_FindsDuplicate()
    {
        // Arrange
        var providerType = ProviderType.From("oidc");
        var issuer = "auth0.com";
        var subject = "targetuser";
        
        var credentials = new List<IdentityCredential>
        {
            CreateCredential(ProviderType.From("oidc"), "google.com", "user1"),
            CreateCredential(ProviderType.From("siws"), "github.com", "user2"),
            CreateCredential(providerType, issuer, subject), // This is the duplicate
            CreateCredential(ProviderType.From("service_api"), "microsoft.com", "user3")
        };
        
        var rule = new CredentialMustBeUniqueRule(
            providerType,
            issuer,
            subject,
            credentials);

        // Act
        var result = rule.IsBroken();

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public void Message_ContainsProviderIssuerAndSubject()
    {
        // Arrange
        var providerType = ProviderType.From("unknown");
        var issuer = "custom.com";
        var subject = "user123";
        
        var rule = new CredentialMustBeUniqueRule(
            providerType,
            issuer,
            subject,
            new List<IdentityCredential>());

        // Act & Assert
        rule.Message.ShouldContain(providerType.Value);
        rule.Message.ShouldContain(issuer);
        rule.Message.ShouldContain(subject);
    }

    [Test]
    public void Code_ContainsExpectedValue()
    {
        // Arrange
        var rule = new CredentialMustBeUniqueRule(
            ProviderType.From("unknown"),
            "test.com",
            "user",
            new List<IdentityCredential>());

        // Act & Assert
        rule.Code.ShouldBe("IDENTITY.CREDENTIAL.DUPLICATE");
    }

    [Test]
    public async Task IsBrokenAsync_ReturnsConsistentWithIsBroken()
    {
        // Arrange
        var providerType = ProviderType.From("dynamic");
        var issuer = "async.com";
        var subject = "asyncuser";
        
        var existingCredential = CreateCredential(providerType, issuer, subject);
        var existingCredentials = new List<IdentityCredential> { existingCredential };
        
        var rule = new CredentialMustBeUniqueRule(
            providerType,
            issuer,
            subject,
            existingCredentials);

        // Act
        var syncResult = rule.IsBroken();
        var asyncResult = await rule.IsBrokenAsync();

        // Assert
        asyncResult.ShouldBe(syncResult);
        asyncResult.ShouldBeTrue();
    }

    [TestCase("oidc", "google.com", "user1", "oidc", "google.com", "user1", true)]
    [TestCase("oidc", "google.com", "user1", "siws", "google.com", "user1", false)]
    [TestCase("oidc", "google.com", "user1", "oidc", "github.com", "user1", false)]
    [TestCase("oidc", "google.com", "user1", "oidc", "google.com", "user2", false)]
    public void IsBroken_WithVariousCombinations_ReturnsExpectedResult(
        string existingProvider, string existingIssuer, string existingSubject,
        string newProvider, string newIssuer, string newSubject,
        bool expectedDuplicate)
    {
        // Arrange
        var existingCredential = CreateCredential(
            ProviderType.From(existingProvider),
            existingIssuer,
            existingSubject);
        
        var rule = new CredentialMustBeUniqueRule(
            ProviderType.From(newProvider),
            newIssuer,
            newSubject,
            new[] { existingCredential });

        // Act
        var result = rule.IsBroken();

        // Assert
        result.ShouldBe(expectedDuplicate);
    }
}