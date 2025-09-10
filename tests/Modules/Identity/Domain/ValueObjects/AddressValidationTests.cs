using Axon.Modules.Identity.Domain.Errors;
using Axon.Modules.Identity.Domain.Tests.TestData;
using Axon.Modules.Identity.Domain.ValueObjects;
using NUnit.Framework;
using Shouldly;
using Vogen;

namespace Axon.Modules.Identity.Domain.Tests.ValueObjects;

[TestFixture]
public class AddressValidationTests
{
    [TestFixture]
    public class SolanaAddressValidationTests : AddressValidationTests
    {
        // Test ID: 1.3-UNIT-017 - P1
        [TestCase("So11111111111111111111111111111111111111112")] // Valid Solana address
        [TestCase("DezXAZ8z7PnrnRJjz3wXBoRgixCa6xjnB7YaB1pPB263")] // Valid Solana address
        [TestCase("11111111111111111111111111111111")] // Valid Solana address (32 chars)
        [TestCase("EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v")] // USDC token address
        public void From_WithValidSolanaAddress_ShouldCreateAddress(string validAddress)
        {
            // Act - When creating address from valid Solana format
            var result = Address.From(validAddress);

            // Assert - Then should create successfully
            result.Value.ShouldNotBeNullOrEmpty();
            result.Value.ShouldBe(validAddress);
        }

        [TestCase("")] // Empty string
        [TestCase(" ")] // Whitespace
        [TestCase("invalid")] // Too short
        [TestCase("So11111111111111111111111111111111111111112X")] // Too long
        [TestCase("0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb1")] // Ethereum format
        [TestCase("So1111111111111111111111111111111111111111!")] // Invalid character
        [TestCase("So111111111111111111111111111111111111111")] // Too short by 1
        public void From_WithInvalidSolanaAddress_ShouldThrowValueObjectValidationException(string invalidAddress)
        {
            // Act & Assert - When creating address from invalid format
            Should.Throw<ValueObjectValidationException>(() => Address.From(invalidAddress));
        }
    }

    [TestFixture]
    public class EthereumAddressValidationTests : AddressValidationTests
    {
        [TestCase("0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb1")] // Valid Ethereum address
        [TestCase("0x0000000000000000000000000000000000000000")] // Zero address
        [TestCase("0xFFfFfFffFFfffFFfFFfFFFFFffFFFffffFfFFFfF")] // Max address
        [TestCase("0xd8dA6BF26964aF9D7eEd9e03E53415D37aA96045")] // Vitalik's address
        public void From_WithValidEthereumAddress_ShouldCreateAddress(string validAddress)
        {
            // Act - When creating address from valid Ethereum format
            var result = Address.From(validAddress);

            // Assert - Then should create successfully
            result.Value.ShouldNotBeNullOrEmpty();
            result.Value.ShouldBe(validAddress); // Should preserve original case
        }

        [TestCase("742d35Cc6634C0532925a3b844Bc9e7595f0bEb1")] // Missing 0x prefix
        [TestCase("0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb")] // Too short
        [TestCase("0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb12")] // Too long
        [TestCase("0x742d35Cc6634C0532925a3b844Bc9e7595f0bEbG")] // Invalid character
        [TestCase("0X742d35Cc6634C0532925a3b844Bc9e7595f0bEb1")] // Wrong case prefix
        public void From_WithInvalidEthereumAddress_ShouldThrowValueObjectValidationException(string invalidAddress)
        {
            // Act & Assert - When creating address from invalid format
            Should.Throw<ValueObjectValidationException>(() => Address.From(invalidAddress));
        }
    }

    [TestFixture]
    public class GeneralValidationTests : AddressValidationTests
    {
        [Test]
        public void From_WithNullAddress_ShouldThrowValueObjectValidationException()
        {
            // Act & Assert - When creating address from null
            Should.Throw<ValueObjectValidationException>(() => Address.From(null!));
        }

        [Test]
        public void From_WithEmptyAddress_ShouldThrowValueObjectValidationException()
        {
            // Act & Assert - When creating address from empty string
            Should.Throw<ValueObjectValidationException>(() => Address.From(""));
        }

        [Test]
        public void From_WithWhitespaceAddress_ShouldThrowValueObjectValidationException()
        {
            // Act & Assert - When creating address from whitespace
            Should.Throw<ValueObjectValidationException>(() => Address.From("   "));
        }

        [TestCase("So11111111111111111111111111111111111111112")]
        [TestCase("0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb1")]
        public void Value_ShouldReturnOriginalString(string originalAddress)
        {
            // Arrange - Given address created from string
            var address = Address.From(originalAddress);

            // Act - When getting value
            var value = address.Value;

            // Assert - Then should return original string
            value.ShouldBe(originalAddress);
        }

