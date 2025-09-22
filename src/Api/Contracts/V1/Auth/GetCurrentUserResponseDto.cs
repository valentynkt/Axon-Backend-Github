namespace Axon.Api.Contracts.V1.Auth;

/// <summary>
/// Normalized snapshot of the authenticated principal suitable for UI/state hydration.
/// </summary>
public sealed record GetCurrentUserResponseDto(
    string AxonUserId,
    bool IsAuthenticated,
    string Environment,             // e.g. "mainnet" | "devnet" | "testnet"
    string RiskPosture,             // e.g. "balanced"
    ProviderLinkDto[] Providers,    // linked auth identities
    WalletDto[] Wallets,            // known wallets
    ChainDefaultDto[] ChainDefaults // per-chain default wallet mapping
);

public sealed record ProviderLinkDto(
    string Provider,                // "dynamic" | "siws" | "oidc" (future)
    string Issuer,                  // provider issuer (if applicable)
    string Subject                  // stable subject/user id from provider
);

public sealed record WalletDto(
    string WalletId,                // internal id (ULID as string)
    string ChainId,                 // "solana" | "ethereum" | etc.
    string Address,                 // normalized address
    string OwnershipStatus,         // "verified" | "pending" | "revoked"
    string AccessMode,              // "signing" | "watch_only"
    string? Provider,               // optional: wallet provider label (if known)
    string? DisplayName             // optional: display name (if set)
);

public sealed record ChainDefaultDto(
    string ChainId,
    string WalletId
);