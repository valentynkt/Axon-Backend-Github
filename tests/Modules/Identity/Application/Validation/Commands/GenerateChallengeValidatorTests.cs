using Axon.Modules.Identity.Application.Commands.GenerateChallenge;
using FluentValidation.TestHelper;
using NUnit.Framework;

namespace Axon.Modules.Identity.Application.Tests.Validation.Commands;

/// <summary>
/// Test suite for GenerateChallengeCommand validator.
/// Tests CAIP-2 chain ID format, wallet address, and optional audience validation.
/// Sprint 7 - Following ExchangeCredentialValidator test patterns.
/// </summary>
[TestFixture]
public class GenerateChallengeValidatorTests
{
    private GenerateChallengeCommandValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _validator = new GenerateChallengeCommandValidator();
    }

    #region Chain ID Validation Tests (6 tests)

    [Test]
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Validate_WithEmptyChainId_Should_HaveValidationError(string? chainId)
    {
        // Arrange
        var command = new GenerateChallengeCommand(chainId!, "0xabcdef1234567890abcdef1234567890abcdef12");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ChainId)
            .WithErrorMessage("Chain ID is required");
    }

    [Test]
    [TestCase("invalid")]
    [TestCase("no-separator")]
    [TestCase("eip155")]
    [TestCase("eip155:")]
    [TestCase(":1")]
    [TestCase("eip155::1")]
    [TestCase("eip155:1:extra")]
    public void Validate_WithInvalidChainIdFormat_Should_HaveValidationError(string chainId)
    {
        // Arrange
        var command = new GenerateChallengeCommand(chainId, "0xabcdef1234567890abcdef1234567890abcdef12");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ChainId)
            .WithErrorMessage("Chain ID must be in format '{blockchain}:{network}' (e.g., 'eip155:1', 'solana:mainnet')");
    }

    [Test]
    [TestCase("unsupported:mainnet")]
    [TestCase("bitcoin:mainnet")]
    [TestCase("ethereum:1")]
    public void Validate_WithUnsupportedBlockchain_Should_HaveValidationError(string chainId)
    {
        // Arrange
        var command = new GenerateChallengeCommand(chainId, "0xabcdef1234567890abcdef1234567890abcdef12");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ChainId)
            .WithErrorMessage("Chain ID must be in format '{blockchain}:{network}' (e.g., 'eip155:1', 'solana:mainnet')");
    }

    [Test]
    [TestCase("eip155:1")]
    [TestCase("eip155:137")]
    [TestCase("solana:mainnet")]
    [TestCase("solana:devnet")]
    [TestCase("cosmos:cosmoshub-4")]
    [TestCase("polkadot:mainnet")]
    public void Validate_WithValidChainId_Should_NotHaveChainIdError(string chainId)
    {
        // Arrange
        var command = new GenerateChallengeCommand(chainId, "0xabcdef1234567890abcdef1234567890abcdef12");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.ChainId);
    }

    #endregion

    #region Wallet Address Validation Tests (5 tests)

    [Test]
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Validate_WithEmptyWalletAddress_Should_HaveValidationError(string? walletAddress)
    {
        // Arrange
        var command = new GenerateChallengeCommand("eip155:1", walletAddress!);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.WalletAddress)
            .WithErrorMessage("Wallet address is required");
    }

    [Test]
    [TestCase("0x123")]
    [TestCase("short")]
    [TestCase("0xabcdef")]
    public void Validate_WithTooShortWalletAddress_Should_HaveValidationError(string walletAddress)
    {
        // Arrange
        var command = new GenerateChallengeCommand("eip155:1", walletAddress);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.WalletAddress)
            .WithErrorMessage("Wallet address must be at least 26 characters");
    }

    [Test]
    public void Validate_WithTooLongWalletAddress_Should_HaveValidationError()
    {
        // Arrange
        var longAddress = new string('a', 257); // Exceeds 256 character limit
        var command = new GenerateChallengeCommand("eip155:1", longAddress);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.WalletAddress)
            .WithErrorMessage("Wallet address cannot exceed 256 characters");
    }

    [Test]
    [TestCase("0xabcdef1234567890abcdef1234567890abcdef12")] // EVM address (42 chars)
    [TestCase("DYw8jCTfwHNRJhhmFcbXvVDTqWMEVFBX6ZKUmG5CNSKK")] // Solana address (44 chars)
    [TestCase("cosmos1234567890abcdefghijklmnopqrstuvwxyz123")] // Cosmos address
    public void Validate_WithValidWalletAddresses_Should_NotHaveAddressError(string walletAddress)
    {
        // Arrange
        var command = new GenerateChallengeCommand("eip155:1", walletAddress);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.WalletAddress);
    }

    #endregion

    #region Audience Validation Tests (6 tests)

    [Test]
    public void Validate_WithNullAudience_Should_NotHaveValidationError()
    {
        // Arrange
        var command = new GenerateChallengeCommand("eip155:1", "0xabcdef1234567890abcdef1234567890abcdef12", null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Audience);
    }

    [Test]
    public void Validate_WithEmptyAudience_Should_NotHaveValidationError()
    {
        // Arrange
        var command = new GenerateChallengeCommand("eip155:1", "0xabcdef1234567890abcdef1234567890abcdef12", "");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Audience);
    }

    [Test]
    public void Validate_WithTooLongAudience_Should_HaveValidationError()
    {
        // Arrange
        var longAudience = "https://example.com/" + new string('a', 237); // Exceeds 256 chars
        var command = new GenerateChallengeCommand("eip155:1", "0xabcdef1234567890abcdef1234567890abcdef12", longAudience);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Audience)
            .WithErrorMessage("Audience cannot exceed 256 characters");
    }

    [Test]
    [TestCase("not-a-url")]
    [TestCase("invalid://url")]
    [TestCase("ftp://example.com")]
    [TestCase("file:///path/to/file")]
    public void Validate_WithInvalidAudienceUrl_Should_HaveValidationError(string audience)
    {
        // Arrange
        var command = new GenerateChallengeCommand("eip155:1", "0xabcdef1234567890abcdef1234567890abcdef12", audience);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Audience)
            .WithErrorMessage("Audience must be a valid URL");
    }

    [Test]
    [TestCase("https://example.com")]
    [TestCase("http://localhost:3000")]
    [TestCase("https://app.dynamic.xyz")]
    [TestCase("https://subdomain.example.com/path?query=value")]
    public void Validate_WithValidAudienceUrls_Should_NotHaveAudienceError(string audience)
    {
        // Arrange
        var command = new GenerateChallengeCommand("eip155:1", "0xabcdef1234567890abcdef1234567890abcdef12", audience);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Audience);
    }

    #endregion

    #region Complete Command Validation Tests (2 tests)

    [Test]
    public void Validate_WithAllValidParameters_Should_NotHaveValidationError()
    {
        // Arrange
        var command = new GenerateChallengeCommand(
            "eip155:1",
            "0xabcdef1234567890abcdef1234567890abcdef12",
            "https://app.example.com"
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void Validate_WithMinimalValidParameters_Should_NotHaveValidationError()
    {
        // Arrange
        var command = new GenerateChallengeCommand(
            "solana:mainnet",
            "DYw8jCTfwHNRJhhmFcbXvVDTqWMEVFBX6ZKUmG5CNSKK",
            null
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion
}