        [Test]
        public void Equals_WithSameAddress_ShouldReturnTrue()
        {
            // Arrange - Given two addresses with same value
            var address1 = Address.From(TestConstants.ValidSolanaAddress);
            var address2 = Address.From(TestConstants.ValidSolanaAddress);

            // Act & Assert - When comparing equal addresses
            address1.ShouldBe(address2);
            address1.Equals(address2).ShouldBeTrue();
            (address1 == address2).ShouldBeTrue();
            (address1 != address2).ShouldBeFalse();
        }

        [Test]
        public void Equals_WithDifferentAddress_ShouldReturnFalse()
        {
            // Arrange - Given two addresses with different values
            var address1 = Address.From(TestConstants.ValidSolanaAddress);
            var address2 = Address.From(TestConstants.ValidEthAddress);

            // Act & Assert - When comparing different addresses
            address1.ShouldNotBe(address2);
            address1.Equals(address2).ShouldBeFalse();
            (address1 == address2).ShouldBeFalse();
            (address1 != address2).ShouldBeTrue();
        }

        [Test]
        public void GetHashCode_WithSameAddress_ShouldReturnSameHash()
        {
            // Arrange - Given two addresses with same value
            var address1 = Address.From(TestConstants.ValidSolanaAddress);
            var address2 = Address.From(TestConstants.ValidSolanaAddress);

            // Act & Assert - When getting hash codes
            address1.GetHashCode().ShouldBe(address2.GetHashCode());
        }

        [Test]
        public void ToString_ShouldReturnAddressValue()
        {
            // Arrange - Given address
            var address = Address.From(TestConstants.ValidSolanaAddress);

            // Act - When converting to string
            var stringValue = address.ToString(System.Globalization.CultureInfo.InvariantCulture);

            // Assert - Then should return address value
            stringValue.ShouldBe(TestConstants.ValidSolanaAddress);
        }
    }

    [TestFixture]
    public class AddressFormatDetectionTests : AddressValidationTests
    {
        [Test]
        public void IsValidFormat_SolanaAddress_ShouldDetectCorrectly()
        {
            // Arrange - Given various Solana addresses
            var validSolanaAddresses = new[]
            {
                "So11111111111111111111111111111111111111112",
                "DezXAZ8z7PnrnRJjz3wXBoRgixCa6xjnB7YaB1pPB263",
                "11111111111111111111111111111111"
            };

            // Act & Assert - When checking format
            foreach (var address in validSolanaAddresses)
            {
                Address.From(address).Value.ShouldNotBeNullOrEmpty();
            }
        }

        [Test]
        public void IsValidFormat_EthereumAddress_ShouldDetectCorrectly()
        {
            // Arrange - Given various Ethereum addresses
            var validEthereumAddresses = new[]
            {
                "0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb1",
                "0x0000000000000000000000000000000000000000",
                "0xFFfFfFffFFfffFFfFFfFFFFFffFFFffffFfFFFfF"
            };

            // Act & Assert - When checking format
            foreach (var address in validEthereumAddresses)
            {
                Address.From(address).Value.ShouldNotBeNullOrEmpty();
            }
        }

        [TestCase("So11111111111111111111111111111111111111112", "SOLANA")]
        [TestCase("0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb1", "ETHEREUM")]
        public void DetectAddressType_ShouldIdentifyCorrectBlockchain(string addressValue, string expectedType)
        {
            // Arrange - Given address
            var address = Address.From(addressValue);

            // Act - When detecting address type (hypothetical method)
            var detectedType = GetAddressType(address);

            // Assert - Then should identify correct blockchain
            detectedType.ShouldBe(expectedType);
        }

        private static string GetAddressType(Address address)
        {
            // Simple heuristic for address type detection
            return address.Value.StartsWith("0x") ? "ETHEREUM" : "SOLANA";
        }
    }

    [TestFixture]
    public class SecurityValidationTests : AddressValidationTests
    {
        [TestCase("javascript:alert('xss')")] // Script injection attempt
        [TestCase("<script>alert('xss')</script>")] // HTML injection attempt
        [TestCase("'; DROP TABLE users; --")] // SQL injection attempt
        [TestCase("../../../etc/passwd")] // Path traversal attempt
        public void From_WithMaliciousInput_ShouldRejectSafely(string maliciousInput)
        {
            // Act & Assert - When creating address from malicious input
            Should.Throw<ValueObjectValidationException>(() => Address.From(maliciousInput));
        }

        [Test]
        public void From_WithExtremelyLongInput_ShouldRejectSafely()
        {
            // Arrange - Given extremely long input (potential DoS)
            var longInput = new string('a', 10000);

            // Act & Assert - When creating address from long input
            Should.Throw<ValueObjectValidationException>(() => Address.From(longInput));
        }

        [Test]
        public void From_WithUnicodeCharacters_ShouldRejectSafely()
        {
            // Arrange - Given input with Unicode characters
            var unicodeInput = "So1111111111111111111111111111111111111111€";

            // Act & Assert - When creating address with Unicode
            Should.Throw<ValueObjectValidationException>(() => Address.From(unicodeInput));
        }
    }
}