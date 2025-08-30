using Axon.Api.Endpoints.V1.Auth;

namespace Axon.Api.Contracts.V1.Identity.Authentication;

/// <summary>
/// Response containing current authenticated user's profile and wallet information
/// </summary>
public record CurrentUserResponseDto
{
    /// <summary>
    /// Gets the detailed user profile information
    /// </summary>
    public required UserProfileDto User { get; init; }
    
    /// <summary>
    /// Gets the list of connected wallets for the user
    /// </summary>
    public required IReadOnlyList<WalletInfo> Wallets { get; init; }
    
    /// <summary>
    /// Gets the timestamp when data was last synchronized
    /// </summary>
    public required DateTime SyncedAt { get; init; }
    
    /// <summary>
    /// Gets the current synchronization status
    /// </summary>
    public required string SyncStatus { get; init; }
}