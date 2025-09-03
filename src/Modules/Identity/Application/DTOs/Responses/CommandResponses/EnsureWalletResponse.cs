namespace Axon.Modules.Identity.Application.DTOs.Responses.CommandResponses;

/// <summary>
/// Status enumeration for EnsureWalletLinked operation.
/// </summary>
public enum EnsureWalletStatus
{
    RegisteredAndLinked,
    Linked,
    AlreadyLinked,
    Updated,
    ConflictOwnedByOther
}

/// <summary>
/// Response for EnsureWalletLinked command.
/// </summary>
public sealed record EnsureWalletResponse(
    WalletDto Wallet,
    WalletOwnershipDto? Ownership,
    bool? AppliedDefault,
    string? DefaultNotAppliedReason,
    EnsureWalletStatus Status
);