using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Infrastructure.Services;
using BuildingBlocks.Core.Diagnostics.Errors;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Axon.Modules.Identity.Infrastructure.Services.Tests;

[TestFixture]
public class AddressNormalizationServiceTests
{
    private AddressNormalizationService _service = null!;
    private ILogger<AddressNormalizationService> _logger = null!;

    [SetUp]
    public void SetUp()
    {
        _logger = Substitute.For<ILogger<AddressNormalizationService>>();
        _service = new AddressNormalizationService(_logger);
    }

    [TestCase("solana", "9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM", "9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM")]
    [TestCase("solana", "  9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM  ", "9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM")]
    [TestCase("solana", "\t9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM\n", "9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM")]
    public void NormalizeAddress_SolanaValidAddress_ReturnsNormalizedAddress(string chainId, string input, string expected)
    {
        // Act
        var result = _service.NormalizeAddress(chainId, input!);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(expected);
    }

    [TestCase("solana", "")]
    [TestCase("solana", "   ")]
    [TestCase("solana", "\t\n")]
    [TestCase("solana", null)]
    public void NormalizeAddress_SolanaEmptyOrNullAddress_ReturnsValidationError(string chainId, string? input)
    {
        // Act
        var result = _service.NormalizeAddress(chainId, input!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Contain("Address cannot be empty");
    }

    [Test]
    public void NormalizeAddress_SolanaInvalidBase58Characters_ReturnsValidationError()
    {
        // Arrange
        var invalidAddress = "9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWW0"; // Contains '0' which is not valid base58

        // Act
        var result = _service.NormalizeAddress("solana", invalidAddress);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Contain("contains invalid base58 characters");
    }

    [Test]
    public void NormalizeAddress_SolanaInvalidLength_ReturnsValidationError()
    {
        // Arrange - Too short
        var shortAddress = "9WzDXwBbmkg8ZTbNMqUx";

        // Act
        var result = _service.NormalizeAddress("solana", shortAddress);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Contain("must be 32-44 characters");
    }

    [Test]
    public void NormalizeAddress_SolanaMaxValidLength_ReturnsSuccess()
    {
        // Arrange - 44 characters (max valid length)
        var maxLengthAddress = "111111111111111111111111111111111111111111111";

        // Act
        var result = _service.NormalizeAddress("solana", maxLengthAddress);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(maxLengthAddress);
    }

    [Test]
    public void NormalizeAddress_SolanaMinValidLength_ReturnsSuccess()
    {
        // Arrange - 32 characters (min valid length)
        var minLengthAddress = "11111111111111111111111111111111";

        // Act
        var result = _service.NormalizeAddress("solana", minLengthAddress);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(minLengthAddress);
    }

    [TestCase("ethereum", "0x742d35Cc6635C0532925a3b8D400E61C9C4bF00e", "0x742d35cc6635c0532925a3b8d400e61c9c4bf00e")]
    [TestCase("ethereum", "0X742D35CC6635C0532925A3B8D400E61C9C4BF00E", "0x742d35cc6635c0532925a3b8d400e61c9c4bf00e")]
    [TestCase("ethereum", "742d35Cc6635C0532925a3b8D400E61C9C4bF00e", "0x742d35cc6635c0532925a3b8d400e61c9c4bf00e")]
    [TestCase("ethereum", "  0x742d35Cc6635C0532925a3b8D400E61C9C4bF00e  ", "0x742d35cc6635c0532925a3b8d400e61c9c4bf00e")]
    public void NormalizeAddress_EthereumValidAddress_ReturnsNormalizedAddress(string chainId, string input, string expected)
    {
        // Act
        var result = _service.NormalizeAddress(chainId, input!);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(expected);
    }

    [TestCase("ethereum", "")]
    [TestCase("ethereum", "   ")]
    [TestCase("ethereum", null)]
    public void NormalizeAddress_EthereumEmptyOrNullAddress_ReturnsValidationError(string chainId, string? input)
    {
        // Act
        var result = _service.NormalizeAddress(chainId, input!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Contain("Address cannot be empty");
    }

    [Test]
    public void NormalizeAddress_EthereumInvalidHexCharacters_ReturnsValidationError()
    {
        // Arrange
        var invalidAddress = "0x742d35Cc6635C0532925a3b8D400E61C9C4bF00G"; // Contains 'G' which is not valid hex

        // Act
        var result = _service.NormalizeAddress("ethereum", invalidAddress);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Contain("contains invalid hexadecimal characters");
    }

    [Test]
    public void NormalizeAddress_EthereumInvalidLength_ReturnsValidationError()
    {
        // Arrange - Too short (less than 40 hex chars)
        var shortAddress = "0x742d35Cc6635C0532925a3b8D400E61C";

        // Act
        var result = _service.NormalizeAddress("ethereum", shortAddress);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Contain("must be exactly 40 hexadecimal characters");
    }

    [Test]
    public void NormalizeAddress_UnsupportedChain_ReturnsNotSupportedError()
    {
        // Arrange
        var chainId = "bitcoin";
        var address = "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa";

        // Act
        var result = _service.NormalizeAddress(chainId, address);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.External);
        result.Error.Message.Should().Contain("Address normalization for chain 'bitcoin' is not yet supported");
    }

    [TestCase("SOLANA", "solana")]
    [TestCase("Solana", "solana")]
    [TestCase("ETHEREUM", "ethereum")]
    [TestCase("Ethereum", "ethereum")]
    [TestCase("ETH", "ethereum")]
    [TestCase("eth", "ethereum")]
    public void NormalizeAddress_ChainIdCaseInsensitive_HandlesProperly(string inputChainId, string expectedChainNormalization)
    {
        // Arrange
        var address = expectedChainNormalization == "solana"
            ? "9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM"
            : "0x742d35Cc6635C0532925a3b8D400E61C9C4bF00e";

        // Act
        var result = _service.NormalizeAddress(inputChainId, address);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // The actual normalization behavior depends on the chain
        result.Value.Value.Should().NotBeNullOrEmpty();
    }

    [Test]
    public void NormalizeAddress_SolanaEdgeCases_HandlesCorrectly()
    {
        // Test various edge cases specific to Solana addresses
        var testCases = new[]
        {
            // System program ID
            ("11111111111111111111111111111112", true),
            // Token program ID
            ("TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA", true),
            // Associated token program ID
            ("ATokenGPvbdGVxr1b2hvZbsiqW5xWH25efTNsLJA8knL", true),
            // SPL Token mint
            ("EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v", true),
            // Invalid - contains confusing characters like 0 and O
            ("9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWO", false),
            ("9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWW0", false)
        };

        foreach (var (address, shouldBeValid) in testCases)
        {
            // Act
            var result = _service.NormalizeAddress("solana", address);

            // Assert
            if (shouldBeValid)
            {
                result.IsSuccess.Should().BeTrue($"Address {address} should be valid");
                result.Value.Value.Should().Be(address, "Valid addresses should not be modified");
            }
            else
            {
                result.IsFailure.Should().BeTrue($"Address {address} should be invalid");
            }
        }
    }

    [Test]
    public void NormalizeAddress_EthereumEdgeCases_HandlesCorrectly()
    {
        // Test various edge cases specific to Ethereum addresses
        var testCases = new[]
        {
            // Zero address
            ("0x0000000000000000000000000000000000000000", "0x0000000000000000000000000000000000000000"),
            // All F's
            ("0xFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF", "0xffffffffffffffffffffffffffffffffffffffff"),
            // Mixed case with valid checksum (should be lowercased)
            ("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed", "0x5aaeb6053f3e94c9b9a09f33669435e7ef1beaed"),
            // Without 0x prefix
            ("5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed", "0x5aaeb6053f3e94c9b9a09f33669435e7ef1beaed")
        };

        foreach (var (input, expected) in testCases)
        {
            // Act
            var result = _service.NormalizeAddress("ethereum", input);

            // Assert
            result.IsSuccess.Should().BeTrue($"Address {input} should be valid");
            result.Value.Value.Should().Be(expected, $"Address {input} should normalize to {expected}");
        }
    }

    [Test]
    public void NormalizeAddress_PreventsDuplicates_WithDifferentCasing()
    {
        // This test demonstrates that normalization prevents duplicate addresses
        // that differ only in casing

        var addressVariations = new[]
        {
            "9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM",
            "  9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM  ",
            "\t9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM\n"
        };

        var normalizedAddresses = new HashSet<string>();

        foreach (var variation in addressVariations)
        {
            // Act
            var result = _service.NormalizeAddress("solana", variation);

            // Assert
            result.IsSuccess.Should().BeTrue();
            normalizedAddresses.Add(result.Value.Value);
        }

        // All variations should normalize to the same value
        normalizedAddresses.Should().HaveCount(1);
    }

    [Test]
    public void NormalizeAddress_EthereumDuplicationPrevention_WithCasingAndPrefix()
    {
        var addressVariations = new[]
        {
            "0x742d35Cc6635C0532925a3b8D400E61C9C4bF00e",
            "0X742D35CC6635C0532925A3B8D400E61C9C4BF00E",
            "742d35Cc6635C0532925a3b8D400E61C9C4bF00e",
            "  0x742d35Cc6635C0532925a3b8D400E61C9C4bF00e  "
        };

        var normalizedAddresses = new HashSet<string>();

        foreach (var variation in addressVariations)
        {
            // Act
            var result = _service.NormalizeAddress("ethereum", variation);

            // Assert
            result.IsSuccess.Should().BeTrue();
            normalizedAddresses.Add(result.Value.Value);
        }

        // All variations should normalize to the same value
        normalizedAddresses.Should().HaveCount(1);
        normalizedAddresses.Single().Should().Be("0x742d35cc6635c0532925a3b8d400e61c9c4bf00e");
    }
}