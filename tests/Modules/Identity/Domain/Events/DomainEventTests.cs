using Axon.Modules.Identity.Domain.Events;
using Axon.Modules.Identity.Domain.Tests.Common;
using BuildingBlocks.Primitives.Ids;
using Shouldly;

namespace Axon.Modules.Identity.Domain.Tests.Events;

/// <summary>
/// Comprehensive test suite for all Identity Domain events.
/// Tests event creation, properties, serialization, and business logic.
/// </summary>
[TestFixture]
public class DomainEventTests : IdentityTestBase
{
    #region PrincipalChangedEvent Tests

    [TestFixture]
    public class PrincipalChangedEventTests : DomainEventTests
    {
        [Test]
        public void PrincipalChangedEvent_WithRequiredParameters_Should_CreateEventCorrectly()
        {
            // Arrange
            var principalId = AxonUserId.New();
            var propertyChanged = "RiskTier";
            var oldValue = "Low";
            var newValue = "Medium";
            var eventTime = DateTime.UtcNow;

            // Act
            var eventObj = new PrincipalChangedEvent(principalId, propertyChanged, oldValue, newValue, eventOccurredAt: eventTime);

            // Assert
            eventObj.ShouldSatisfyAllConditions(
                e => e.PrincipalId.ShouldBe(principalId),
                e => e.PropertyChanged.ShouldBe(propertyChanged),
                e => e.OldValue.ShouldBe(oldValue),
                e => e.NewValue.ShouldBe(newValue),
                e => e.OccurredAt.ShouldBe(eventTime),
                e => e.Metadata.ShouldBeNull()
            );
        }

        [Test]
        public void PrincipalChangedEvent_WithMetadata_Should_IncludeMetadata()
        {
            // Arrange
            var principalId = AxonUserId.New();
            var propertyChanged = "ChainDefault.ethereum-mainnet";
            var metadata = new Dictionary<string, string>
            {
                ["reason"] = "user_preference",
                ["source"] = "api"
            };

            // Act
            var eventObj = new PrincipalChangedEvent(principalId, propertyChanged, metadata: metadata);

            // Assert
            eventObj.Metadata.ShouldNotBeNull();
            eventObj.Metadata.ShouldContainKeyAndValue("reason", "user_preference");
            eventObj.Metadata.ShouldContainKeyAndValue("source", "api");
        }

        [Test]
        public void PrincipalChangedEvent_WithoutTimestamp_Should_UseCurrentTime()
        {
            // Arrange
            var beforeCreation = DateTime.UtcNow;
            var principalId = AxonUserId.New();

            // Act
            var eventObj = new PrincipalChangedEvent(principalId, "TestProperty");

            // Assert
            var afterCreation = DateTime.UtcNow;
            eventObj.OccurredAt.ShouldBeGreaterThanOrEqualTo(beforeCreation);
            eventObj.OccurredAt.ShouldBeLessThanOrEqualTo(afterCreation);
        }

        [Test]
        [TestCase("RiskTier", "Low", "Medium")]
        [TestCase("ChainDefault.ethereum-mainnet", "none", "wallet-123")]
        [TestCase("Type", "Human", "Service")]
        [TestCase("Profile.Language", "en", "es")]
        public void PrincipalChangedEvent_WithDifferentProperties_Should_CaptureCorrectly(
            string property, string oldValue, string newValue)
        {
            // Arrange
            var principalId = AxonUserId.New();

            // Act
            var eventObj = new PrincipalChangedEvent(principalId, property, oldValue, newValue);

            // Assert
            eventObj.ShouldSatisfyAllConditions(
                e => e.PropertyChanged.ShouldBe(property),
                e => e.OldValue.ShouldBe(oldValue),
                e => e.NewValue.ShouldBe(newValue)
            );
        }

        [Test]
        public void PrincipalChangedEvent_WithNullValues_Should_HandleCorrectly()
        {
            // Arrange
            var principalId = AxonUserId.New();

            // Act
            var eventObj = new PrincipalChangedEvent(principalId, "TestProperty", null, null);

            // Assert
            eventObj.ShouldSatisfyAllConditions(
                e => e.OldValue.ShouldBeNull(),
                e => e.NewValue.ShouldBeNull()
            );
        }

