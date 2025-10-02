using BuildingBlocks.Web.Contracts;

namespace Axon.Api.Contracts.V1.Auth;

/// <summary>
/// Response for GET /api/v1/auth/me endpoint per High-Level Flow Architecture
/// Returns canonical merged view of current user with ETag support
/// </summary>
public sealed record GetCurrentUserResponseDto(
    UserProfileDto Profile,
    WalletInfoDto[] Wallets,
    string ETag
) : IHaveETag;

/// <summary>
/// User profile information
/// </summary>
public sealed record UserProfileDto(
    string AxonId,
    string RiskTier                 // e.g. "balanced"
);

/// <summary>
/// Simplified wallet information per architecture specification
/// </summary>
public sealed record WalletInfoDto(
    string Chain,                   // "solana", "ethereum", etc.
    string Address,                 // normalized wallet address
    string State,                   // "verified", "pending", "revoked"
    string Access,                  // "signing", "watch_only"
    bool IsDefault                  // true if this is the default wallet for this chain
);