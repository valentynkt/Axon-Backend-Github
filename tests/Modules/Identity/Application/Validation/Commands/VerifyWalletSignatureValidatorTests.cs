using Axon.Modules.Identity.Application.Commands.VerifyWalletSignature;
using FluentValidation.TestHelper;
using NUnit.Framework;

namespace Axon.Modules.Identity.Application.Tests.Validation.Commands;

/// <summary>
/// Test suite for VerifyWalletSignatureCommand validator.
/// Tests comprehensive validation of all 4 required fields for wallet signature verification.
/// Sprint 7 - Following ExchangeCredentialValidator test patterns.
/// </summary>
[TestFixture]
public class VerifyWalletSignatureValidatorTests
{
    private VerifyWalletSignatureCommandValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _validator = new VerifyWalletSignatureCommandValidator();
    }

    #region Chain ID Validation Tests (4 tests)

    [Test]
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Validate_WithEmptyChainId_Should_HaveValidationError(string? chainId)
    {
        // Arrange
        var command = CreateValidCommand() with { ChainId = chainId! };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ChainId)
            .WithErrorMessage("Chain ID is required");
    }

    [Test]
    [TestCase("invalid")]
    [TestCase("eip155")]
    [TestCase(":1")]
    [TestCase("eip155:")]
    [TestCase("unsupported:mainnet")]
    public void Validate_WithInvalidChainId_Should_HaveValidationError(string chainId)
    {
        // Arrange
        var command = CreateValidCommand() with { ChainId = chainId };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ChainId)
            .WithErrorMessage("Chain ID must be in format '{blockchain}:{network}' (e.g., 'eip155:1', 'solana:mainnet')");
    }

    [Test]
    [TestCase("eip155:1")]
    [TestCase("solana:mainnet")]
    [TestCase("cosmos:cosmoshub-4")]
    public void Validate_WithValidChainId_Should_NotHaveChainIdError(string chainId)
    {
        // Arrange
        var command = CreateValidCommand() with { ChainId = chainId };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.ChainId);
    }

    #endregion

    #region Address Validation Tests (4 tests)

    [Test]
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Validate_WithEmptyAddress_Should_HaveValidationError(string? address)
    {
        // Arrange
        var command = CreateValidCommand() with { Address = address! };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Address)
            .WithErrorMessage("Wallet address is required");
    }

    [Test]
    [TestCase("0x123")]
    [TestCase("short")]
    public void Validate_WithTooShortAddress_Should_HaveValidationError(string address)
    {
        // Arrange
        var command = CreateValidCommand() with { Address = address };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Address)
            .WithErrorMessage("Wallet address must be at least 26 characters");
    }

    [Test]
    public void Validate_WithTooLongAddress_Should_HaveValidationError()
    {
        // Arrange
        var longAddress = new string('a', 257);
        var command = CreateValidCommand() with { Address = longAddress };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Address)
            .WithErrorMessage("Wallet address cannot exceed 256 characters");
    }

    [Test]
    [TestCase("0xabcdef1234567890abcdef1234567890abcdef12")]
    [TestCase("DYw8jCTfwHNRJhhmFcbXvVDTqWMEVFBX6ZKUmG5CNSKK")]
    public void Validate_WithValidAddress_Should_NotHaveAddressError(string address)
    {
        // Arrange
        var command = CreateValidCommand() with { Address = address };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Address);
    }

    #endregion

    #region Signed Message Validation Tests (4 tests)

    [Test]
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Validate_WithEmptySignedMessage_Should_HaveValidationError(string? signedMessage)
    {
        // Arrange
        var command = CreateValidCommand() with { SignedMessage = signedMessage! };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SignedMessage)
            .WithErrorMessage("Signed message is required");
    }

    [Test]
    [TestCase("not-json")]
    [TestCase("{invalid json}")]
    [TestCase("{'single': 'quotes'}")]
    [TestCase("{unclosed")]
    public void Validate_WithInvalidJsonMessage_Should_HaveValidationError(string signedMessage)
    {
        // Arrange
        var command = CreateValidCommand() with { SignedMessage = signedMessage };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SignedMessage)
            .WithErrorMessage("Signed message must be valid JSON");
    }

    [Test]
    [TestCase("{\"aud\":\"https://example.com\",\"nonce\":\"test-nonce-123\"}")]
    [TestCase("{\"message\":\"Sign this\"}")]
    [TestCase("{}")]
    [TestCase("{\"complex\":{\"nested\":\"value\"},\"array\":[1,2,3]}")]
    public void Validate_WithValidJsonMessage_Should_NotHaveMessageError(string signedMessage)
    {
        // Arrange
        var command = CreateValidCommand() with { SignedMessage = signedMessage };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.SignedMessage);
    }

    #endregion

    #region Signature Validation Tests (3 tests)

    [Test]
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Validate_WithEmptySignature_Should_HaveValidationError(string? signature)
    {
        // Arrange
        var command = CreateValidCommand() with { Signature = signature! };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Signature)
            .WithErrorMessage("Signature is required");
    }

    [Test]
    [TestCase("short")]
    [TestCase("0x123456789")]
    public void Validate_WithTooShortSignature_Should_HaveValidationError(string signature)
    {
        // Arrange
        var command = CreateValidCommand() with { Signature = signature };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Signature)
            .WithErrorMessage("Signature must be at least 20 characters");
    }

    [Test]
    [TestCase("0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef12")]
    [TestCase("base64-encoded-signature-value-here")]
    public void Validate_WithValidSignature_Should_NotHaveSignatureError(string signature)
    {
        // Arrange
        var command = CreateValidCommand() with { Signature = signature };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Signature);
    }

    #endregion

    #region Complete Command Validation Tests (2 tests)

    [Test]
    public void Validate_WithAllValidParameters_Should_NotHaveValidationError()
    {
        // Arrange
        var command = CreateValidCommand();

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void Validate_WithMultipleErrors_Should_ReportAllErrors()
    {
        // Arrange
        var command = new VerifyWalletSignatureCommand(
            ChainId: "",
            Address: "short",
            SignedMessage: "not-json",
            Signature: "tiny"
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ChainId);
        result.ShouldHaveValidationErrorFor(x => x.Address);
        result.ShouldHaveValidationErrorFor(x => x.SignedMessage);
        result.ShouldHaveValidationErrorFor(x => x.Signature);
    }

    #endregion

    #region Helper Methods

    private static VerifyWalletSignatureCommand CreateValidCommand() =>
        new(
            ChainId: "eip155:1",
            Address: "0xabcdef1234567890abcdef1234567890abcdef12",
            SignedMessage: "{\"aud\":\"https://example.com\",\"nonce\":\"test-nonce-123\"}",
            Signature: "0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef12"
        );

    #endregion
}