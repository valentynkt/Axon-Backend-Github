using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Tests.Common;
using BuildingBlocks.Primitives.Ids;
using Shouldly;

namespace Axon.Modules.Identity.Domain.Tests.Entities;

/// <summary>
/// Comprehensive test suite for IdentityCredential entity.
/// Tests creation, validation, and last seen updates.
/// </summary>
[TestFixture]
public class IdentityCredentialTests : IdentityTestBase
{
    #region Creation Tests

    [TestFixture]
    public class CreateTests : IdentityCredentialTests
    {
        [Test]
        public void Create_WithValidParameters_Should_CreateCredentialWithCorrectProperties()
        {
            // Arrange
            var principalId = AxonId.New();
            var provider = "dynamic";
            var issuer = "app.dynamicauth.com/test-env";
            var subject = "test-user-123";
            var timestamp = DateTime.UtcNow;

            // Act
            var credential = IdentityCredential.Create(principalId, provider, issuer, subject, timestamp);

            // Assert
            credential.ShouldSatisfyAllConditions(
                c => c.Id.Value.ShouldNotBe(Guid.Empty),
                c => c.PrincipalId.ShouldBe(principalId),
                c => c.Provider.ShouldBe(provider),
                c => c.Issuer.ShouldBe(issuer),
                c => c.Subject.ShouldBe(subject),
                c => c.LastSeenAt.ShouldBe(timestamp),
                c => c.CreatedAt.ShouldBeLessThanOrEqualTo(DateTime.UtcNow),
                c => c.IsDeleted.ShouldBeFalse()
            );
        }

        [Test]
        public void Create_WithNullTimestamp_Should_UseCurrentTime()
        {
            // Arrange
            var beforeCreation = DateTime.UtcNow;
            var principalId = AxonId.New();
            var provider = "dynamic";
            var issuer = "app.dynamicauth.com/test-env";
            var subject = "test-user-123";

            // Act
            var credential = IdentityCredential.Create(principalId, provider, issuer, subject);

            // Assert
            var afterCreation = DateTime.UtcNow;
            credential.LastSeenAt.ShouldBeGreaterThanOrEqualTo(beforeCreation);
            credential.LastSeenAt.ShouldBeLessThanOrEqualTo(afterCreation);
        }

        [Test]
        public void Create_ShouldGenerateUniqueIds()
        {
            // Arrange
            var principalId = AxonId.New();
            var provider = "dynamic";
            var issuer = "app.dynamicauth.com/test-env";
            var subject = "test-user-123";

            // Act
            var credential1 = IdentityCredential.Create(principalId, provider, issuer, subject);
            var credential2 = IdentityCredential.Create(principalId, provider, issuer, subject);

            // Assert
            credential1.Id.ShouldNotBe(credential2.Id);
        }

        [Test]
        [TestCase("dynamic")]
        [TestCase("github")]
        [TestCase("google")]
        [TestCase("apple")]
        public void Create_WithDifferentProviders_Should_AcceptAllValidProviders(string provider)
        {
            // Arrange
            var principalId = AxonId.New();
            var issuer = $"{provider}:test-env";
            var subject = "test-user-123";

            // Act
            var credential = IdentityCredential.Create(principalId, provider, issuer, subject);

            // Assert
            credential.Provider.ShouldBe(provider);
        }

        [Test]
        public void Create_WithEmptyStrings_Should_AllowEmptyValues()
        {
            // Arrange
            var principalId = AxonId.New();

            // Act
            var credential = IdentityCredential.Create(principalId, "", "", "");

            // Assert
            credential.ShouldSatisfyAllConditions(
                c => c.Provider.ShouldBe(""),
                c => c.Issuer.ShouldBe(""),
                c => c.Subject.ShouldBe("")
            );
        }
    }

    #endregion

    #region Last Seen Update Tests

    [TestFixture]
    public class UpdateLastSeenTests : IdentityCredentialTests
    {
        [Test]
        public void UpdateLastSeen_WithNewerTimestamp_Should_UpdateLastSeenAt()
        {
            // Arrange
            var initialTimestamp = DateTime.UtcNow.AddMinutes(-10);
            var credential = IdentityCredential.Create(
                AxonId.New(), "dynamic", "issuer", "subject", initialTimestamp);
            var newerTimestamp = DateTime.UtcNow;

            // Act
            credential.UpdateLastSeen(newerTimestamp);

            // Assert
            credential.LastSeenAt.ShouldBe(newerTimestamp);
        }

