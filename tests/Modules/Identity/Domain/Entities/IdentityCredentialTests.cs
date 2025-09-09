using Axon.Modules.Identity.Domain.Entities;
using BuildingBlocks.Primitives.Ids;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Domain.Tests.Entities;

[TestFixture]
public class IdentityCredentialTests
{
    private readonly AxonId _testPrincipalId = AxonId.New();

    [TestFixture]
    public class Create : IdentityCredentialTests
    {
        [Test]
        public void WithValidData_ShouldSucceed()
        {
            // Arrange
            var provider = "dynamic";
            var issuer = "https://test-issuer.com";
            var subject = "user123";
            var timestamp = DateTime.UtcNow;

            // Act
            var credential = IdentityCredential.Create(
                _testPrincipalId, provider, issuer, subject, timestamp);

            // Assert
            credential.ShouldNotBeNull();
            credential.PrincipalId.ShouldBe(_testPrincipalId);
            credential.Provider.ShouldBe(provider);
            credential.Issuer.ShouldBe(issuer);
            credential.Subject.ShouldBe(subject);
            credential.LastSeenAt.ShouldBe(timestamp);
        }

        [Test]
        public void WithMinimalData_ShouldSucceed()
        {
            // Arrange
            var provider = "oidc";
            var issuer = "https://minimal-issuer.com";
            var subject = "minimal-subject";

            // Act (timestamp defaults to now)
            var credential = IdentityCredential.Create(
                _testPrincipalId, provider, issuer, subject);

            // Assert
            credential.ShouldNotBeNull();
            credential.Provider.ShouldBe(provider);
            credential.Issuer.ShouldBe(issuer);
            credential.Subject.ShouldBe(subject);
            credential.LastSeenAt.ShouldBeGreaterThan(DateTime.UtcNow.AddMinutes(-1));
        }
    }

    [TestFixture]
    public class UpdateLastSeen : IdentityCredentialTests
    {
        [Test]
        public void WithLaterTimestamp_ShouldUpdate()
        {
            // Arrange
            var credential = CreateTestCredential();
            var originalLastSeen = credential.LastSeenAt;
            var newLastSeen = originalLastSeen.AddHours(1);

            // Act
            credential.UpdateLastSeen(newLastSeen);

            // Assert
            credential.LastSeenAt.ShouldBe(newLastSeen);
        }

        [Test]
        public void WithEarlierTimestamp_ShouldNotUpdate()
        {
            // Arrange
            var credential = CreateTestCredential();
            var originalLastSeen = credential.LastSeenAt;
            var earlierTime = originalLastSeen.AddHours(-1);

            // Act
            credential.UpdateLastSeen(earlierTime);

            // Assert
            credential.LastSeenAt.ShouldBe(originalLastSeen); // Should remain unchanged
        }

        [Test]
        public void WithSameTimestamp_ShouldNotUpdate()
        {
            // Arrange
            var credential = CreateTestCredential();
            var originalLastSeen = credential.LastSeenAt;

            // Act
            credential.UpdateLastSeen(originalLastSeen);

            // Assert
            credential.LastSeenAt.ShouldBe(originalLastSeen);
        }
    }

    private IdentityCredential CreateTestCredential()
    {
        return IdentityCredential.Create(
            _testPrincipalId,
            "dynamic",
            "https://test-issuer.com",
            "test-subject",
            DateTime.UtcNow);
    }
}