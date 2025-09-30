using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Infrastructure.Tests.Persistence.DbInvariants;

/// <summary>
/// Canonical test data fixtures from TDD document.
/// Provides consistent, deterministic test data across all DB invariant tests.
/// </summary>
public static class TestDataFixtures
{
    #region Environments

    /// <summary>
    /// Production environment identifier.
    /// </summary>
    public const string MainnetEnvironment = "mainnet";

    /// <summary>
    /// Development environment identifier.
    /// </summary>
    public const string DevnetEnvironment = "devnet";

    #endregion

    #region Chain Identifiers

    /// <summary>
    /// Solana mainnet chain identifier.
    /// </summary>
    public const string SolanaMainnetChain = "solana-mainnet";

    /// <summary>
    /// Solana devnet chain identifier.
    /// </summary>
    public const string SolanaDevnetChain = "solana-devnet";

    #endregion

    #region Wallet Addresses

    /// <summary>
    /// Wallet address W1 for mainnet chain.
    /// </summary>
    public const string W1MainAddress = "DhQ7ZbWfF5h7j8K2mN9pL3rS4tU6vX8yA1bC2dE3fG4h";

    /// <summary>
    /// Wallet address W1 for devnet chain - same address but different chain.
    /// </summary>
    public const string W1DevAddress = "DhQ7ZbWfF5h7j8K2mN9pL3rS4tU6vX8yA1bC2dE3fG4h";

    /// <summary>
    /// Wallet address W2 for mainnet - different from W1.
    /// </summary>
    public const string W2MainAddress = "9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM";

    #endregion

    #region Dynamic JWT Data

    /// <summary>
    /// Dynamic environment ID for testing (tenant-specific).
    /// </summary>
    public const string DynamicEnvironmentId = "dyn_test_env_12345";

    /// <summary>
    /// Dynamic issuer for testing - constructed to match handler logic.
    /// </summary>
    public const string DynamicIssuer = "app.dynamicauth.com/dyn_test_env_12345";

    /// <summary>
    /// Subject for Dynamic JWT user A.
    /// </summary>
    public const string DynA_Subject = "dyn_user_a_12345";

    /// <summary>
    /// Subject for Dynamic JWT user A with rotated key.
    /// </summary>
    public const string DynA_Rotated_Subject = "dyn_user_a_12345"; // Same subject, different kid

    /// <summary>
    /// Key ID 1 for Dynamic JWT.
    /// </summary>
    public const string Kid1 = "dyn_kid_001";

    /// <summary>
    /// Key ID 2 for rotated Dynamic JWT.
    /// </summary>
    public const string Kid2 = "dyn_kid_002";

    #endregion

    #region Principal Factory Methods

    /// <summary>
    /// Creates Principal A - primary test principal.
    /// </summary>
    public static AxonPrincipal CreatePrincipalA(AxonUserId? id = null)
    {
        return AxonPrincipal.CreateWithDynamicCredential(
            ProviderType.Create("dynamic").Value,
            DynamicIssuer,
            DynA_Subject,
            id).Value;
    }

    /// <summary>
    /// Creates Principal B - secondary test principal.
    /// </summary>
    public static AxonPrincipal CreatePrincipalB(AxonUserId? id = null)
    {
        return AxonPrincipal.CreateWithDynamicCredential(
            ProviderType.Create("dynamic").Value,
            DynamicIssuer,
            "dyn_user_b_67890",
            id).Value;
    }

    /// <summary>
    /// Creates a human principal without credentials.
    /// </summary>
    public static AxonPrincipal CreatePlainPrincipal(AxonUserId? id = null)
    {
        return AxonPrincipal.CreateHuman(id);
    }

    #endregion

    #region Wallet Factory Methods

    /// <summary>
    /// Creates wallet W1 for mainnet chain.
    /// </summary>
    public static Wallet CreateW1Main(WalletId? id = null)
    {
        return Wallet.Create(
            id,
            SolanaMainnetChain,
            Address.Create(W1MainAddress).Value);
    }

    /// <summary>
    /// Creates wallet W1 for devnet chain.
    /// </summary>
    public static Wallet CreateW1Dev(WalletId? id = null)
    {
        return Wallet.Create(
            id,
            SolanaDevnetChain,
            Address.Create(W1DevAddress).Value);
    }

