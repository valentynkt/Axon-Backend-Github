namespace Axon.Api.Contracts.V1.Identity.Authentication;

/// <summary>
/// Response from Dynamic JWT exchange with summary metrics
/// </summary>
public sealed record ExchangeDynamicTokenResponse(
    /// <summary>
    /// The Axon principal ID for the authenticated user
    /// </summary>
    string AxonId,
    
    /// <summary>
    /// Whether this principal was created during this exchange
    /// </summary>
    bool Created,
    
    /// <summary>
    /// Total number of wallets processed from the JWT
    /// </summary>
    int WalletsProcessed,
    
    /// <summary>
    /// Number of wallets successfully linked to the principal
    /// </summary>
    int WalletsLinked,
    
    /// <summary>
    /// Number of wallets set as chain defaults during this exchange
    /// </summary>
    int DefaultsApplied,
    
    /// <summary>
    /// Number of wallets skipped due to invalid chain/address formats
    /// </summary>
    int Skipped,
    
    /// <summary>
    /// Number of wallets that couldn't be linked due to ownership conflicts
    /// </summary>
    int Conflicts
);