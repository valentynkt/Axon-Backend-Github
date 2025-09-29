using Axon.Modules.Identity.Application.Commands.VerifyWalletSignature;
using Axon.Modules.Identity.Application.Contracts.Providers;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Application.Tests.Commands.VerifyWalletSignature;

/// <summary>
/// Tests for VerifyWalletSignatureHandler command handler.
/// Sprint 5 - Command handler tests following Sprint 2-4 patterns.
///
/// TESTING PHILOSOPHY:
/// - Focus on multi-step validation pipeline (7 steps)
/// - Test each failure point in the validation chain
/// - Verify correct orchestration of 4 service dependencies
/// - Minimal mocking - just the 4 services + logger
/// </summary>
[TestFixture]
public class VerifyWalletSignatureHandlerTests
{
    private IAuthenticationOrchestrator _orchestrator = null!;
    private IWalletSignatureVerifier _signatureVerifier = null!;
    private IPrincipalResolutionService _principalResolver = null!;
    private IAddressNormalizationService _addressNormalizer = null!;
    private ILogger<VerifyWalletSignatureHandler> _logger = null!;
    private VerifyWalletSignatureHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _orchestrator = Substitute.For<IAuthenticationOrchestrator>();
        _signatureVerifier = Substitute.For<IWalletSignatureVerifier>();
        _principalResolver = Substitute.For<IPrincipalResolutionService>();
        _addressNormalizer = Substitute.For<IAddressNormalizationService>();
        _logger = Substitute.For<ILogger<VerifyWalletSignatureHandler>>();