    /// <summary>
    /// Creates wallet W2 for mainnet chain.
    /// </summary>
    public static Wallet CreateW2Main(WalletId? id = null)
    {
        return Wallet.Create(
            id,
            SolanaMainnetChain,
            Address.Create(W2MainAddress).Value);
    }

    /// <summary>
    /// Creates a wallet with custom parameters for edge case testing.
    /// </summary>
    public static Wallet CreateCustomWallet(
        string chainId,
        string address,
        WalletId? id = null)
    {
        ArgumentNullException.ThrowIfNull(chainId);

        return Wallet.Create(
            id,
            chainId,
            Address.Create(address).Value);
    }

    #endregion

    #region Ownership Factory Methods

    /// <summary>
    /// Creates verified signing ownership.
    /// </summary>
    public static WalletOwnership CreateVerifiedSigningOwnership(
        AxonUserId principalId,
        WalletId walletId)
    {
        return WalletOwnership.Create(
            principalId,
            walletId,
            AccessMode.Signing,
            OwnershipStatus.Verified);
    }

    /// <summary>
    /// Creates pending signing ownership.
    /// </summary>
    public static WalletOwnership CreatePendingSigningOwnership(
        AxonUserId principalId,
        WalletId walletId)
    {
        return WalletOwnership.Create(
            principalId,
            walletId,
            AccessMode.Signing,
            OwnershipStatus.Pending);
    }

    /// <summary>
    /// Creates verified watch-only ownership.
    /// </summary>
    public static WalletOwnership CreateVerifiedWatchOnlyOwnership(
        AxonUserId principalId,
        WalletId walletId)
    {
        return WalletOwnership.Create(
            principalId,
            walletId,
            AccessMode.WatchOnly,
            OwnershipStatus.Verified);
    }

    /// <summary>
    /// Creates pending watch-only ownership.
    /// </summary>
    public static WalletOwnership CreatePendingWatchOnlyOwnership(
        AxonUserId principalId,
        WalletId walletId)
    {
        return WalletOwnership.Create(
            principalId,
            walletId,
            AccessMode.WatchOnly,
            OwnershipStatus.Pending);
    }

    #endregion

    #region Credential Factory Methods

    /// <summary>
    /// Creates a Dynamic credential for user A for the specified principal.
    /// </summary>
    public static IdentityCredential CreateDynACredential(AxonUserId principalId)
    {
        return IdentityCredential.Create(
            principalId,
            ProviderType.Create("dynamic").Value.Value,
            DynamicIssuer,
            DynA_Subject);
    }

    /// <summary>
    /// Creates a Dynamic credential for user B for the specified principal.
    /// </summary>
    public static IdentityCredential CreateDynBCredential(AxonUserId principalId)
    {
        return IdentityCredential.Create(
            principalId,
            ProviderType.Create("dynamic").Value.Value,
            DynamicIssuer,
            "dyn_user_b_67890");
    }

    #endregion

    #region Default Wallet Factory Methods

    /// <summary>
    /// Creates a principal chain default for mainnet Solana.
    /// </summary>
    public static PrincipalChainDefault CreateMainnetSolanaDefault(
        AxonUserId principalId,
        WalletId walletId)
    {
        return PrincipalChainDefault.Create(
            principalId,
            SolanaMainnetChain,
            walletId);
    }

    /// <summary>
    /// Creates a principal chain default for devnet Solana.
    /// </summary>
    public static PrincipalChainDefault CreateDevnetSolanaDefault(
        AxonUserId principalId,
        WalletId walletId)
    {
        return PrincipalChainDefault.Create(
            principalId,
            SolanaDevnetChain,
            walletId);
    }

    /// <summary>
    /// Creates a custom principal chain default.
    /// </summary>
    public static PrincipalChainDefault CreateCustomDefault(
        AxonUserId principalId,
        string chainId,
        WalletId walletId)
    {
        return PrincipalChainDefault.Create(
            principalId,
            chainId,
            walletId);
    }

    #endregion

    #region Test Scenario Factory Methods

    /// <summary>
    /// Creates a complete mainnet test scenario with principal, wallet, and ownership.
    /// </summary>
    public static (AxonPrincipal principal, Wallet wallet, WalletOwnership ownership) CreateMainnetScenario()
    {
        var principal = CreatePrincipalA();
        var wallet = CreateW1Main();
        var ownership = CreateVerifiedSigningOwnership(principal.Id, wallet.Id);

        return (principal, wallet, ownership);
    }

