using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;
using FluentAssertions;

namespace Axon.Modules.Identity.Domain.Entities.Tests;

[TestFixture]
public class IdentityCredentialTests
{
    private AxonUserId _principalId;
    private string _provider = "dynamic";
    private string _issuer = "https://app.dynamic.xyz/mainnet";
    private string _subject = "user123";

    [SetUp]
    public void SetUp()
    {
        _principalId = AxonUserId.New();
    }

    [Test]
    public void UpdateLastSeen_NewerTimestamp_UpdatesLastSeenAt()
    {
        // Arrange
        var initialTimestamp = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var credential = IdentityCredential.Create(_principalId, _provider, _issuer, _subject, initialTimestamp);

        var newerTimestamp = new DateTime(2025, 1, 1, 11, 0, 0, DateTimeKind.Utc);

        // Act
        credential.UpdateLastSeen(newerTimestamp);

        // Assert
        credential.LastSeenAt.Should().Be(newerTimestamp);
    }

    [Test]
    public void UpdateLastSeen_OlderTimestamp_DoesNotUpdateLastSeenAt()
    {
        // Arrange
        var initialTimestamp = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var credential = IdentityCredential.Create(_principalId, _provider, _issuer, _subject, initialTimestamp);

        var olderTimestamp = new DateTime(2025, 1, 1, 9, 0, 0, DateTimeKind.Utc);

        // Act
        credential.UpdateLastSeen(olderTimestamp);

        // Assert
        credential.LastSeenAt.Should().Be(initialTimestamp); // Should remain unchanged
    }

    [Test]
    public void UpdateLastSeen_SameTimestamp_DoesNotUpdateLastSeenAt()
    {
        // Arrange
        var initialTimestamp = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var credential = IdentityCredential.Create(_principalId, _provider, _issuer, _subject, initialTimestamp);

        // Act
        credential.UpdateLastSeen(initialTimestamp);

        // Assert
        credential.LastSeenAt.Should().Be(initialTimestamp);
    }

    [Test]
    public void UpdateLastSeen_MultipleUpdates_OnlyKeepsLatest()
    {
        // Arrange
        var initialTimestamp = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var credential = IdentityCredential.Create(_principalId, _provider, _issuer, _subject, initialTimestamp);

        var timestamp1 = new DateTime(2025, 1, 1, 11, 0, 0, DateTimeKind.Utc);
        var timestamp2 = new DateTime(2025, 1, 1, 9, 0, 0, DateTimeKind.Utc); // Older
        var timestamp3 = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var timestamp4 = new DateTime(2025, 1, 1, 11, 30, 0, DateTimeKind.Utc); // Between timestamp1 and timestamp3

        // Act
        credential.UpdateLastSeen(timestamp1);
        credential.UpdateLastSeen(timestamp2); // Should be ignored (older)
        credential.UpdateLastSeen(timestamp3);
        credential.UpdateLastSeen(timestamp4); // Should be ignored (older than current)

        // Assert
        credential.LastSeenAt.Should().Be(timestamp3); // Should be the latest
    }

    [Test]
    public void UpdateLastSeen_MicrosecondPrecision_HandlesCorrectly()
    {
        // Arrange
        var initialTimestamp = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc).AddMilliseconds(500);
        var credential = IdentityCredential.Create(_principalId, _provider, _issuer, _subject, initialTimestamp);

        var slightlyNewerTimestamp = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc).AddMilliseconds(500.001);

        // Act
        credential.UpdateLastSeen(slightlyNewerTimestamp);

        // Assert
        credential.LastSeenAt.Should().Be(slightlyNewerTimestamp);
    }

    [Test]
    public void Create_WithoutTimestamp_UsesCurrentUtcTime()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;

        // Act
        var credential = IdentityCredential.Create(_principalId, _provider, _issuer, _subject);
        var afterCreation = DateTime.UtcNow;

        // Assert
        credential.LastSeenAt.Should().BeOnOrAfter(beforeCreation);
        credential.LastSeenAt.Should().BeOnOrBefore(afterCreation);
        credential.LastSeenAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Test]
    public void Create_WithSpecificTimestamp_UsesProvidedTimestamp()
    {
        // Arrange
        var specificTimestamp = new DateTime(2024, 12, 25, 15, 30, 45, DateTimeKind.Utc);

        // Act
        var credential = IdentityCredential.Create(_principalId, _provider, _issuer, _subject, specificTimestamp);

        // Assert
        credential.LastSeenAt.Should().Be(specificTimestamp);
    }

    [Test]
    public void UpdateLastSeen_RapidSuccessiveUpdates_MaintainsMonotonicity()
    {
        // Arrange
        var credential = IdentityCredential.Create(_principalId, _provider, _issuer, _subject);
        var timestamps = new List<DateTime>();

        // Generate 100 timestamps with some out of order
        var baseTime = DateTime.UtcNow;
        for (int i = 0; i < 100; i++)
        {
            if (i % 10 == 0 && i > 0)
            {
                // Insert an older timestamp every 10th iteration
                timestamps.Add(baseTime.AddSeconds(i - 5));
            }
            else
            {
                timestamps.Add(baseTime.AddSeconds(i));
            }
        }

        // Act
        foreach (var timestamp in timestamps)
        {
            credential.UpdateLastSeen(timestamp);
        }

        // Assert - Should have the maximum timestamp
        var expectedMax = timestamps.Max();
        credential.LastSeenAt.Should().Be(expectedMax);
    }

    [Test]
    public void UpdateLastSeen_ConcurrentScenarioSimulation_MaintainsMonotonicity()
    {
        // This simulates what might happen if multiple threads try to update
        // In practice, the repository would handle concurrency, but the domain logic should be correct

        // Arrange
        var credential = IdentityCredential.Create(_principalId, _provider, _issuer, _subject);
        var random = new Random(42); // Seed for reproducibility
        var updates = new List<DateTime>();
        var baseTime = DateTime.UtcNow;

        // Generate random timestamps as if from concurrent requests
        for (int i = 0; i < 50; i++)
        {
            var offsetSeconds = random.Next(-10, 20); // Some in past, some in future
            updates.Add(baseTime.AddSeconds(offsetSeconds));
        }

        // Act
        foreach (var timestamp in updates)
        {
            credential.UpdateLastSeen(timestamp);
        }

        // Assert
        var expectedMax = updates.Max();
        credential.LastSeenAt.Should().Be(expectedMax);

        // Verify monotonicity was maintained (never went backwards)
        credential.LastSeenAt.Should().BeOnOrAfter(baseTime.AddSeconds(-10));
    }

    [Test]
    public void IdentityCredential_PropertiesAreImmutable_ExceptLastSeenAt()
    {
        // Arrange
        var credential = IdentityCredential.Create(_principalId, _provider, _issuer, _subject);
        var originalPrincipalId = credential.PrincipalId;
        var originalProvider = credential.Provider;
        var originalIssuer = credential.Issuer;
        var originalSubject = credential.Subject;
        var originalLastSeen = credential.LastSeenAt;

        // Act - Update last seen
        var newTimestamp = DateTime.UtcNow.AddHours(1);
        credential.UpdateLastSeen(newTimestamp);

        // Assert - Only LastSeenAt should change
        credential.PrincipalId.Should().Be(originalPrincipalId);
        credential.Provider.Should().Be(originalProvider);
        credential.Issuer.Should().Be(originalIssuer);
        credential.Subject.Should().Be(originalSubject);
        credential.LastSeenAt.Should().NotBe(originalLastSeen);
        credential.LastSeenAt.Should().Be(newTimestamp);
    }
}
