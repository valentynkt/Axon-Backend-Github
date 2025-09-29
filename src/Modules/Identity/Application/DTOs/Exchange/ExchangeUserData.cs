namespace Axon.Modules.Identity.Application.DTOs.Exchange;

/// <summary>
/// Simplified user data extracted from validated Dynamic JWT for exchange operations within Application layer.
/// This DTO bridges the gap between Infrastructure (Dynamic service) and Application (command processing).
/// Contains only the essential data needed for principal and wallet operations.
/// </summary>
/// <param name="AxonUserId">The Dynamic user's unique identifier (subject claim)</param>
/// <param name="Email">The user's email address, if provided in the JWT</param>
/// <param name="DynamicEnvironmentId">The Dynamic.xyz tenant environment ID (NOT blockchain network environment)</param>
/// <param name="Wallets">List of connected wallets from the JWT claims</param>
/// <param name="FirstVisitUtc">Optional timestamp of user's first visit to the application</param>
/// <param name="LastVisitUtc">Optional timestamp of user's most recent visit</param>
/// <param name="IsNewUser">Whether this is the user's first authentication with Dynamic</param>
/// <param name="AdditionalMetadata">Optional metadata like session keys or verified credentials</param>
public sealed record ExchangeUserData(
    string AxonUserId,
    string Email,
    string DynamicEnvironmentId,
    List<ExchangeWalletData> Wallets,
    DateTimeOffset? FirstVisitUtc = null,
    DateTimeOffset? LastVisitUtc = null,
    bool IsNewUser = false,
    Dictionary<string, object>? AdditionalMetadata = null
);

/// <summary>
/// Simplified wallet data extracted from Dynamic JWT claims for exchange operations within Application layer.
/// Contains the essential information needed for wallet activity tracking and ownership linking.
/// </summary>
/// <param name="Address">The wallet's blockchain address in its native format</param>
/// <param name="Chain">The blockchain network identifier (normalized to Axon format)</param>
/// <param name="WalletName">Optional display name of the wallet (e.g., "MetaMask Account 1")</param>
/// <param name="Provider">Optional wallet provider name (e.g., "metamask", "walletconnect")</param>
/// <param name="ConnectedAtUtc">Optional timestamp when the wallet was connected to Dynamic</param>
public sealed record ExchangeWalletData(
    string Address,
    string Chain,
    string? WalletName = null,
    string? Provider = null,
    DateTimeOffset? ConnectedAtUtc = null
);