        _handler = new VerifyWalletSignatureHandler(
            _orchestrator,
            _signatureVerifier,
            _principalResolver,
            _addressNormalizer,
            _logger);
    }

    #region Success Scenarios (2 tests)

    [Test]
    public async Task VerifySignature_WithValidSignature_ShouldReturnAuthResponse()
    {
        // Arrange
        var chainId = "eip155:1";
        var address = "0xabcdef1234567890abcdef1234567890abcdef12";
        var audience = "https://app.example.com";
        var signedMessage = $"{{\"aud\":\"{audience}\",\"nonce\":\"test-nonce-123\"}}";
        var signature = "base64-signature-value";
        var mac = "mac-value";
        var mkv = "mkv-value";
        var userId = Guid.NewGuid();
        var accessToken = "jwt-access-token-abc123";

        // Step 2: Challenge validation succeeds
        _orchestrator.ValidateChallenge(signedMessage, chainId, address, audience)
            .Returns(Result.Success<bool, Error>(true));

        // Step 3: Replay protection succeeds
        _orchestrator.CheckAndMarkNonceUsedAsync(signedMessage, mkv, Arg.Any<CancellationToken>())
            .Returns(UnitResult.Success<Error>());

        // Step 4: Signature verification succeeds
        _signatureVerifier.VerifySignature(chainId, address, signedMessage, signature)
            .Returns(Result.Success<bool, Error>(true));

        // Step 5: Address normalization succeeds
        _addressNormalizer.NormalizeAddress(chainId, address)
            .Returns(Address.Create(address));

        // Step 6: Authentication succeeds
        var authResponse = new AuthenticationResponse(
            AccessToken: accessToken,
            UserId: userId,
            ProviderType: "wallet",
            ExpiresAt: DateTime.UtcNow.AddMinutes(30),
            AdditionalData: new Dictionary<string, object> { ["created"] = false });

        _orchestrator.AuthenticateWithWalletAsync(
            Arg.Is<WalletAuthenticationRequest>(r =>
                r.ChainId == chainId &&
                r.Address == address &&
                r.SignedMessage == signedMessage &&
                r.Signature == signature &&
                r.Mac == mac &&
                r.Mkv == mkv),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success<AuthenticationResponse, Error>(authResponse));

        var command = new VerifyWalletSignatureCommand(chainId, address, signedMessage, signature, mac, mkv);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var verifyResult = result.Value;
        verifyResult.AccessToken.ShouldBe(accessToken);
        verifyResult.TokenType.ShouldBe("Bearer");
        verifyResult.AxonUserId.ShouldBe(userId.ToString());
        verifyResult.Created.ShouldBeFalse();
        verifyResult.WalletsLinked.ShouldBe(1);
        verifyResult.Conflicts.ShouldBe(0);
    }

    [Test]
    public async Task VerifySignature_MapsAllResultFields_Correctly()
    {
        // Arrange
        var chainId = "eip155:1";
        var address = "0x1234567890abcdef1234567890abcdef12345678";
        var audience = "https://test.com";
        var signedMessage = $"{{\"aud\":\"{audience}\"}}";
        var signature = "sig";
        var mac = "mac";
        var mkv = "mkv";
        var userId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddMinutes(30);

        // Mock all validation steps to succeed
        _orchestrator.ValidateChallenge(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Result.Success<bool, Error>(true));
        _orchestrator.CheckAndMarkNonceUsedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(UnitResult.Success<Error>());
        _signatureVerifier.VerifySignature(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Result.Success<bool, Error>(true));
        _addressNormalizer.NormalizeAddress(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Address.Create(address));

        var authResponse = new AuthenticationResponse(
            AccessToken: "token-xyz",
            UserId: userId,
            ProviderType: "wallet",
            ExpiresAt: expiresAt,
            AdditionalData: new Dictionary<string, object> { ["created"] = true });

        _orchestrator.AuthenticateWithWalletAsync(Arg.Any<WalletAuthenticationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<AuthenticationResponse, Error>(authResponse));

        var command = new VerifyWalletSignatureCommand(chainId, address, signedMessage, signature, mac, mkv);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var verifyResult = result.Value;
        verifyResult.ExpiresIn.ShouldBeGreaterThan(0);
        verifyResult.ExpiresIn.ShouldBeLessThanOrEqualTo(1800); // ~30 minutes
        verifyResult.Created.ShouldBeTrue(); // Mapped from AdditionalData
    }

    #endregion

    #region Validation Pipeline Failures (5 tests)

    [Test]
    public async Task VerifySignature_WithInvalidChallenge_ShouldReturnValidationError()
    {
        // Arrange
        var chainId = "eip155:1";
        var address = "0xabcdef1234567890abcdef1234567890abcdef12";
        var audience = "https://app.example.com";
        var signedMessage = $"{{\"aud\":\"{audience}\"}}";
        var signature = "sig";
        var error = Error.Validation("Challenge expired", "CHALLENGE.EXPIRED");

        // Step 2: Challenge validation fails
        _orchestrator.ValidateChallenge(signedMessage, chainId, address, audience)
            .Returns(Result.Failure<bool, Error>(error));

        var command = new VerifyWalletSignatureCommand(chainId, address, signedMessage, signature, "mac", "mkv");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Code.ShouldBe("CHALLENGE.EXPIRED");
    }

    [Test]
    public async Task VerifySignature_WithReplayedNonce_ShouldReturnReplayError()
    {
        // Arrange
        var chainId = "eip155:1";
        var address = "0xabcdef1234567890abcdef1234567890abcdef12";
        var audience = "https://app.example.com";
        var signedMessage = $"{{\"aud\":\"{audience}\"}}";
        var signature = "sig";
        var mkv = "mkv";
        var error = Error.Validation("Nonce already used", "NONCE.REPLAY_DETECTED");

        // Step 2: Challenge validation succeeds
        _orchestrator.ValidateChallenge(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Result.Success<bool, Error>(true));

        // Step 3: Replay protection fails
        _orchestrator.CheckAndMarkNonceUsedAsync(signedMessage, mkv, Arg.Any<CancellationToken>())
            .Returns(UnitResult.Failure(error));

        var command = new VerifyWalletSignatureCommand(chainId, address, signedMessage, signature, "mac", mkv);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Code.ShouldBe("NONCE.REPLAY_DETECTED");
    }

    [Test]
    public async Task VerifySignature_WithInvalidSignature_ShouldReturnUnauthorizedError()
    {
        // Arrange
        var chainId = "eip155:1";
        var address = "0xabcdef1234567890abcdef1234567890abcdef12";
        var audience = "https://app.example.com";
        var signedMessage = $"{{\"aud\":\"{audience}\"}}";
        var signature = "invalid-signature";
        var error = Error.Unauthorized("Signature verification failed", "SIGNATURE.INVALID");

        // Steps 2-3: Pass
        _orchestrator.ValidateChallenge(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Result.Success<bool, Error>(true));
        _orchestrator.CheckAndMarkNonceUsedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(UnitResult.Success<Error>());

        // Step 4: Signature verification fails
        _signatureVerifier.VerifySignature(chainId, address, signedMessage, signature)
            .Returns(Result.Failure<bool, Error>(error));

        var command = new VerifyWalletSignatureCommand(chainId, address, signedMessage, signature, "mac", "mkv");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
        result.Error.Code.ShouldBe("SIGNATURE.INVALID");
    }

    [Test]
    public async Task VerifySignature_WithNormalizationFailure_ShouldReturnError()
    {
        // Arrange
        var chainId = "eip155:1";
        var address = "invalid-address";
        var audience = "https://app.example.com";
        var signedMessage = $"{{\"aud\":\"{audience}\"}}";
        var signature = "sig";
        var error = Error.Validation("Invalid address format", "ADDRESS.INVALID");

        // Steps 2-4: Pass
        _orchestrator.ValidateChallenge(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Result.Success<bool, Error>(true));
        _orchestrator.CheckAndMarkNonceUsedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(UnitResult.Success<Error>());
        _signatureVerifier.VerifySignature(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Result.Success<bool, Error>(true));

        // Step 5: Address normalization fails
        _addressNormalizer.NormalizeAddress(chainId, address)
            .Returns(Result.Failure<Address, Error>(error));

        var command = new VerifyWalletSignatureCommand(chainId, address, signedMessage, signature, "mac", "mkv");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Code.ShouldBe("ADDRESS.INVALID");
    }

    [Test]
    public async Task VerifySignature_WithAuthenticationFailure_ShouldReturnError()
    {
        // Arrange
        var chainId = "eip155:1";
        var address = "0xabcdef1234567890abcdef1234567890abcdef12";
        var audience = "https://app.example.com";
        var signedMessage = $"{{\"aud\":\"{audience}\"}}";
        var signature = "sig";
        var error = Error.Internal("Database unavailable", "AUTH.DB_ERROR");

        // Steps 2-5: Pass
        _orchestrator.ValidateChallenge(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Result.Success<bool, Error>(true));
        _orchestrator.CheckAndMarkNonceUsedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(UnitResult.Success<Error>());
        _signatureVerifier.VerifySignature(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Result.Success<bool, Error>(true));
        _addressNormalizer.NormalizeAddress(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Address.Create(address));

        // Step 6: Authentication fails
        _orchestrator.AuthenticateWithWalletAsync(Arg.Any<WalletAuthenticationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<AuthenticationResponse, Error>(error));

        var command = new VerifyWalletSignatureCommand(chainId, address, signedMessage, signature, "mac", "mkv");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        result.Error.Code.ShouldBe("AUTH.DB_ERROR");
    }

    #endregion

    #region Message Parsing (1 test)

    [Test]
    public async Task VerifySignature_ParsesAudienceFromSignedMessage()
    {
        // Arrange
        var chainId = "eip155:1";
        var address = "0xabcdef1234567890abcdef1234567890abcdef12";
        var expectedAudience = "https://custom-app.example.com";
        var signedMessage = $"{{\"aud\":\"{expectedAudience}\",\"nonce\":\"abc\"}}";
        var signature = "sig";
        var userId = Guid.NewGuid();

        // Mock all steps to pass
        _orchestrator.ValidateChallenge(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Result.Success<bool, Error>(true));
        _orchestrator.CheckAndMarkNonceUsedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(UnitResult.Success<Error>());
        _signatureVerifier.VerifySignature(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Result.Success<bool, Error>(true));
        _addressNormalizer.NormalizeAddress(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Address.Create(address));

        var authResponse = new AuthenticationResponse(
            AccessToken: "token",
            UserId: userId,
            ProviderType: "wallet",
            ExpiresAt: DateTime.UtcNow.AddMinutes(30),
            AdditionalData: null);

        _orchestrator.AuthenticateWithWalletAsync(Arg.Any<WalletAuthenticationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<AuthenticationResponse, Error>(authResponse));

        var command = new VerifyWalletSignatureCommand(chainId, address, signedMessage, signature, "mac", "mkv");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert - Verify orchestrator was called with parsed audience
        _orchestrator.Received(1).ValidateChallenge(signedMessage, chainId, address, expectedAudience);
    }

    #endregion

    #region Result Mapping Edge Cases (2 tests)

    [Test]
    public async Task VerifySignature_WithCreatedTrue_ShouldSetCreatedFlag()
    {
        // Arrange
        var chainId = "eip155:1";
        var address = "0xabcdef1234567890abcdef1234567890abcdef12";
        var audience = "https://app.example.com";
        var signedMessage = $"{{\"aud\":\"{audience}\"}}";
        var signature = "sig";
        var userId = Guid.NewGuid();

        // Mock all steps to pass
        _orchestrator.ValidateChallenge(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Result.Success<bool, Error>(true));
        _orchestrator.CheckAndMarkNonceUsedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(UnitResult.Success<Error>());
        _signatureVerifier.VerifySignature(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Result.Success<bool, Error>(true));
        _addressNormalizer.NormalizeAddress(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Address.Create(address));

        // Return response with created = true
        var authResponse = new AuthenticationResponse(
            AccessToken: "token",
            UserId: userId,
            ProviderType: "wallet",
            ExpiresAt: DateTime.UtcNow.AddMinutes(30),
            AdditionalData: new Dictionary<string, object> { ["created"] = true });

        _orchestrator.AuthenticateWithWalletAsync(Arg.Any<WalletAuthenticationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<AuthenticationResponse, Error>(authResponse));

        var command = new VerifyWalletSignatureCommand(chainId, address, signedMessage, signature, "mac", "mkv");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Created.ShouldBeTrue();
    }

    [Test]
    public async Task VerifySignature_WithNullAdditionalData_ShouldHandleGracefully()
    {
        // Arrange
        var chainId = "eip155:1";
        var address = "0xabcdef1234567890abcdef1234567890abcdef12";
        var audience = "https://app.example.com";
        var signedMessage = $"{{\"aud\":\"{audience}\"}}";
        var signature = "sig";
        var userId = Guid.NewGuid();

        // Mock all steps to pass
        _orchestrator.ValidateChallenge(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Result.Success<bool, Error>(true));
        _orchestrator.CheckAndMarkNonceUsedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(UnitResult.Success<Error>());
        _signatureVerifier.VerifySignature(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Result.Success<bool, Error>(true));
        _addressNormalizer.NormalizeAddress(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Address.Create(address));

        // Return response with null AdditionalData
        var authResponse = new AuthenticationResponse(
            AccessToken: "token",
            UserId: userId,
            ProviderType: "wallet",
            ExpiresAt: DateTime.UtcNow.AddMinutes(30),
            AdditionalData: null);

        _orchestrator.AuthenticateWithWalletAsync(Arg.Any<WalletAuthenticationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<AuthenticationResponse, Error>(authResponse));

        var command = new VerifyWalletSignatureCommand(chainId, address, signedMessage, signature, "mac", "mkv");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Created.ShouldBeFalse(); // Should default to false when AdditionalData is null
    }

    #endregion
}