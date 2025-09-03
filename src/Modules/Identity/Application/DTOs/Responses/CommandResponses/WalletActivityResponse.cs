namespace Axon.Modules.Identity.Application.DTOs.Responses.CommandResponses;

/// <summary>
/// Status enumeration for UpsertWalletActivity operation.
/// </summary>
public enum WalletActivityStatus
{
    RegisteredAndUpdated,
    Updated,
    NoOp
}

/// <summary>
/// Represents changes made to wallet tags.
/// </summary>
public sealed record TagDiff(
    string[] Added,
    string[] Removed
);

/// <summary>
/// Response for UpsertWalletActivity command.
/// </summary>
public sealed record WalletActivityResponse(
    WalletDto Wallet,
    string[]? KeysChanged,
    TagDiff TagDiff,
    WalletActivityStatus Status
);