using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using Vogen;

namespace Axon.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// NetworkEnvironment value object representing the blockchain network environment for wallets and on-chain artifacts.
/// Represents the blockchain network (mainnet/devnet/testnet) for network environment isolation.
/// Note: Credentials are network-agnostic and do not use NetworkEnvironment.
/// Creation: <c>NetworkEnvironment.From("...")</c> (throws on invalid)
/// Non-throwing: <c>NetworkEnvironment.Create("...")</c> (returns Result)
/// </summary>
[ValueObject<string>(
    conversions: Conversions.SystemTextJson | Conversions.TypeConverter | Conversions.EfCoreValueConverter)]
public readonly partial struct NetworkEnvironment
{
    // Blockchain network environments (for wallets, ownership, defaults)
    public static readonly NetworkEnvironment Mainnet = From("mainnet");
    public static readonly NetworkEnvironment Devnet = From("devnet");
    public static readonly NetworkEnvironment Testnet = From("testnet");

    private static Validation Validate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Validation.Invalid("Network environment value cannot be empty.");

        var normalized = input.Trim().ToLowerInvariant();

        // Validate against known blockchain network values only
        var validValues = new[] { "mainnet", "devnet", "testnet" };
        if (!validValues.Contains(normalized))
        {
            return Validation.Invalid($"Invalid network environment value: {input}. Must be one of: {string.Join(", ", validValues)}");
        }

        return Validation.Ok;
    }

    private static string NormalizeInput(string input)
    {
        return input.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Non-throwing factory bridging Vogen to CFE Result.
    /// Preferred in application layer to avoid exception-based control flow.
    /// </summary>
    public static Result<NetworkEnvironment, Error> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<NetworkEnvironment, Error>(
                Error.Validation("Network environment value cannot be empty.", "NETWORK_ENVIRONMENT.EMPTY"));
        }

        return TryParse(value, provider: null, out var vo)
            ? Result.Success<NetworkEnvironment, Error>(vo)
            : Result.Failure<NetworkEnvironment, Error>(
                Error.Validation($"Invalid network environment value: {value}", "NETWORK_ENVIRONMENT.INVALID"));
    }

    /// <summary>
    /// Checks if this is a production blockchain network.
    /// </summary>
    public bool IsMainnet => Value == "mainnet";

    /// <summary>
    /// Checks if this is a development/testing blockchain network.
    /// </summary>
    public bool IsTestNetwork => Value == "devnet" || Value == "testnet";
}