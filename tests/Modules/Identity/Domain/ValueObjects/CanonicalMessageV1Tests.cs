using Axon.Modules.Identity.Domain.ValueObjects;
using NUnit.Framework;
using Shouldly;
using System.Text.Json;

namespace Axon.Modules.Identity.Domain.Tests.ValueObjects;

[TestFixture]
public class CanonicalMessageV1Tests
{
    [Test]
    public void Create_Should_Enforce_5_Minute_TTL()
    {
        // Arrange
        var networkEnv = NetworkEnvironment.Create("mainnet").Value;
        var chainId = ChainId.Create("solana:mainnet").Value;
        var address = Address.Create("HN7cABqLq46Es1jh92dQQisAq662SmxELLLsHHe4YWrH").Value;
        var audience = "axon-api";
        var nonce = "test-nonce-123";
        var mac = "test-mac-456";

        // Act
        var result = CanonicalMessageV1.Create(
            networkEnv,
            chainId,
            address,
            audience,
            nonce,
            mac);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var message = result.Value;

        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(message.IssuedAt);
        var expiration = DateTimeOffset.FromUnixTimeSeconds(message.Exp);
        var ttlSeconds = (expiration - issuedAt).TotalSeconds;

        ttlSeconds.ShouldBe(CanonicalMessageV1.MaxTtlSeconds);
        ttlSeconds.ShouldBe(300); // 5 minutes
    }

    [Test]
    public void ValidateTiming_Should_Reject_TTL_Over_5_Minutes()
    {
        // Arrange - create message with manipulated timestamps
        var json = @"{
            ""network_environment"": ""mainnet"",
            ""chain_id"": ""solana:mainnet"",
            ""address"": ""HN7cABqLq46Es1jh92dQQisAq662SmxELLLsHHe4YWrH"",
            ""issued_at"": 1000000000,
            ""exp"": 1000000400,
            ""nbf"": 1000000000,
            ""nonce"": ""test-nonce"",
            ""aud"": ""axon-api"",
            ""version"": ""v1"",
            ""mac"": ""test-mac""
        }";

        var messageResult = CanonicalMessageV1.FromJson(json);
        messageResult.IsSuccess.ShouldBeTrue();

        // Act
        var now = DateTimeOffset.FromUnixTimeSeconds(1000000100);
        var validationResult = messageResult.Value.ValidateTiming(now);

