using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Application.Tests._TestInfrastructure.Fixtures;

/// <summary>
/// Test data fixtures for Application layer tests.
/// Contains minimal test data without database dependencies.
/// </summary>
public static class TestDataFixtures
{
    // Constants for test data
    public const string DynamicIssuer = "https://app.dynamic.xyz/test";
    public const string DynA_Subject = "dyn_user_a_12345";
    public const string DynB_Subject = "dyn_user_b_67890";
    public const string SolanaMainnetChain = "solana-mainnet";
    public const string SolanaDevnetChain = "solana-devnet";
    public const string W1MainAddress = "9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM";
    public const string W1DevAddress = "9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM";
    public const string W2MainAddress = "CuieVDEDtLo7FypA9SbLM9saXFdb1dsshEkyErMqkRQq";

    /// <summary>
    /// Test timestamp for consistent time-based tests.
    /// </summary>
    public static class SignatureTestVectors
    {
        public static readonly DateTime TestTimestamp = new(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);
    }

    /// <summary>
    /// Creates test principal A with Dynamic credential.
    /// </summary>
    public static AxonPrincipal CreatePrincipalA()
    {
        var providerType = ProviderType.Create("dynamic").Value;
        return AxonPrincipal.CreateWithDynamicCredential(
            providerType,
            DynamicIssuer,
            DynA_Subject).Value;
    }

    /// <summary>
    /// Creates test principal B with different Dynamic credential.
    /// </summary>
    public static AxonPrincipal CreatePrincipalB()
    {
        var providerType = ProviderType.Create("dynamic").Value;
        return AxonPrincipal.CreateWithDynamicCredential(
            providerType,
            DynamicIssuer,
            DynB_Subject).Value;
    }

    /// <summary>
    /// Creates test wallet W1 for mainnet.
    /// </summary>
    public static Wallet CreateW1Main()
    {
        return Wallet.Create(
            null,
            SolanaMainnetChain,
            Address.Create(W1MainAddress).Value);
    }

    /// <summary>
    /// Creates test wallet W1 for devnet.
    /// </summary>
    public static Wallet CreateW1Dev()
    {
        return Wallet.Create(
            null,
            SolanaDevnetChain,
            Address.Create(W1DevAddress).Value);
    }

    /// <summary>
    /// Creates test wallet W2 for mainnet.
    /// </summary>
    public static Wallet CreateW2Main()
    {
        return Wallet.Create(
            null,
            SolanaMainnetChain,
            Address.Create(W2MainAddress).Value);
    }

    /// <summary>
    /// Creates custom wallet with specified chain and address.
    /// </summary>
    public static Wallet CreateCustomWallet(string chainId, string address)
    {
        return Wallet.Create(
            null,
            chainId,
            Address.Create(address).Value);
    }

    /// <summary>
    /// Creates verified signing ownership for testing.
    /// </summary>
    public static WalletOwnership CreateVerifiedSigningOwnership(AxonUserId principalId, WalletId walletId)
    {
        return WalletOwnership.Create(
            principalId,
            walletId,
            AccessMode.Signing,
            OwnershipStatus.Verified);
    }

    /// <summary>
    /// Creates pending signing ownership for testing.
    /// </summary>
    public static WalletOwnership CreatePendingSigningOwnership(AxonUserId principalId, WalletId walletId)
    {
        return WalletOwnership.Create(
            principalId,
            walletId,
            AccessMode.Signing,
            OwnershipStatus.Pending);
    }

    /// <summary>
    /// Creates a plain human principal without credentials.
    /// </summary>
    public static AxonPrincipal CreatePlainPrincipal()
    {
        return AxonPrincipal.CreateHuman();
    }

    /// <summary>
    /// Creates single active owner scenario for testing.
    /// </summary>
    public static (AxonPrincipal principal, Wallet wallet) CreateSingleActiveOwnerScenario()
    {
        var principal = AxonPrincipal.CreateHuman();
        var wallet = CreateW1Main();
        var ownership = CreateVerifiedSigningOwnership(principal.Id, wallet.Id);
        principal.LinkWalletOwnership(ownership, (_, _, _) => Result.Success<bool, Error>(false));
        return (principal, wallet);
    }

    /// <summary>
    /// Creates no match scenario for testing new principal creation.
    /// </summary>
    public static (string unknownSubject, string unknownWalletAddress) CreateNoMatchScenario()
    {
        return ("unknown_subject_99999", "UnknownWalletAddress12345");
    }
}