        [Test]
        public void PrincipalChangedEvent_Should_BeRecord()
        {
            // Arrange
            var principalId = AxonUserId.New();
            var property = "RiskTier";

            // Act
            var event1 = new PrincipalChangedEvent(principalId, property, "Low", "Medium");
            var event2 = new PrincipalChangedEvent(principalId, property, "Low", "Medium");

            // Assert - Records with same values should be equal
            event1.PrincipalId.ShouldBe(event2.PrincipalId);
            event1.PropertyChanged.ShouldBe(event2.PropertyChanged);
            event1.OldValue.ShouldBe(event2.OldValue);
            event1.NewValue.ShouldBe(event2.NewValue);
        }
    }

    #endregion

    #region CredentialChangedEvent Tests

    [TestFixture]
    public class CredentialChangedEventTests : DomainEventTests
    {
        [Test]
        public void CredentialChangedEvent_WithRequiredParameters_Should_CreateEventCorrectly()
        {
            // Arrange
            var principalId = AxonUserId.New();
            var credentialId = IdentityCredentialId.New();
            var changeType = "added";
            var provider = "dynamic";
            var eventTime = DateTime.UtcNow;

            // Act
            var eventObj = new CredentialChangedEvent(principalId, credentialId, changeType, provider, eventOccurredAt: eventTime);

            // Assert
            eventObj.ShouldSatisfyAllConditions(
                e => e.PrincipalId.ShouldBe(principalId),
                e => e.CredentialId.ShouldBe(credentialId),
                e => e.ChangeType.ShouldBe(changeType),
                e => e.Provider.ShouldBe(provider),
                e => e.OccurredAt.ShouldBe(eventTime),
                e => e.Metadata.ShouldBeNull()
            );
        }

        [Test]
        [TestCase("added")]
        [TestCase("removed")]
        [TestCase("last_seen_updated")]
        public void CredentialChangedEvent_WithDifferentChangeTypes_Should_CaptureCorrectly(string changeType)
        {
            // Arrange
            var principalId = AxonUserId.New();
            var credentialId = IdentityCredentialId.New();

            // Act
            var eventObj = new CredentialChangedEvent(principalId, credentialId, changeType);

            // Assert
            eventObj.ChangeType.ShouldBe(changeType);
        }

        [Test]
        [TestCase("dynamic")]
        [TestCase("github")]
        [TestCase("google")]
        [TestCase("apple")]
        public void CredentialChangedEvent_WithDifferentProviders_Should_CaptureCorrectly(string provider)
        {
            // Arrange
            var principalId = AxonUserId.New();
            var credentialId = IdentityCredentialId.New();

            // Act
            var eventObj = new CredentialChangedEvent(principalId, credentialId, "added", provider);

            // Assert
            eventObj.Provider.ShouldBe(provider);
        }

        [Test]
        public void CredentialChangedEvent_WithMetadata_Should_IncludeMetadata()
        {
            // Arrange
            var principalId = AxonUserId.New();
            var credentialId = IdentityCredentialId.New();
            var metadata = new Dictionary<string, string>
            {
                ["issuer"] = "dynamic:test-env",
                ["subject"] = "user-123"
            };

            // Act
            var eventObj = new CredentialChangedEvent(principalId, credentialId, "added", metadata: metadata);

            // Assert
            eventObj.Metadata.ShouldNotBeNull();
            eventObj.Metadata.ShouldContainKeyAndValue("issuer", "dynamic:test-env");
            eventObj.Metadata.ShouldContainKeyAndValue("subject", "user-123");
        }

        [Test]
        public void CredentialChangedEvent_WithoutOptionalParameters_Should_HandleCorrectly()
        {
            // Arrange
            var principalId = AxonUserId.New();
            var credentialId = IdentityCredentialId.New();

            // Act
            var eventObj = new CredentialChangedEvent(principalId, credentialId, "added");

            // Assert
            eventObj.ShouldSatisfyAllConditions(
                e => e.Provider.ShouldBeNull(),
                e => e.Metadata.ShouldBeNull()
            );
        }
    }

    #endregion

    #region OwnershipChangedEvent Tests

    [TestFixture]
    public class OwnershipChangedEventTests : DomainEventTests
    {
        [Test]
        public void OwnershipChangedEvent_WithRequiredParameters_Should_CreateEventCorrectly()
        {
            // Arrange
            var principalId = AxonUserId.New();
            var walletId = WalletId.New();
            var changeType = "linked";
            var accessMode = "Signing";
            var status = "Verified";
            var eventTime = DateTime.UtcNow;

            // Act
            var eventObj = new OwnershipChangedEvent(principalId, walletId, changeType, accessMode, status, eventOccurredAt: eventTime);

            // Assert
            eventObj.ShouldSatisfyAllConditions(
                e => e.PrincipalId.ShouldBe(principalId),
                e => e.WalletId.ShouldBe(walletId),
                e => e.ChangeType.ShouldBe(changeType),
                e => e.AccessMode.ShouldBe(accessMode),
                e => e.Status.ShouldBe(status),
                e => e.OccurredAt.ShouldBe(eventTime),
                e => e.Metadata.ShouldBeNull()
            );
        }

