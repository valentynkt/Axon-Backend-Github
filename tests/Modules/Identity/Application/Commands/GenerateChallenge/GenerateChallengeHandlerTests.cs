using Axon.Modules.Identity.Application.Commands.GenerateChallenge;
using Axon.Modules.Identity.Application.Common;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Application.Tests.Commands.GenerateChallenge;

/// <summary>
/// Tests for GenerateChallengeHandler command handler.
/// Sprint 5 - Command handler tests following Sprint 2-4 patterns.
///
/// TESTING PHILOSOPHY:
/// - Focus on address normalization + orchestrator coordination
/// - Test chain ID extraction logic
/// - Verify correct result mapping from AuthenticationChallenge to GenerateChallengeResult
/// - Minimal mocking (orchestrator + address normalizer)
/// </summary>
[TestFixture]
public class GenerateChallengeHandlerTests
{
    private IAuthenticationOrchestrator _orchestrator = null!;
    private IAddressNormalizationService _addressNormalizer = null!;
    private ILogger<GenerateChallengeHandler> _logger = null!;
    private GenerateChallengeHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _orchestrator = Substitute.For<IAuthenticationOrchestrator>();
        _addressNormalizer = Substitute.For<IAddressNormalizationService>();
        _logger = Substitute.For<ILogger<GenerateChallengeHandler>>();

