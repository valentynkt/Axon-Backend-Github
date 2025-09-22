using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Infrastructure.Services;

public sealed class AddressNormalizationService : IAddressNormalizationService
{
    private readonly ILogger<AddressNormalizationService> _logger;

    public AddressNormalizationService(ILogger<AddressNormalizationService> logger)
    {
        _logger = logger;
    }

    public Result<Address, Error> NormalizeAddress(string chainId, string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return Result.Failure<Address, Error>(Error.Validation("Address cannot be empty"));
        }

        var trimmedAddress = address.Trim();

        return chainId.ToLowerInvariant() switch
        {
            "solana" => NormalizeSolanaAddress(trimmedAddress),
            "ethereum" => NormalizeEvmAddress(trimmedAddress),
            "polygon" => NormalizeEvmAddress(trimmedAddress),
            "arbitrum" => NormalizeEvmAddress(trimmedAddress),
            "optimism" => NormalizeEvmAddress(trimmedAddress),
            "base" => NormalizeEvmAddress(trimmedAddress),
            _ => Result.Success<Address, Error>(Address.From(trimmedAddress))
        };
    }

    private Result<Address, Error> NormalizeSolanaAddress(string address)
    {
        // Validate base58 format
        if (!IsValidBase58(address))
        {
            _logger.LogWarning("Invalid Solana address format: {Address}", address);
            return Result.Failure<Address, Error>(Error.Validation("Invalid Solana address: not valid base58"));
        }

        // Check length (Solana addresses are typically 32-44 characters)
        if (address.Length < 32 || address.Length > 44)
        {
            _logger.LogWarning("Invalid Solana address length: {Length}", address.Length);
            return Result.Failure<Address, Error>(Error.Validation($"Invalid Solana address length: {address.Length}"));
        }

        // Solana addresses are case-sensitive, return as-is after validation
        return Result.Success<Address, Error>(Address.From(address));
    }

    private Result<Address, Error> NormalizeEvmAddress(string address)
    {
        // Remove 0x prefix if present for validation
        var cleanAddress = address.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? address[2..]
            : address;

        // Validate hex format
        if (!IsValidHex(cleanAddress))
        {
            _logger.LogWarning("Invalid EVM address format: {Address}", address);
            return Result.Failure<Address, Error>(Error.Validation("Invalid EVM address: not valid hexadecimal"));
        }

        // Check length (EVM addresses are 40 hex characters without 0x)
        if (cleanAddress.Length != 40)
        {
            _logger.LogWarning("Invalid EVM address length: {Length}", cleanAddress.Length);
            return Result.Failure<Address, Error>(Error.Validation($"Invalid EVM address length: {cleanAddress.Length}"));
        }

        // Normalize to lowercase with 0x prefix (EIP-55 checksum optional for now)
        var normalizedAddress = $"0x{cleanAddress.ToLowerInvariant()}";
        return Result.Success<Address, Error>(Address.From(normalizedAddress));
    }

    private static bool IsValidBase58(string value)
    {
        const string base58Chars = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
        return value.All(c => base58Chars.Contains(c));
    }

    private static bool IsValidHex(string value)
    {
        return value.All(c => "0123456789abcdefABCDEF".Contains(c));
    }
}