    /// <summary>
    /// Creates a complete devnet test scenario with principal, wallet, and ownership.
    /// </summary>
    public static (AxonPrincipal principal, Wallet wallet, WalletOwnership ownership) CreateDevnetScenario()
    {
        var principal = CreatePrincipalA();
        var wallet = CreateW1Dev();
        var ownership = CreateVerifiedSigningOwnership(principal.Id, wallet.Id);

        return (principal, wallet, ownership);
    }

    /// <summary>
    /// Creates a two-principal scenario for exclusivity testing.
    /// </summary>
    public static (AxonPrincipal principalA, AxonPrincipal principalB, Wallet sharedWallet) CreateExclusivityScenario()
    {
        var principalA = CreatePrincipalA();
        var principalB = CreatePrincipalB();
        var sharedWallet = CreateW1Main();

        return (principalA, principalB, sharedWallet);
    }

    #endregion

    #region Resolution Algorithm Test Scenarios (Section B)

    /// <summary>
    /// Creates scenario for Test 7: RESOLVE_credential_first
    /// Principal exists with Dynamic credential - should resolve via credential lookup.
    /// </summary>
    public static (AxonPrincipal principal, Wallet wallet) CreateCredentialFirstScenario()
    {
        var principal = CreatePrincipalA(); // Has Dynamic credential
        var wallet = CreateW1Main();

        // Link wallet to principal with verified signing ownership
        var ownership = CreateVerifiedSigningOwnership(principal.Id, wallet.Id);
        principal.LinkWalletOwnership(ownership, (_, _, _) => Result.Success<bool, Error>(false), TimeProvider.System);

        return (principal, wallet);
    }

    /// <summary>
    /// Creates scenario for Test 8: RESOLVE_wallet_verified_wins
    /// Principal has verified+signing wallet - should resolve via wallet lookup.
    /// </summary>
    public static (AxonPrincipal principal, Wallet wallet) CreateWalletVerifiedScenario()
    {
        var principal = CreatePlainPrincipal(); // No credentials
        var wallet = CreateW1Main();

        // Link wallet with verified signing ownership
        var ownership = CreateVerifiedSigningOwnership(principal.Id, wallet.Id);
        principal.LinkWalletOwnership(ownership, (_, _, _) => Result.Success<bool, Error>(false), TimeProvider.System);

        return (principal, wallet);
    }

    /// <summary>
    /// Creates scenario for Test 9: RESOLVE_single_active_owner
    /// Single watch-only ownership should resolve to that principal.
    /// </summary>
    public static (AxonPrincipal principal, Wallet wallet) CreateSingleActiveOwnerScenario()
    {
        var principal = CreatePlainPrincipal(); // No credentials
        var wallet = CreateW1Main();

        // Link wallet with watch-only ownership (only active owner)
        var ownership = CreateVerifiedWatchOnlyOwnership(principal.Id, wallet.Id);
        principal.LinkWalletOwnership(ownership, (_, _, _) => Result.Success<bool, Error>(false), TimeProvider.System);

        return (principal, wallet);
    }

    /// <summary>
    /// Creates scenario for Test 10: RESOLVE_ambiguity_tie_break
    /// Multiple non-verified ownerships require authority ranking and tie-breaking.
    /// </summary>
    public static (AxonPrincipal principalA, AxonPrincipal principalB, Wallet wallet) CreateAmbiguityScenario()
    {
        var principalA = CreatePlainPrincipal(); // Earlier principal (tie-breaker)
        var principalB = CreatePlainPrincipal(); // Later principal
        var wallet = CreateW1Main();

        // Principal A has pending signing (higher authority than watch-only)
        var ownershipA = CreatePendingSigningOwnership(principalA.Id, wallet.Id);
        principalA.LinkWalletOwnership(ownershipA, (_, _, _) => Result.Success<bool, Error>(false), TimeProvider.System);

        // Principal B has verified watch-only (lower authority)
        var ownershipB = CreateVerifiedWatchOnlyOwnership(principalB.Id, wallet.Id);
        principalB.LinkWalletOwnership(ownershipB, (_, _, _) => Result.Success<bool, Error>(false), TimeProvider.System);

        return (principalA, principalB, wallet);
    }