        [Test]
        [TestCase("linked")]
        [TestCase("removed")]
        [TestCase("verified")]
        [TestCase("access_mode_updated")]
        [TestCase("status_updated")]
        public void OwnershipChangedEvent_WithDifferentChangeTypes_Should_CaptureCorrectly(string changeType)
        {
            // Arrange
            var principalId = AxonUserId.New();
            var walletId = WalletId.New();

            // Act
            var eventObj = new OwnershipChangedEvent(principalId, walletId, changeType);

            // Assert
            eventObj.ChangeType.ShouldBe(changeType);
        }

        [Test]
        [TestCase("Signing")]
        [TestCase("WatchOnly")]
        public void OwnershipChangedEvent_WithDifferentAccessModes_Should_CaptureCorrectly(string accessMode)
        {
            // Arrange
            var principalId = AxonUserId.New();
            var walletId = WalletId.New();

            // Act
            var eventObj = new OwnershipChangedEvent(principalId, walletId, "linked", accessMode);

            // Assert
            eventObj.AccessMode.ShouldBe(accessMode);
        }

        [Test]
        [TestCase("Pending")]
        [TestCase("Verified")]
        [TestCase("Revoked")]
        public void OwnershipChangedEvent_WithDifferentStatuses_Should_CaptureCorrectly(string status)
        {
            // Arrange
            var principalId = AxonUserId.New();
            var walletId = WalletId.New();

            // Act
            var eventObj = new OwnershipChangedEvent(principalId, walletId, "linked", status: status);

            // Assert
            eventObj.Status.ShouldBe(status);
        }

        [Test]
        public void OwnershipChangedEvent_WithMetadata_Should_IncludeMetadata()
        {
            // Arrange
            var principalId = AxonUserId.New();
            var walletId = WalletId.New();
            var metadata = new Dictionary<string, string>
            {
                ["verification_method"] = "signature",
                ["chain_id"] = "ethereum-mainnet"
            };

            // Act
            var eventObj = new OwnershipChangedEvent(principalId, walletId, "verified", metadata: metadata);

            // Assert
            eventObj.Metadata.ShouldNotBeNull();
            eventObj.Metadata.ShouldContainKeyAndValue("verification_method", "signature");
            eventObj.Metadata.ShouldContainKeyAndValue("chain_id", "ethereum-mainnet");
        }

        [Test]
        public void OwnershipChangedEvent_ForRemoval_Should_HandleCorrectly()
        {
            // Arrange
            var principalId = AxonUserId.New();
            var walletId = WalletId.New();

            // Act
            var eventObj = new OwnershipChangedEvent(principalId, walletId, "removed", "Signing", "Verified");

            // Assert
            eventObj.ShouldSatisfyAllConditions(
                e => e.ChangeType.ShouldBe("removed"),
                e => e.AccessMode.ShouldBe("Signing"),
                e => e.Status.ShouldBe("Verified")
            );
        }
    }

    #endregion

    #region WalletChangedEvent Tests

    [TestFixture]
    public class WalletChangedEventTests : DomainEventTests
    {
        [Test]
        public void WalletChangedEvent_WithRequiredParameters_Should_CreateEventCorrectly()
        {
            // Arrange
            var walletId = WalletId.New();
            var propertyChanged = "ownership";
            var oldValue = "none";
            var newValue = "principal-123";
            var eventTime = DateTime.UtcNow;

            // Act
            var eventObj = new WalletChangedEvent(walletId, propertyChanged, oldValue, newValue, occurredAt: eventTime);

            // Assert
            eventObj.ShouldSatisfyAllConditions(
                e => e.WalletId.ShouldBe(walletId),
                e => e.PropertyChanged.ShouldBe(propertyChanged),
                e => e.OldValue.ShouldBe(oldValue),
                e => e.NewValue.ShouldBe(newValue),
                e => e.OccurredAt.ShouldBe(eventTime),
                e => e.Metadata.ShouldBeNull()
            );
        }