        // Assert - Should fail because TTL is 400 seconds (> 300)
        validationResult.IsFailure.ShouldBeTrue();
        validationResult.Error.Message.ShouldContain("TTL exceeds 5-minute maximum");
        validationResult.Error.Code.ShouldBe("CANONICAL_V1.TTL_EXCEEDED");
    }

    [Test]
    public void ValidateTiming_Should_Accept_Exactly_5_Minute_TTL()
    {
        // Arrange
        var json = @"{
            ""network_environment"": ""mainnet"",
            ""chain_id"": ""solana:mainnet"",
            ""address"": ""HN7cABqLq46Es1jh92dQQisAq662SmxELLLsHHe4YWrH"",
            ""issued_at"": 1000000000,
            ""exp"": 1000000300,
            ""nbf"": 1000000000,
            ""nonce"": ""test-nonce"",
            ""aud"": ""axon-api"",
            ""version"": ""v1"",
            ""mac"": ""test-mac""
        }";

        var messageResult = CanonicalMessageV1.FromJson(json);
        messageResult.IsSuccess.ShouldBeTrue();

        // Act
        var now = DateTimeOffset.FromUnixTimeSeconds(1000000100);
        var validationResult = messageResult.Value.ValidateTiming(now);

        // Assert - Should pass because TTL is exactly 300 seconds
        validationResult.IsSuccess.ShouldBeTrue();
    }

    [Test]
    public void ValidateTiming_Should_Apply_Clock_Skew_Tolerance()
    {
        // Arrange
        var networkEnv = NetworkEnvironment.Create("mainnet").Value;
        var chainId = ChainId.Create("solana:mainnet").Value;
        var address = Address.Create("HN7cABqLq46Es1jh92dQQisAq662SmxELLLsHHe4YWrH").Value;
        var audience = "axon-api";
        var nonce = "test-nonce";
        var mac = "test-mac";

        var issuedAt = DateTimeOffset.UtcNow;
        var messageResult = CanonicalMessageV1.Create(
            networkEnv,
            chainId,
            address,
            audience,
            nonce,
            mac,
            issuedAt);

        messageResult.IsSuccess.ShouldBeTrue();
        var message = messageResult.Value;

        // Test before NBF with skew
        var beforeNbf = issuedAt.AddSeconds(-30); // Within 60-second skew
        var beforeResult = message.ValidateTiming(beforeNbf);
        beforeResult.IsSuccess.ShouldBeTrue();

        // Test after expiration with skew
        var afterExp = issuedAt.AddSeconds(330); // 30 seconds after expiration, within skew
        var afterResult = message.ValidateTiming(afterExp);
        afterResult.IsSuccess.ShouldBeTrue();

        // Test too far before NBF
        var tooEarly = issuedAt.AddSeconds(-90); // Outside 60-second skew
        var tooEarlyResult = message.ValidateTiming(tooEarly);
        tooEarlyResult.IsFailure.ShouldBeTrue();
        tooEarlyResult.Error.Code.ShouldBe("CANONICAL_V1.NOT_YET_VALID");

        // Test too far after expiration
        var tooLate = issuedAt.AddSeconds(390); // 90 seconds after expiration, outside skew
        var tooLateResult = message.ValidateTiming(tooLate);
        tooLateResult.IsFailure.ShouldBeTrue();
        tooLateResult.Error.Code.ShouldBe("CANONICAL_V1.EXPIRED");
    }

    [Test]
    public void ToCanonicalJson_Should_Produce_Deterministic_Output()
    {
        // Arrange
        var networkEnv = NetworkEnvironment.Create("mainnet").Value;
        var chainId = ChainId.Create("solana:mainnet").Value;
        var address = Address.Create("HN7cABqLq46Es1jh92dQQisAq662SmxELLLsHHe4YWrH").Value;
        var audience = "axon-api";
        var nonce = "test-nonce";
        var mac = "test-mac";

        var message1Result = CanonicalMessageV1.Create(
            networkEnv,
            chainId,
            address,
            audience,
            nonce,
            mac,
            DateTimeOffset.FromUnixTimeSeconds(1000000000));

        var message2Result = CanonicalMessageV1.Create(
            networkEnv,
            chainId,
            address,
            audience,
            nonce,
            mac,
            DateTimeOffset.FromUnixTimeSeconds(1000000000));

        // Act
        var json1 = message1Result.Value.ToCanonicalJson();
        var json2 = message2Result.Value.ToCanonicalJson();

        // Assert
        json1.ShouldBe(json2);

        // Verify field order
        var doc = JsonDocument.Parse(json1);
        var properties = doc.RootElement.EnumerateObject().Select(p => p.Name).ToList();

        properties[0].ShouldBe("network_environment");
        properties[1].ShouldBe("chain_id");
        properties[2].ShouldBe("address");
        properties[3].ShouldBe("issued_at");
        properties[4].ShouldBe("exp");
        properties[5].ShouldBe("nbf");
        properties[6].ShouldBe("nonce");
        properties[7].ShouldBe("aud");
        properties[8].ShouldBe("version");
        properties[9].ShouldBe("mac");
    }

    [Test]
    public void GetCanonicalBytes_Should_Return_UTF8_Bytes()
    {
        // Arrange
        var networkEnv = NetworkEnvironment.Create("mainnet").Value;
        var chainId = ChainId.Create("solana:mainnet").Value;
        var address = Address.Create("HN7cABqLq46Es1jh92dQQisAq662SmxELLLsHHe4YWrH").Value;
        var audience = "axon-api";
        var nonce = "test-nonce";
        var mac = "test-mac";

        var messageResult = CanonicalMessageV1.Create(
            networkEnv,
            chainId,
            address,
            audience,
            nonce,
            mac,
            DateTimeOffset.FromUnixTimeSeconds(1000000000));

        // Act
        var bytes = messageResult.Value.GetCanonicalBytes();
        var reconstructedJson = System.Text.Encoding.UTF8.GetString(bytes);

        // Assert
        bytes.ShouldNotBeNull();
        bytes.Length.ShouldBeGreaterThan(0);
        reconstructedJson.ShouldBe(messageResult.Value.ToCanonicalJson());
    }

    [Test]
    public void FromJson_Should_Validate_Version()
    {
        // Arrange - JSON with wrong version
        var json = @"{
            ""network_environment"": ""mainnet"",
            ""chain_id"": ""solana:mainnet"",
            ""address"": ""HN7cABqLq46Es1jh92dQQisAq662SmxELLLsHHe4YWrH"",
            ""issued_at"": 1000000000,
            ""exp"": 1000000300,
            ""nbf"": 1000000000,
            ""nonce"": ""test-nonce"",
            ""aud"": ""axon-api"",
            ""version"": ""v2"",
            ""mac"": ""test-mac""
        }";

        // Act
        var result = CanonicalMessageV1.FromJson(json);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("CANONICAL_V1.VERSION_UNSUPPORTED");
    }

    [Test]
    public void Create_Should_Include_Audience_In_Message()
    {
        // Arrange
        var networkEnv = NetworkEnvironment.Create("mainnet").Value;
        var chainId = ChainId.Create("solana:mainnet").Value;
        var address = Address.Create("HN7cABqLq46Es1jh92dQQisAq662SmxELLLsHHe4YWrH").Value;
        var audience = "partner-app-123";
        var nonce = "test-nonce";
        var mac = "test-mac";

        // Act
        var result = CanonicalMessageV1.Create(
            networkEnv,
            chainId,
            address,
            audience,
            nonce,
            mac);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Aud.ShouldBe(audience);

        var json = result.Value.ToCanonicalJson();
        json.ShouldContain($"\"aud\":\"{audience}\"");
    }
}