namespace Axon.Modules.Identity.Application.DTOs.Responses;

/// <summary>
/// Response DTO for GET /auth/me endpoint containing user profile and wallet information.
/// Designed for efficient client-side caching with ETag support.
/// </summary>
public sealed record CurrentUserResult(
    UserProfile Profile,
    IReadOnlyList<WalletInfo> Wallets,
    IReadOnlyDictionary<string, string> ChainDefaults,
    string ETag);

/// <summary>
/// User profile information for the current authenticated user.
/// </summary>
public sealed record UserProfile(
    string AxonUserId,
    string Subject,
    string RiskTier);

/// <summary>
/// Wallet information for wallets owned by the current user.
/// Only includes verified wallets for security.
/// </summary>
public sealed record WalletInfo(
    string WalletId,
    string ChainId,
    string Address,
    string AccessMode,
    bool IsVerified);