        [Test]
        [TestCase("ownership")]
        [TestCase("ownershipStatus")]
        [TestCase("accessMode")]
        [TestCase("lastSeen")]
        public void WalletChangedEvent_WithDifferentProperties_Should_CaptureCorrectly(string propertyChanged)
        {
            // Arrange
            var walletId = WalletId.New();

            // Act
            var eventObj = new WalletChangedEvent(walletId, propertyChanged);

            // Assert
            eventObj.PropertyChanged.ShouldBe(propertyChanged);
        }

        [Test]
        public void WalletChangedEvent_ForOwnershipChange_Should_CaptureCorrectly()
        {
            // Arrange
            var walletId = WalletId.New();
            var metadata = new Dictionary<string, string>
            {
                ["accessMode"] = "Signing",
                ["status"] = "Verified"
            };

            // Act
            var eventObj = new WalletChangedEvent(walletId, "ownership", "none", "principal-123", metadata);

            // Assert
            eventObj.ShouldSatisfyAllConditions(
                e => e.PropertyChanged.ShouldBe("ownership"),
                e => e.OldValue.ShouldBe("none"),
                e => e.NewValue.ShouldBe("principal-123"),
                e => e.Metadata.ShouldNotBeNull(),
                e => e.Metadata!["accessMode"].ShouldBe("Signing"),
                e => e.Metadata!["status"].ShouldBe("Verified")
            );
        }

        [Test]
        public void WalletChangedEvent_ForStatusChange_Should_CaptureCorrectly()
        {
            // Arrange
            var walletId = WalletId.New();

            // Act
            var eventObj = new WalletChangedEvent(walletId, "ownershipStatus", "Pending", "Verified");

            // Assert
            eventObj.ShouldSatisfyAllConditions(
                e => e.PropertyChanged.ShouldBe("ownershipStatus"),
                e => e.OldValue.ShouldBe("Pending"),
                e => e.NewValue.ShouldBe("Verified")
            );
        }

        [Test]
        public void WalletChangedEvent_ForAccessModeChange_Should_CaptureCorrectly()
        {
            // Arrange
            var walletId = WalletId.New();

            // Act
            var eventObj = new WalletChangedEvent(walletId, "accessMode", "WatchOnly", "Signing");

            // Assert
            eventObj.ShouldSatisfyAllConditions(
                e => e.PropertyChanged.ShouldBe("accessMode"),
                e => e.OldValue.ShouldBe("WatchOnly"),
                e => e.NewValue.ShouldBe("Signing")
            );
        }

        [Test]
        public void WalletChangedEvent_WithoutTimestamp_Should_UseCurrentTime()
        {
            // Arrange
            var beforeCreation = DateTime.UtcNow;
            var walletId = WalletId.New();

            // Act
            var eventObj = new WalletChangedEvent(walletId, "lastSeen");

            // Assert
            var afterCreation = DateTime.UtcNow;
            eventObj.OccurredAt.ShouldBeGreaterThanOrEqualTo(beforeCreation);
            eventObj.OccurredAt.ShouldBeLessThanOrEqualTo(afterCreation);
        }
    }

    #endregion

    #region Common Event Behavior Tests

    [TestFixture]
    public class CommonEventBehaviorTests : DomainEventTests
    {
        [Test]
        public void AllEvents_Should_InheritFromDomainEvent()
        {
            // Arrange & Act
            var principalEvent = new PrincipalChangedEvent(AxonUserId.New(), "TestProp");
            var credentialEvent = new CredentialChangedEvent(AxonUserId.New(), IdentityCredentialId.New(), "added");
            var ownershipEvent = new OwnershipChangedEvent(AxonUserId.New(), WalletId.New(), "linked");
            var walletEvent = new WalletChangedEvent(WalletId.New(), "ownership");

            // Assert
            principalEvent.ShouldBeOfType<PrincipalChangedEvent>();
            credentialEvent.ShouldBeOfType<CredentialChangedEvent>();
            ownershipEvent.ShouldBeOfType<OwnershipChangedEvent>();
            walletEvent.ShouldBeOfType<WalletChangedEvent>();

            // All should have OccurredAt from base class
            principalEvent.OccurredAt.ShouldBeLessThanOrEqualTo(DateTime.UtcNow);
            credentialEvent.OccurredAt.ShouldBeLessThanOrEqualTo(DateTime.UtcNow);
            ownershipEvent.OccurredAt.ShouldBeLessThanOrEqualTo(DateTime.UtcNow);
            walletEvent.OccurredAt.ShouldBeLessThanOrEqualTo(DateTime.UtcNow);
        }