        [Test]
        public void UpdateLastSeen_WithOlderTimestamp_Should_NotUpdateLastSeenAt()
        {
            // Arrange
            var recentTimestamp = DateTime.UtcNow;
            var credential = IdentityCredential.Create(
                AxonId.New(), "dynamic", "issuer", "subject", recentTimestamp);
            var olderTimestamp = recentTimestamp.AddMinutes(-5);

            // Act
            credential.UpdateLastSeen(olderTimestamp);

            // Assert
            credential.LastSeenAt.ShouldBe(recentTimestamp); // Should remain unchanged
        }

        [Test]
        public void UpdateLastSeen_WithSameTimestamp_Should_NotChange()
        {
            // Arrange
            var timestamp = DateTime.UtcNow;
            var credential = IdentityCredential.Create(
                AxonId.New(), "dynamic", "issuer", "subject", timestamp);

            // Act
            credential.UpdateLastSeen(timestamp);

            // Assert
            credential.LastSeenAt.ShouldBe(timestamp);
        }

        [Test]
        public void UpdateLastSeen_WithMaxDateTime_Should_HandleExtremeValues()
        {
            // Arrange
            var credential = IdentityCredential.Create(
                AxonId.New(), "dynamic", "issuer", "subject");

            // Act
            credential.UpdateLastSeen(DateTime.MaxValue);

            // Assert
            credential.LastSeenAt.ShouldBe(DateTime.MaxValue);
        }

        [Test]
        public void UpdateLastSeen_MultipleUpdates_Should_KeepMostRecent()
        {
            // Arrange
            var baseTime = DateTime.UtcNow;
            var credential = IdentityCredential.Create(
                AxonId.New(), "dynamic", "issuer", "subject", baseTime);

            var time1 = baseTime.AddMinutes(1);
            var time2 = baseTime.AddMinutes(2);
            var time3 = baseTime.AddMinutes(1.5); // Between time1 and time2

            // Act
            credential.UpdateLastSeen(time1);
            credential.UpdateLastSeen(time2);
            credential.UpdateLastSeen(time3); // Should not update since time3 < time2

            // Assert
            credential.LastSeenAt.ShouldBe(time2); // Should remain at time2
        }
    }

    #endregion

    #region Equality and Comparison Tests

    [TestFixture]
    public class EqualityTests : IdentityCredentialTests
    {
        [Test]
        public void Credentials_WithSameProviderIssuerSubject_Should_BeConsideredEquivalent()
        {
            // Arrange
            var principalId1 = AxonId.New();
            var principalId2 = AxonId.New();
            var provider = "dynamic";
            var issuer = "app.dynamicauth.com/test-env";
            var subject = "test-user-123";

            // Act
            var credential1 = IdentityCredential.Create(principalId1, provider, issuer, subject);
            var credential2 = IdentityCredential.Create(principalId2, provider, issuer, subject);

            // Assert - While they have different IDs and principals, they represent the same logical credential
            credential1.ShouldSatisfyAllConditions(
                c => c.Provider.ShouldBe(credential2.Provider),
                c => c.Issuer.ShouldBe(credential2.Issuer),
                c => c.Subject.ShouldBe(credential2.Subject)
            );

            // But should have different IDs and principal IDs
            credential1.Id.ShouldNotBe(credential2.Id);
            credential1.PrincipalId.ShouldNotBe(credential2.PrincipalId);
        }

        [Test]
        public void Credentials_WithDifferentSubjects_Should_BeDifferent()
        {
            // Arrange
            var principalId = AxonId.New();
            var provider = "dynamic";
            var issuer = "app.dynamicauth.com/test-env";

            // Act
            var credential1 = IdentityCredential.Create(principalId, provider, issuer, "user1");
            var credential2 = IdentityCredential.Create(principalId, provider, issuer, "user2");

            // Assert
            credential1.Subject.ShouldNotBe(credential2.Subject);
        }
    }