        _handler = new GenerateChallengeHandler(_orchestrator, _addressNormalizer, _logger);
    }

    #region Success Scenarios (3 tests)

    [Test]
    public async Task GenerateChallenge_WithValidAddress_ShouldReturnChallenge()
    {
        // Arrange
        var chainId = "eip155:1";
        var rawAddress = "0xabcdef1234567890abcdef1234567890abcdef12";
        var normalizedAddress = "0xabcdef1234567890abcdef1234567890abcdef12";
        var audience = "https://example.com";

        _addressNormalizer.NormalizeAddress("eip155:1", rawAddress)
            .Returns(Address.Create(normalizedAddress));

        var challenge = new AuthenticationChallenge(
            ChainId: chainId,
            Address: normalizedAddress,
            IssuedAt: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Exp: DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds(),
            Nonce: "test-nonce-123",
            Aud: audience,
            Message: "Sign this message",
            Mac: "mac-value",
            Mkv: "mkv-value");

        _orchestrator.GenerateChallengeAsync(chainId, normalizedAddress, audience, Arg.Any<CancellationToken>())
            .Returns(Result.Success<AuthenticationChallenge, Error>(challenge));

        var command = new GenerateChallengeCommand(chainId, rawAddress, audience);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var challengeResult = result.Value;
        challengeResult.ChainId.ShouldBe(chainId);
        challengeResult.Address.ShouldBe(normalizedAddress);
        challengeResult.Message.ShouldBe("Sign this message");
        challengeResult.Nonce.ShouldBe("test-nonce-123");
        challengeResult.Audience.ShouldBe(audience);
        challengeResult.Mac.ShouldBe("mac-value");
        challengeResult.Mkv.ShouldBe("mkv-value");
    }

    [Test]
    public async Task GenerateChallenge_WithoutAudience_ShouldUseEmptyString()
    {
        // Arrange
        var chainId = "eip155:1";
        var rawAddress = "0x1234567890abcdef1234567890abcdef12345678";
        var normalizedAddress = "0x1234567890abcdef1234567890abcdef12345678";

        _addressNormalizer.NormalizeAddress("eip155:1", rawAddress)
            .Returns(Address.Create(normalizedAddress));

        var challenge = new AuthenticationChallenge(
            ChainId: chainId,
            Address: normalizedAddress,
            IssuedAt: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Exp: DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds(),
            Nonce: "nonce",
            Aud: string.Empty,
            Message: "message",
            Mac: "mac",
            Mkv: "mkv");

        _orchestrator.GenerateChallengeAsync(chainId, normalizedAddress, string.Empty, Arg.Any<CancellationToken>())
            .Returns(Result.Success<AuthenticationChallenge, Error>(challenge));

        var command = new GenerateChallengeCommand(chainId, rawAddress, null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await _orchestrator.Received(1).GenerateChallengeAsync(chainId, normalizedAddress, string.Empty, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GenerateChallenge_MapsAllFieldsCorrectly()
    {
        // Arrange
        var chainId = "solana:mainnet";
        var rawAddress = "DYw8jCTfwHNRJhhmFcbXvVDTqWMEVFBX6ZKUmG5CNSKK";
        var normalizedAddress = "DYw8jCTfwHNRJhhmFcbXvVDTqWMEVFBX6ZKUmG5CNSKK";
        var issuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds();

        _addressNormalizer.NormalizeAddress("solana:mainnet", rawAddress)
            .Returns(Address.Create(normalizedAddress));

        var challenge = new AuthenticationChallenge(
            ChainId: chainId,
            Address: normalizedAddress,
            IssuedAt: issuedAt,
            Exp: expiresAt,
            Nonce: "unique-nonce",
            Aud: "test-audience",
            Message: "Please sign this message",
            Mac: "mac-hash",
            Mkv: "mkv-version");

        _orchestrator.GenerateChallengeAsync(chainId, normalizedAddress, "test-audience", Arg.Any<CancellationToken>())
            .Returns(Result.Success<AuthenticationChallenge, Error>(challenge));

        var command = new GenerateChallengeCommand(chainId, rawAddress, "test-audience");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var challengeResult = result.Value;
        challengeResult.IssuedAt.ShouldBe(issuedAt);
        challengeResult.ExpiresAt.ShouldBe(expiresAt);
        challengeResult.Nonce.ShouldBe("unique-nonce");
        challengeResult.Mac.ShouldBe("mac-hash");
        challengeResult.Mkv.ShouldBe("mkv-version");
    }

    #endregion

    #region Address Normalization Errors (2 tests)

    [Test]
    public async Task GenerateChallenge_WithInvalidAddress_ShouldReturnNormalizationError()
    {
        // Arrange
        var chainId = "eip155:1";
        var invalidAddress = "not-a-valid-address";
        var error = Error.Validation("Invalid address format", "ADDRESS.INVALID_FORMAT");

        _addressNormalizer.NormalizeAddress("eip155:1", invalidAddress)
            .Returns(Result.Failure<Address, Error>(error));

        var command = new GenerateChallengeCommand(chainId, invalidAddress);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Code.ShouldBe("ADDRESS.INVALID_FORMAT");
    }

    [Test]
    public async Task GenerateChallenge_WithNormalizationFailure_ShouldNotCallOrchestrator()
    {
        // Arrange
        var chainId = "eip155:1";
        var invalidAddress = "bad-address";
        var error = Error.Validation("Cannot normalize", "ADDRESS.ERROR");

        _addressNormalizer.NormalizeAddress("eip155:1", invalidAddress)
            .Returns(Result.Failure<Address, Error>(error));

        var command = new GenerateChallengeCommand(chainId, invalidAddress);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _orchestrator.DidNotReceive().GenerateChallengeAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Chain Extraction Logic (2 tests)

    [Test]
    public async Task GenerateChallenge_ExtractsBaseChain_FromEip155ChainId()
    {
        // Arrange
        var chainId = "eip155:137"; // Polygon
        var rawAddress = "0xfedcba0987654321fedcba0987654321fedcba09";
        var normalizedAddress = "0xfedcba0987654321fedcba0987654321fedcba09";

        // Verify that FULL chain ID (with network ID) is used for normalization
        _addressNormalizer.NormalizeAddress("eip155:137", rawAddress)
            .Returns(Address.Create(normalizedAddress));

        var challenge = new AuthenticationChallenge(
            ChainId: chainId,
            Address: normalizedAddress,
            IssuedAt: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Exp: DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds(),
            Nonce: "nonce",
            Aud: string.Empty,
            Message: "message",
            Mac: "mac",
            Mkv: "mkv");

        _orchestrator.GenerateChallengeAsync(chainId, normalizedAddress, string.Empty, Arg.Any<CancellationToken>())
            .Returns(Result.Success<AuthenticationChallenge, Error>(challenge));

        var command = new GenerateChallengeCommand(chainId, rawAddress);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert - should pass FULL chain ID to normalization (no extraction)
        _addressNormalizer.Received(1).NormalizeAddress("eip155:137", rawAddress);
    }

    [Test]
    public async Task GenerateChallenge_ExtractsBaseChain_FromSolanaChainId()
    {
        // Arrange
        var chainId = "solana:mainnet";
        var rawAddress = "7Np41oeYqPefeNQEHSv1UDhYrehxin3NStELsSKCT4K2";
        var normalizedAddress = "7Np41oeYqPefeNQEHSv1UDhYrehxin3NStELsSKCT4K2";

        _addressNormalizer.NormalizeAddress("solana:mainnet", rawAddress)
            .Returns(Address.Create(normalizedAddress));

        var challenge = new AuthenticationChallenge(
            ChainId: chainId,
            Address: normalizedAddress,
            IssuedAt: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Exp: DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds(),
            Nonce: "nonce",
            Aud: string.Empty,
            Message: "message",
            Mac: "mac",
            Mkv: "mkv");

        _orchestrator.GenerateChallengeAsync(chainId, normalizedAddress, string.Empty, Arg.Any<CancellationToken>())
            .Returns(Result.Success<AuthenticationChallenge, Error>(challenge));

        var command = new GenerateChallengeCommand(chainId, rawAddress);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert - should pass FULL chain ID to normalization (no extraction)
        _addressNormalizer.Received(1).NormalizeAddress("solana:mainnet", rawAddress);
    }

    #endregion

    #region Orchestrator Failures (1 test)

    [Test]
    public async Task GenerateChallenge_WithOrchestratorFailure_ShouldReturnError()
    {
        // Arrange
        var chainId = "eip155:1";
        var rawAddress = "0xdeadbeefcafebabe0123456789abcdefdeadbeef";
        var normalizedAddress = "0xdeadbeefcafebabe0123456789abcdefdeadbeef";
        var error = Error.Internal("Challenge service unavailable", "CHALLENGE.SERVICE_ERROR");

        _addressNormalizer.NormalizeAddress("eip155:1", rawAddress)
            .Returns(Address.Create(normalizedAddress));

        _orchestrator.GenerateChallengeAsync(chainId, normalizedAddress, string.Empty, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<AuthenticationChallenge, Error>(error));

        var command = new GenerateChallengeCommand(chainId, rawAddress);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        result.Error.Code.ShouldBe("CHALLENGE.SERVICE_ERROR");
    }

    #endregion
}