    /// <summary>
    /// Creates scenario for Test 11: RESOLVE_no_match_creates
    /// No existing matches - should create new principal idempotently.
    /// </summary>
    public static (string unknownSubject, string unknownWalletAddress) CreateNoMatchScenario()
    {
        return ("unknown_subject_12345", "UnknownWalletAddress9876543210123456789012345");
    }

    #endregion

    #region Defaults Behavior Test Scenarios (Section D)

    /// <summary>
    /// Creates scenario for Test 15: DEFAULT_auto_seed_on_first_verified
    /// First verified+signing wallet should auto-create default.
    /// </summary>
    public static (AxonPrincipal principal, Wallet wallet) CreateAutoSeedDefaultScenario()
    {
        var principal = CreatePlainPrincipal(); // No existing defaults
        var wallet = CreateW1Main();

        // This will be the first verified+signing wallet
        var ownership = CreateVerifiedSigningOwnership(principal.Id, wallet.Id);
        principal.LinkWalletOwnership(ownership, (_, _, _) => Result.Success<bool, Error>(false), TimeProvider.System);

        return (principal, wallet);
    }

    /// <summary>
    /// Creates scenario for Test 16: DEFAULT_clear_on_revoke
    /// Revoking defaulted ownership should clear the default.
    /// </summary>
    public static (AxonPrincipal principal, Wallet wallet, PrincipalChainDefault chainDefault) CreateClearOnRevokeScenario()
    {
        var principal = CreatePlainPrincipal();
        var wallet = CreateW1Main();

        // Create verified signing ownership
        var ownership = CreateVerifiedSigningOwnership(principal.Id, wallet.Id);
        principal.LinkWalletOwnership(ownership, (_, _, _) => Result.Success<bool, Error>(false), TimeProvider.System);

        // Create default for this wallet
        var chainDefault = CreateMainnetSolanaDefault(principal.Id, wallet.Id);
        principal.ApplyChainDefault(SolanaMainnetChain, wallet.Id, TimeProvider.System);

        return (principal, wallet, chainDefault);
    }

    /// <summary>
    /// Creates multi-wallet scenario for defaults testing.
    /// </summary>
    public static (AxonPrincipal principal, Wallet mainnetWallet, Wallet devnetWallet) CreateMultiChainDefaultScenario()
    {
        var principal = CreatePlainPrincipal();
        var mainnetWallet = CreateW1Main();
        var devnetWallet = CreateW1Dev();

        // Link both wallets with verified signing
        var mainnetOwnership = CreateVerifiedSigningOwnership(principal.Id, mainnetWallet.Id);
        var devnetOwnership = CreateVerifiedSigningOwnership(principal.Id, devnetWallet.Id);

        principal.LinkWalletOwnership(mainnetOwnership, (_, _, _) => Result.Success<bool, Error>(false), TimeProvider.System);
        principal.LinkWalletOwnership(devnetOwnership, (_, _, _) => Result.Success<bool, Error>(false), TimeProvider.System);

        return (principal, mainnetWallet, devnetWallet);
    }

    #endregion

    #region Wallet Signature Test Vectors

    /// <summary>
    /// Test vectors for wallet signature validation.
    /// Contains signed messages with TTL for resolution testing.
    /// </summary>
    public static class SignatureTestVectors
    {
        /// <summary>
        /// Valid signed message for W1 with version 1 TTL.
        /// </summary>
        public const string Sig_W1_Msg_V1 = "signed_message_w1_v1_valid_ttl";

        /// <summary>
        /// Valid signed message for W1 with version 2 TTL.
        /// </summary>
        public const string Sig_W1_Msg_V2 = "signed_message_w1_v2_valid_ttl";

        /// <summary>
        /// Expired signed message for W1 (TTL exceeded).
        /// </summary>
        public const string Sig_W1_Expired = "signed_message_w1_expired_ttl";

        /// <summary>
        /// Valid message content for testing.
        /// </summary>
        public const string ValidMessageContent = "I want to connect my wallet to Axon";

        /// <summary>
        /// Test timestamp for deterministic TTL testing.
        /// </summary>
        public static readonly DateTime TestTimestamp = new(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Valid TTL (issued 5 minutes ago, expires in 55 minutes).
        /// </summary>
        public static readonly DateTime ValidIssuedAt = TestTimestamp.AddMinutes(-5);

        /// <summary>
        /// Expired TTL (issued 2 hours ago).
        /// </summary>
        public static readonly DateTime ExpiredIssuedAt = TestTimestamp.AddHours(-2);
    }

    #endregion
}