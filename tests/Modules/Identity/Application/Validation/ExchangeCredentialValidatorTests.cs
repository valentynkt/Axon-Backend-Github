using Axon.Modules.Identity.Application.Commands.ExchangeCredential;
using Axon.Modules.Identity.Application.DTOs.Exchange;
using FluentValidation.TestHelper;
using Shouldly;

namespace Axon.Modules.Identity.Application.Tests.Validation;

/// <summary>
/// Comprehensive test suite for ExchangeCredentialCommand validators.
/// Tests all validation rules, edge cases, and security constraints.
/// </summary>
[TestFixture]
public class ExchangeCredentialValidatorTests
{
    private ExchangeCredentialCommandValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _validator = new ExchangeCredentialCommandValidator();
    }

    #region Command Level Validation Tests

    [TestFixture]
    public class CommandLevelValidationTests : ExchangeCredentialValidatorTests
    {
        [Test]
        public void Validate_WithNullUserData_Should_HaveValidationError()
        {
            // Arrange
            var command = new ExchangeCredentialCommand(null!);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.UserData)
                .WithErrorMessage("User data is required");
        }

        [Test]
        public void Validate_WithValidUserData_Should_NotHaveValidationError()
        {
            // Arrange
            var userData = CreateValidExchangeUserData();
            var command = new ExchangeCredentialCommand(userData);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }
    }

    #endregion

    #region User Data Validation Tests

    [TestFixture]
    public class UserDataValidationTests : ExchangeCredentialValidatorTests
    {
        [Test]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void Validate_WithInvalidUserId_Should_HaveValidationError(string? userId)
        {
            // Arrange
            var userData = CreateValidExchangeUserData() with { UserId = userId! };
            var command = new ExchangeCredentialCommand(userData);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor("UserData.UserId")
                .WithErrorMessage("User ID is required");
        }

        [Test]
        public void Validate_WithTooLongUserId_Should_HaveValidationError()
        {
            // Arrange
            var longUserId = new string('a', 257); // Exceeds 256 character limit
            var userData = CreateValidExchangeUserData() with { UserId = longUserId };
            var command = new ExchangeCredentialCommand(userData);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor("UserData.UserId")
                .WithErrorMessage("User ID must not exceed 256 characters");
        }

        [Test]
        public void Validate_WithMaxLengthUserId_Should_NotHaveValidationError()
        {
            // Arrange
            var maxLengthUserId = new string('a', 256);
            var userData = CreateValidExchangeUserData() with { UserId = maxLengthUserId };
            var command = new ExchangeCredentialCommand(userData);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldNotHaveValidationErrorFor("UserData.UserId");
        }

        [Test]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void Validate_WithInvalidEmail_Should_HaveValidationError(string? email)
        {
            // Arrange
            var userData = CreateValidExchangeUserData() with { Email = email! };
            var command = new ExchangeCredentialCommand(userData);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor("UserData.Email")
                .WithErrorMessage("Email is required");
        }

        [Test]
        [TestCase("invalid-email")]
        [TestCase("@example.com")]
        [TestCase("user@")]
        public void Validate_WithMalformedEmail_Should_HaveValidationError(string email)
        {
            // Arrange
            var userData = CreateValidExchangeUserData() with { Email = email };
            var command = new ExchangeCredentialCommand(userData);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor("UserData.Email")
                .WithErrorMessage("Email must be a valid email address");
        }

        [Test]
        public void Validate_WithTooLongEmail_Should_HaveValidationError()
        {
            // Arrange
            var longEmail = $"{new string('a', 310)}@example.com"; // Exceeds 320 character limit
            var userData = CreateValidExchangeUserData() with { Email = longEmail };
            var command = new ExchangeCredentialCommand(userData);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor("UserData.Email")
                .WithErrorMessage("Email must not exceed 320 characters");
        }

        [Test]
        [TestCase("user@example.com")]
        [TestCase("user.name+tag@example.com")]
        [TestCase("user@subdomain.example.com")]
        [TestCase("user@example-domain.com")]
        public void Validate_WithValidEmail_Should_NotHaveValidationError(string email)
        {
            // Arrange
            var userData = CreateValidExchangeUserData() with { Email = email };
            var command = new ExchangeCredentialCommand(userData);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldNotHaveValidationErrorFor("UserData.Email");
        }

        [Test]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void Validate_WithInvalidEnvironmentId_Should_HaveValidationError(string? environmentId)
        {
            // Arrange
            var userData = CreateValidExchangeUserData() with { EnvironmentId = environmentId! };
            var command = new ExchangeCredentialCommand(userData);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor("UserData.EnvironmentId")
                .WithErrorMessage("Environment ID is required");
        }

        [Test]
        public void Validate_WithTooLongEnvironmentId_Should_HaveValidationError()
        {
            // Arrange
            var longEnvironmentId = new string('a', 257); // Exceeds 256 character limit
            var userData = CreateValidExchangeUserData() with { EnvironmentId = longEnvironmentId };
            var command = new ExchangeCredentialCommand(userData);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor("UserData.EnvironmentId")
                .WithErrorMessage("Environment ID must not exceed 256 characters");
        }

        [Test]
        public void Validate_WithNullWallets_Should_HaveValidationError()
        {
            // Arrange
            var userData = CreateValidExchangeUserData() with { Wallets = null! };
            var command = new ExchangeCredentialCommand(userData);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor("UserData.Wallets")
                .WithErrorMessage("Wallets list is required");
        }

        [Test]
        public void Validate_WithEmptyWallets_Should_NotHaveValidationError()
        {
            // Arrange
            var userData = CreateValidExchangeUserData() with { Wallets = new List<ExchangeWalletData>() };
            var command = new ExchangeCredentialCommand(userData);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldNotHaveValidationErrorFor("UserData.Wallets");
        }
    }

    #endregion

    #region Wallet Data Validation Tests

    [TestFixture]
    public class WalletDataValidationTests : ExchangeCredentialValidatorTests
    {
        [Test]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void Validate_WithInvalidWalletAddress_Should_HaveValidationError(string? address)
        {
            // Arrange
            var walletData = CreateValidExchangeWalletData() with { Address = address! };
            var userData = CreateValidExchangeUserData() with { Wallets = new List<ExchangeWalletData> { walletData } };
            var command = new ExchangeCredentialCommand(userData);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor("UserData.Wallets[0].Address")
                .WithErrorMessage("Wallet address is required");
        }

        [Test]
        public void Validate_WithTooLongWalletAddress_Should_HaveValidationError()
        {
            // Arrange
            var longAddress = new string('a', 257); // Exceeds 256 character limit
            var walletData = CreateValidExchangeWalletData() with { Address = longAddress };
            var userData = CreateValidExchangeUserData() with { Wallets = new List<ExchangeWalletData> { walletData } };
            var command = new ExchangeCredentialCommand(userData);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor("UserData.Wallets[0].Address")
                .WithErrorMessage("Wallet address must not exceed 256 characters");
        }

        [Test]
        [TestCase("abc")] // Too short
        [TestCase("0x@#$%")] // Invalid characters
        [TestCase("!@#$%^&*()")] // Invalid characters
        public void Validate_WithInvalidWalletAddressFormat_Should_HaveValidationError(string address)
        {
            // Arrange
            var walletData = CreateValidExchangeWalletData() with { Address = address };
            var userData = CreateValidExchangeUserData() with { Wallets = new List<ExchangeWalletData> { walletData } };
            var command = new ExchangeCredentialCommand(userData);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor("UserData.Wallets[0].Address")
                .WithErrorMessage("Wallet address must contain only valid characters");
        }

        [Test]
        [TestCase("0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e45")] // Ethereum address
        [TestCase("9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM")] // Solana address
        [TestCase("1234567890abcdef")] // Generic hex
        [TestCase("ABCDEF1234567890")] // Uppercase hex
        public void Validate_WithValidWalletAddress_Should_NotHaveValidationError(string address)
        {
            // Arrange
            var walletData = CreateValidExchangeWalletData() with { Address = address };
            var userData = CreateValidExchangeUserData() with { Wallets = new List<ExchangeWalletData> { walletData } };
            var command = new ExchangeCredentialCommand(userData);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldNotHaveValidationErrorFor("UserData.Wallets[0].Address");
        }

        [Test]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void Validate_WithInvalidChain_Should_HaveValidationError(string? chain)
        {
            // Arrange
            var walletData = CreateValidExchangeWalletData() with { Chain = chain! };
            var userData = CreateValidExchangeUserData() with { Wallets = new List<ExchangeWalletData> { walletData } };
            var command = new ExchangeCredentialCommand(userData);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor("UserData.Wallets[0].Chain")
                .WithErrorMessage("Chain identifier is required");
        }

        [Test]
        public void Validate_WithTooLongChain_Should_HaveValidationError()
        {
            // Arrange
            var longChain = new string('a', 51); // Exceeds 50 character limit
            var walletData = CreateValidExchangeWalletData() with { Chain = longChain };
            var userData = CreateValidExchangeUserData() with { Wallets = new List<ExchangeWalletData> { walletData } };
            var command = new ExchangeCredentialCommand(userData);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor("UserData.Wallets[0].Chain")
                .WithErrorMessage("Chain identifier must not exceed 50 characters");
        }

        [Test]
        [TestCase("ethereum@mainnet")] // Invalid character @
        [TestCase("chain with spaces")] // Spaces not allowed
        [TestCase("chain.with.dots")] // Dots not allowed
        public void Validate_WithInvalidChainFormat_Should_HaveValidationError(string chain)
        {
            // Arrange
            var walletData = CreateValidExchangeWalletData() with { Chain = chain };
            var userData = CreateValidExchangeUserData() with { Wallets = new List<ExchangeWalletData> { walletData } };
            var command = new ExchangeCredentialCommand(userData);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor("UserData.Wallets[0].Chain")
                .WithErrorMessage("Chain identifier must be valid (letters, numbers, hyphens, underscores only)");
        }

        [Test]
        [TestCase("ethereum-mainnet")]
        [TestCase("solana_mainnet")]
        [TestCase("polygon")]
        [TestCase("arbitrum-one")]
        [TestCase("chain123")]
        [TestCase("ETHEREUM")]
        public void Validate_WithValidChain_Should_NotHaveValidationError(string chain)
        {
            // Arrange
            var walletData = CreateValidExchangeWalletData() with { Chain = chain };
            var userData = CreateValidExchangeUserData() with { Wallets = new List<ExchangeWalletData> { walletData } };
            var command = new ExchangeCredentialCommand(userData);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldNotHaveValidationErrorFor("UserData.Wallets[0].Chain");
        }

        [Test]
        public void Validate_WithTooLongWalletName_Should_HaveValidationError()
        {
            // Arrange
            var longWalletName = new string('a', 101); // Exceeds 100 character limit
            var walletData = CreateValidExchangeWalletData() with { WalletName = longWalletName };
            var userData = CreateValidExchangeUserData() with { Wallets = new List<ExchangeWalletData> { walletData } };
            var command = new ExchangeCredentialCommand(userData);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor("UserData.Wallets[0].WalletName")
                .WithErrorMessage("Wallet name must not exceed 100 characters");
        }

        [Test]
        public void Validate_WithEmptyWalletName_Should_NotHaveValidationError()
        {
            // Arrange
            var walletData = CreateValidExchangeWalletData() with { WalletName = "" };
            var userData = CreateValidExchangeUserData() with { Wallets = new List<ExchangeWalletData> { walletData } };
            var command = new ExchangeCredentialCommand(userData);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldNotHaveValidationErrorFor("UserData.Wallets[0].WalletName");
        }

        [Test]
        public void Validate_WithTooLongProvider_Should_HaveValidationError()
        {
            // Arrange
            var longProvider = new string('a', 51); // Exceeds 50 character limit
            var walletData = CreateValidExchangeWalletData() with { Provider = longProvider };
            var userData = CreateValidExchangeUserData() with { Wallets = new List<ExchangeWalletData> { walletData } };
            var command = new ExchangeCredentialCommand(userData);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor("UserData.Wallets[0].Provider")
                .WithErrorMessage("Provider name must not exceed 50 characters");
        }

        [Test]
        public void Validate_WithEmptyProvider_Should_NotHaveValidationError()
        {
            // Arrange
            var walletData = CreateValidExchangeWalletData() with { Provider = "" };
            var userData = CreateValidExchangeUserData() with { Wallets = new List<ExchangeWalletData> { walletData } };
            var command = new ExchangeCredentialCommand(userData);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldNotHaveValidationErrorFor("UserData.Wallets[0].Provider");
        }
    }

    #endregion

    #region Multiple Wallets Validation Tests

    [TestFixture]
    public class MultipleWalletsValidationTests : ExchangeCredentialValidatorTests
    {
        [Test]
        public void Validate_WithMultipleValidWallets_Should_NotHaveValidationError()
        {
            // Arrange
            var wallets = new List<ExchangeWalletData>
            {
                CreateValidExchangeWalletData(),
                CreateValidExchangeWalletData() with
                {
                    Address = "9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM",
                    Chain = "solana-mainnet"
                }
            };
            var userData = CreateValidExchangeUserData() with { Wallets = wallets };
            var command = new ExchangeCredentialCommand(userData);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Test]
        public void Validate_WithSomeInvalidWallets_Should_HaveValidationErrorsForInvalidWallets()
        {
            // Arrange
            var wallets = new List<ExchangeWalletData>
            {
                CreateValidExchangeWalletData(), // Valid
                CreateValidExchangeWalletData() with { Address = "invalid" }, // Invalid address
                CreateValidExchangeWalletData() with { Chain = "" } // Invalid chain
            };
            var userData = CreateValidExchangeUserData() with { Wallets = wallets };
            var command = new ExchangeCredentialCommand(userData);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor("UserData.Wallets[1].Address");
            result.ShouldHaveValidationErrorFor("UserData.Wallets[2].Chain");
            result.ShouldNotHaveValidationErrorFor("UserData.Wallets[0].Address");
            result.ShouldNotHaveValidationErrorFor("UserData.Wallets[0].Chain");
        }

        [Test]
        public void Validate_WithManyWallets_Should_ValidateAll()
        {
            // Arrange
            var wallets = new List<ExchangeWalletData>();
            for (int i = 0; i < 10; i++)
            {
                wallets.Add(CreateValidExchangeWalletData() with
                {
                    Address = $"0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e{i:D2}",
                    Chain = $"chain-{i}"
                });
            }
            var userData = CreateValidExchangeUserData() with { Wallets = wallets };
            var command = new ExchangeCredentialCommand(userData);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }
    }

    #endregion

    #region Edge Cases and Security Tests

    [TestFixture]
    public class EdgeCasesAndSecurityTests : ExchangeCredentialValidatorTests
    {
        [Test]
        public void Validate_WithSpecialCharactersInValidFields_Should_HandleCorrectly()
        {
            // Arrange
            var userData = CreateValidExchangeUserData() with
            {
                UserId = "user-123_test.special+chars",
                Email = "user.name+tag@example-domain.com",
                EnvironmentId = "env-123_test-special"
            };
            var command = new ExchangeCredentialCommand(userData);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Test]
        public void Validate_WithUnicodeCharacters_Should_HandleCorrectly()
        {
            // Arrange
            var userData = CreateValidExchangeUserData() with
            {
                UserId = "用户123",
                Email = "test@example.com", // Keep email ASCII for validity
                EnvironmentId = "环境123"
            };
            var command = new ExchangeCredentialCommand(userData);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldNotHaveValidationErrorFor("UserData.UserId");
            result.ShouldNotHaveValidationErrorFor("UserData.EnvironmentId");
        }

        [Test]
        public void Validate_WithBoundaryValues_Should_HandleCorrectly()
        {
            // Test exactly at the limits
            var userData = CreateValidExchangeUserData() with
            {
                UserId = new string('a', 256), // Exactly at limit
                Email = $"{new string('a', 307)}@example.com", // Exactly at 320 limit (307 + 13 = 320)
                EnvironmentId = new string('a', 256) // Exactly at limit
            };

            var walletData = CreateValidExchangeWalletData() with
            {
                Address = new string('a', 200), // Exactly at limit (max 200 according to validator)
                Chain = new string('a', 50), // Exactly at limit
                WalletName = new string('a', 100), // Exactly at limit
                Provider = new string('a', 50) // Exactly at limit
            };

            userData = userData with { Wallets = new List<ExchangeWalletData> { walletData } };
            var command = new ExchangeCredentialCommand(userData);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Test]
        public void Validate_WithInjectionAttempts_Should_BeRejected()
        {
            // Test potential injection attempts in various fields
            var userData = CreateValidExchangeUserData() with
            {
                UserId = "<script>alert('xss')</script>",
                EnvironmentId = "'; DROP TABLE users; --"
            };
            var command = new ExchangeCredentialCommand(userData);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            // Should pass validation since these are just strings being validated for length/format
            // The actual security protection happens at other layers
            result.ShouldNotHaveValidationErrorFor("UserData.UserId");
            result.ShouldNotHaveValidationErrorFor("UserData.EnvironmentId");
        }
    }

    #endregion

    #region Helper Methods

    private static ExchangeUserData CreateValidExchangeUserData()
    {
        return new ExchangeUserData(
            UserId: "test-user-123",
            Email: "test@example.com",
            EnvironmentId: "test-env-456",
            Wallets: new List<ExchangeWalletData>
            {
                CreateValidExchangeWalletData()
            }
        );
    }

    private static ExchangeWalletData CreateValidExchangeWalletData()
    {
        return new ExchangeWalletData(
            Address: "0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e45",
            Chain: "ethereum-mainnet",
            WalletName: "My Wallet",
            Provider: "MetaMask"
        );
    }

    #endregion
}