    #endregion

    #region Edge Cases and Validation Tests

    [TestFixture]
    public class EdgeCasesTests : IdentityCredentialTests
    {
        [Test]
        public void Create_WithVeryLongStrings_Should_HandleLargeValues()
        {
            // Arrange
            var principalId = AxonId.New();
            var longString = new string('a', 1000);

            // Act
            var credential = IdentityCredential.Create(principalId, longString, longString, longString);

            // Assert
            credential.ShouldSatisfyAllConditions(
                c => c.Provider.ShouldBe(longString),
                c => c.Issuer.ShouldBe(longString),
                c => c.Subject.ShouldBe(longString)
            );
        }

        [Test]
        public void Create_WithSpecialCharacters_Should_HandleCorrectly()
        {
            // Arrange
            var principalId = AxonId.New();
            var specialChars = "!@#$%^&*()_+-=[]{}|;:,.<>?/~`";

            // Act
            var credential = IdentityCredential.Create(principalId, specialChars, specialChars, specialChars);

            // Assert
            credential.ShouldSatisfyAllConditions(
                c => c.Provider.ShouldBe(specialChars),
                c => c.Issuer.ShouldBe(specialChars),
                c => c.Subject.ShouldBe(specialChars)
            );
        }

        [Test]
        public void Create_WithUnicodeCharacters_Should_HandleCorrectly()
        {
            // Arrange
            var principalId = AxonId.New();
            var unicode = "测试用户🚀💯";

            // Act
            var credential = IdentityCredential.Create(principalId, unicode, unicode, unicode);

            // Assert
            credential.ShouldSatisfyAllConditions(
                c => c.Provider.ShouldBe(unicode),
                c => c.Issuer.ShouldBe(unicode),
                c => c.Subject.ShouldBe(unicode)
            );
        }

        [Test]
        public void UpdateLastSeen_ConcurrentUpdates_Should_HandleCorrectly()
        {
            // This test simulates concurrent updates to verify thread safety logic
            // Arrange
            var credential = IdentityCredential.Create(AxonId.New(), "dynamic", "issuer", "subject");
            var baseTime = DateTime.UtcNow;

            // Act - Simulate concurrent updates with varying timestamps
            var tasks = new List<Task>();
            var timestamps = new[]
            {
                baseTime.AddSeconds(1),
                baseTime.AddSeconds(3),
                baseTime.AddSeconds(2),
                baseTime.AddSeconds(5),
                baseTime.AddSeconds(4)
            };

            foreach (var timestamp in timestamps)
            {
                tasks.Add(Task.Run(() => credential.UpdateLastSeen(timestamp)));
            }

            Task.WaitAll(tasks.ToArray());

            // Assert - Should have the latest timestamp
            credential.LastSeenAt.ShouldBe(baseTime.AddSeconds(5));
        }
    }

    #endregion

    #region Inheritance and Base Class Tests

    [TestFixture]
    public class InheritanceTests : IdentityCredentialTests
    {
        [Test]
        public void IdentityCredential_Should_InheritFromAuditableDeletableEntity()
        {
            // Arrange & Act
            var credential = IdentityCredential.Create(AxonId.New(), "dynamic", "issuer", "subject");

            // Assert - Verify auditable properties are available
            var now = DateTimeOffset.UtcNow;
            credential.ShouldSatisfyAllConditions(
                c => c.CreatedAt.ShouldBeLessThanOrEqualTo(now),
                c => c.UpdatedAt.ShouldNotBeNull().ShouldBeLessThanOrEqualTo(now),
                c => c.IsDeleted.ShouldBeFalse(),
                c => c.DeletedAt.ShouldBeNull()
            );
        }

        [Test]
        public void IdentityCredential_Should_HaveUniquelyTypedId()
        {
            // Arrange & Act
            var credential = IdentityCredential.Create(AxonId.New(), "dynamic", "issuer", "subject");

            // Assert
            credential.Id.ShouldBeOfType<IdentityCredentialId>();
            credential.Id.Value.ShouldNotBe(Guid.Empty);
        }
    }

    #endregion
}