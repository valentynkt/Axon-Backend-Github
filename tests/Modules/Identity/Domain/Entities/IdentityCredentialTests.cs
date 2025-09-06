using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Domain.Tests.TestData;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Domain.Tests.Entities;

[TestFixture]
public class IdentityCredentialTests
{
    private readonly AxonId _testAxonId = AxonId.New();
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    [TestFixture]
    public class Create : IdentityCredentialTests
    {
        [Test]
        public void WithValidData_ShouldSucceed()
        {
            // Arrange
            var providerType = Builders.DynamicProvider;
            var issuer = "https://test-issuer.com";
            var subject = "user123";
            var environmentId = "prod";
            var verifiedAt = DateTimeOffset.UtcNow;
            var emailHash = EmailHash.From("test@example.com");
            var proofType = Builders.SignatureProof;

            // Act
            var result = IdentityCredential.Create(
                _testAxonId, providerType, issuer, subject, environmentId, verifiedAt,
                proofType, emailHash, "session-key", "device-123", "user-agent", "ip-hash");

            // Should
            result.IsSuccess.ShouldBeTrue();
            var credential = result.Value;
            credential.AxonId.ShouldBe(_testAxonId);
            credential.ProviderType.ShouldBe(providerType);
            credential.Issuer.ShouldBe(issuer);
            credential.Subject.ShouldBe(subject);
            credential.EnvironmentId.ShouldBe(environmentId);
            credential.VerifiedAt.ShouldBe(verifiedAt);
            credential.LastSeenAt.ShouldBe(verifiedAt);
            credential.GetEmailHash().ShouldBe(emailHash);
            credential.GetVerificationMethod().ShouldBe(proofType);
            credential.GetSessionPublicKey().ShouldBe("session-key");
        }

        [Test]
        public void WithMinimalData_ShouldSucceed()
        {
            // Arrange
            var providerType = Builders.OidcProvider;
            var issuer = "https://minimal-issuer.com";
            var subject = "minimal-subject";
            var verifiedAt = DateTimeOffset.UtcNow;

            // Act
            var result = IdentityCredential.Create(
                _testAxonId, providerType, issuer, subject, null, verifiedAt);

            // Should
            result.IsSuccess.ShouldBeTrue();
            var credential = result.Value;
            credential.ProviderType.ShouldBe(providerType);
            credential.Issuer.ShouldBe(issuer);
            credential.Subject.ShouldBe(subject);
            credential.EnvironmentId.ShouldBeNull();
            credential.GetEmailHash().ShouldBeNull();
            credential.GetVerificationMethod().Value.ShouldBe("unknown");
        }

        [Test]
        public void WithEmptyIssuer_ShouldFail()
        {
            // Act
            var result = IdentityCredential.Create(
                _testAxonId, Builders.DynamicProvider, "", "subject", null, DateTimeOffset.UtcNow);

            // Should
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.Should().Contain("ISSUER.REQUIRED");
        }

        [Test]
        public void WithEmptySubject_ShouldFail()
        {
            // Act
            var result = IdentityCredential.Create(
                _testAxonId, Builders.DynamicProvider, "issuer", "", null, DateTimeOffset.UtcNow);

            // Should
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.Should().Contain("SUBJECT.REQUIRED");
        }

        [Test]
        public void WithTooLongIssuer_ShouldFail()
        {
            // Arrange
            var longIssuer = new string('x', 201); // Exceeds 200 char limit

            // Act
            var result = IdentityCredential.Create(
                _testAxonId, Builders.DynamicProvider, longIssuer, "subject", null, DateTimeOffset.UtcNow);

            // Should
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.Should().Contain("ISSUER.TOO_LONG");
        }

        [Test]
        public void WithTooLongSubject_ShouldFail()
        {
            // Arrange
            var longSubject = new string('x', 201); // Exceeds 200 char limit

            // Act
            var result = IdentityCredential.Create(
                _testAxonId, Builders.DynamicProvider, "issuer", longSubject, null, DateTimeOffset.UtcNow);

            // Should
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.Should().Contain("SUBJECT.TOO_LONG");
        }

        [Test]
        public void WithTooLongEnvironmentId_ShouldFail()
        {
            // Arrange
            var longEnvironmentId = new string('x', 51); // Exceeds 50 char limit

            // Act
            var result = IdentityCredential.Create(
                _testAxonId, Builders.DynamicProvider, "issuer", "subject", longEnvironmentId, DateTimeOffset.UtcNow);

            // Should
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.Should().Contain("ENVIRONMENT_ID.TOO_LONG");
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

            // Should
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

            // Should
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

            // Should
            credential.LastSeenAt.ShouldBe(originalLastSeen);
        }
    }

    [TestFixture]
    public class UpdateVerifiedAt : IdentityCredentialTests
    {
        [Test]
        public void WithLaterTimestamp_ShouldUpdateBothVerifiedAndLastSeen()
        {
            // Arrange
            var credential = CreateTestCredential();
            var originalVerified = credential.VerifiedAt;
            var newVerifiedAt = originalVerified.AddHours(2);

            // Act
            credential.UpdateVerifiedAt(newVerifiedAt);

            // Should
            credential.VerifiedAt.ShouldBe(newVerifiedAt);
            credential.LastSeenAt.ShouldBe(newVerifiedAt);
        }

