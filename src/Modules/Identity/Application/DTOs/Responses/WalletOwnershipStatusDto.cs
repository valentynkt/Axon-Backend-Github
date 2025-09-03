namespace Axon.Modules.Identity.Application.DTOs.Responses;

/// <summary>
/// Response DTO for wallet ownership queries.
/// Either contains ownership details or indicates the wallet is not owned by caller.
/// </summary>
public sealed record WalletOwnershipStatusDto(
    string Status,
    WalletOwnershipDto? Ownership = null
)
{
    /// <summary>
    /// Creates a response indicating the wallet is owned by the caller.
    /// </summary>
    public static WalletOwnershipStatusDto Owned(WalletOwnershipDto ownership) =>
        new("OWNED", ownership);

    /// <summary>
    /// Creates a response indicating the wallet is not owned by the caller.
    /// </summary>
    public static WalletOwnershipStatusDto NotOwned() =>
        new("NOT_OWNED");
};