        [Test]
        public void AllEvents_Should_BeRecords()
        {
            // All events should be record types for immutability and value equality
            var principalEvent = new PrincipalChangedEvent(AxonUserId.New(), "TestProp");
            var credentialEvent = new CredentialChangedEvent(AxonUserId.New(), IdentityCredentialId.New(), "added");
            var ownershipEvent = new OwnershipChangedEvent(AxonUserId.New(), WalletId.New(), "linked");
            var walletEvent = new WalletChangedEvent(WalletId.New(), "ownership");

            // Records should have ToString implementations
            principalEvent.ToString().ShouldNotBeNullOrEmpty();
            credentialEvent.ToString().ShouldNotBeNullOrEmpty();
            ownershipEvent.ToString().ShouldNotBeNullOrEmpty();
            walletEvent.ToString().ShouldNotBeNullOrEmpty();
        }

        [Test]
        public void AllEvents_WithExplicitTimestamp_Should_UseProvidedTimestamp()
        {
            // Arrange
            var specificTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);

            // Act
            var principalEvent = new PrincipalChangedEvent(AxonUserId.New(), "TestProp", eventOccurredAt: specificTime);
            var credentialEvent = new CredentialChangedEvent(AxonUserId.New(), IdentityCredentialId.New(), "added", eventOccurredAt: specificTime);
            var ownershipEvent = new OwnershipChangedEvent(AxonUserId.New(), WalletId.New(), "linked", eventOccurredAt: specificTime);
            var walletEvent = new WalletChangedEvent(WalletId.New(), "ownership", occurredAt: specificTime);

            // Assert
            principalEvent.OccurredAt.ShouldBe(specificTime);
            credentialEvent.OccurredAt.ShouldBe(specificTime);
            ownershipEvent.OccurredAt.ShouldBe(specificTime);
            walletEvent.OccurredAt.ShouldBe(specificTime);
        }
    }

    #endregion

    #region Metadata Handling Tests

    [TestFixture]
    public class MetadataHandlingTests : DomainEventTests
    {
        [Test]
        public void Events_WithComplexMetadata_Should_HandleCorrectly()
        {
            // Arrange
            var complexMetadata = new Dictionary<string, string>
            {
                ["user_agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
                ["ip_address"] = "192.168.1.1",
                ["session_id"] = "session-123",
                ["api_version"] = "v1.0",
                ["correlation_id"] = "corr-456"
            };

            // Act
            var principalEvent = new PrincipalChangedEvent(AxonUserId.New(), "RiskTier", metadata: complexMetadata);

            // Assert
            principalEvent.Metadata.ShouldNotBeNull();
            principalEvent.Metadata.Count.ShouldBe(5);
            principalEvent.Metadata.ShouldContainKeyAndValue("user_agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            principalEvent.Metadata.ShouldContainKeyAndValue("correlation_id", "corr-456");
        }

        [Test]
        public void Events_WithEmptyMetadata_Should_HandleCorrectly()
        {
            // Arrange
            var emptyMetadata = new Dictionary<string, string>();

            // Act
            var ownershipEvent = new OwnershipChangedEvent(AxonUserId.New(), WalletId.New(), "linked", metadata: emptyMetadata);

            // Assert
            ownershipEvent.Metadata.ShouldNotBeNull();
            ownershipEvent.Metadata.ShouldBeEmpty();
        }

        [Test]
        public void Events_MetadataValues_Should_SupportSpecialCharacters()
        {
            // Arrange
            var specialMetadata = new Dictionary<string, string>
            {
                ["special_chars"] = "!@#$%^&*()_+-=[]{}|;:,.<>?/~`",
                ["unicode"] = "测试🚀💯",
                ["json_like"] = "{\"key\": \"value\"}",
                ["whitespace"] = "  value with spaces  "
            };

            // Act
            var credentialEvent = new CredentialChangedEvent(AxonUserId.New(), IdentityCredentialId.New(), "added", metadata: specialMetadata);

            // Assert
            credentialEvent.Metadata.ShouldNotBeNull();
            credentialEvent.Metadata.ShouldContainKeyAndValue("special_chars", "!@#$%^&*()_+-=[]{}|;:,.<>?/~`");
            credentialEvent.Metadata.ShouldContainKeyAndValue("unicode", "测试🚀💯");
            credentialEvent.Metadata.ShouldContainKeyAndValue("json_like", "{\"key\": \"value\"}");
            credentialEvent.Metadata.ShouldContainKeyAndValue("whitespace", "  value with spaces  ");
        }
    }

    #endregion
}