        [Test]
        public void WithEarlierTimestamp_ShouldNotUpdate()
        {
            // Arrange
            var credential = CreateTestCredential();
            var originalVerified = credential.VerifiedAt;
            var originalLastSeen = credential.LastSeenAt;
            var earlierTime = originalVerified.AddHours(-1);

            // Act
            credential.UpdateVerifiedAt(earlierTime);

            // Should
            credential.VerifiedAt.ShouldBe(originalVerified);
            credential.LastSeenAt.ShouldBe(originalLastSeen);
        }
    }

    [TestFixture]
    public class UpdateContext : IdentityCredentialTests
    {
        [Test]
        public void WithValidData_ShouldSucceed()
        {
            // Arrange
            var credential = CreateTestCredential();
            var newProofType = ProofType.From("new-proof");
            var newEmailHash = EmailHash.From("new@example.com");

            // Act
            var result = credential.UpdateContext(
                verificationMethod: newProofType,
                emailHash: newEmailHash,
                sessionPublicKey: "new-session-key",
                deviceId: "new-device",
                userAgent: "new-agent",
                ipHash: "new-ip-hash");

            // Should
            result.IsSuccess.ShouldBeTrue();
            credential.GetVerificationMethod().ShouldBe(newProofType);
            credential.GetEmailHash().ShouldBe(newEmailHash);
            credential.GetSessionPublicKey().ShouldBe("new-session-key");
        }
    }

    [TestFixture]
    public class TouchSession : IdentityCredentialTests
    {
        [Test]
        public void WithValidData_ShouldUpdateContextAndLastSeen()
        {
            // Arrange
            var credential = CreateTestCredential();
            var newLastSeen = DateTimeOffset.UtcNow.AddMinutes(30);
            var originalLastSeen = credential.LastSeenAt;

            // Act
            var result = credential.TouchSession(
                deviceId: "new-device",
                userAgent: "new-agent",
                ipHash: "new-ip",
                lastSeenAt: newLastSeen);

            // Should
            result.IsSuccess.ShouldBeTrue();
            credential.LastSeenAt.ShouldBe(newLastSeen);
            credential.LastSeenAt.Should().BeAfter(originalLastSeen);
        }

        [Test]
        public void WithoutLastSeenAt_ShouldOnlyUpdateContext()
        {
            // Arrange
            var credential = CreateTestCredential();
            var originalLastSeen = credential.LastSeenAt;

            // Act
            var result = credential.TouchSession(
                deviceId: "updated-device",
                userAgent: "updated-agent");

            // Should
            result.IsSuccess.ShouldBeTrue();
            credential.LastSeenAt.ShouldBe(originalLastSeen); // Should remain unchanged
        }
    }

    [TestFixture]
    public class GetUniqueKey : IdentityCredentialTests
    {
        [Test]
        public void ShouldReturnCorrectFormat()
        {
            // Arrange
            var credential = CreateTestCredential();
            var expectedKey = $"{credential.ProviderType.Value}:{credential.Issuer}:{credential.Subject}";

            // Act
            var uniqueKey = credential.GetUniqueKey();

            // Should
            uniqueKey.ShouldBe(expectedKey);
        }
    }

    [TestFixture]
    public class Matches : IdentityCredentialTests
    {
        [Test]
        public void WithSameProviderIssuerSubject_ShouldReturnTrue()
        {
            // Arrange
            var credential = CreateTestCredential();

            // Act
            var matches = credential.Matches(
                credential.ProviderType, credential.Issuer, credential.Subject);

            // Should
            matches.ShouldBeTrue();
        }

        [Test]
        public void WithDifferentProvider_ShouldReturnFalse()
        {
            // Arrange
            var credential = CreateTestCredential();

            // Act
            var matches = credential.Matches(
                Builders.SiwsProvider, credential.Issuer, credential.Subject);

            // Should
            matches.ShouldBeFalse();
        }

        [Test]
        public void WithDifferentIssuer_ShouldReturnFalse()
        {
            // Arrange
            var credential = CreateTestCredential();

            // Act
            var matches = credential.Matches(
                credential.ProviderType, "different-issuer", credential.Subject);

            // Should
            matches.ShouldBeFalse();
        }

        [Test]
        public void WithDifferentSubject_ShouldReturnFalse()
        {
            // Arrange
            var credential = CreateTestCredential();

            // Act
            var matches = credential.Matches(
                credential.ProviderType, credential.Issuer, "different-subject");

            // Should
            matches.ShouldBeFalse();
        }
    }

    [TestFixture]
    public class BelongsTo : IdentityCredentialTests
    {
        [Test]
        public void WithSamePrincipalId_ShouldReturnTrue()
        {
            // Arrange
            var credential = CreateTestCredential();

            // Act
            var belongsTo = credential.BelongsTo(_testAxonId);

            // Should
            belongsTo.ShouldBeTrue();
        }

        [Test]
        public void WithDifferentPrincipalId_ShouldReturnFalse()
        {
            // Arrange
            var credential = CreateTestCredential();
            var differentId = AxonId.New();

            // Act
            var belongsTo = credential.BelongsTo(differentId);

            // Should
            belongsTo.ShouldBeFalse();
        }
    }

    private IdentityCredential CreateTestCredential()
    {
        var result = IdentityCredential.Create(
            _testAxonId,
            Builders.DynamicProvider,
            "https://test-issuer.com",
            "test-subject",
            "test-env",
            DateTimeOffset.UtcNow,
            Builders.SignatureProof,
            EmailHash.From("test@example.com"),
            "test-session-key",
            "test-device",
            "test-agent",
            "test-ip");

        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }
}