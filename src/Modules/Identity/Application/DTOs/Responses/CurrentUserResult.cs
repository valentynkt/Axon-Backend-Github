namespace Axon.Modules.Identity.Application.DTOs.Responses;

/// <summary>
/// Response DTO for GET /auth/me endpoint per High-Level Flow Architecture.
/// Contains canonical merged view of current user.
/// </summary>
public sealed record CurrentUserResult(
    UserProfile Profile,
    IReadOnlyList<WalletInfo> Wallets);

/// <summary>
/// User profile information aligned with architecture specification.
/// </summary>
public sealed record UserProfile(
    string AxonId,
    string RiskTier);

/// <summary>
/// Wallet information per architecture specification.
/// Includes default wallet information and simplified state model.
/// </summary>
public sealed record WalletInfo(
    string Chain,
    string Address,
    string State,               // "verified", "pending", "revoked"
    string Access,              // "signing", "watch_only"
    bool IsDefault);