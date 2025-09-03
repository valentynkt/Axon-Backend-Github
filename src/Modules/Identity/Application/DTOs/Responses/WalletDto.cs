namespace Axon.Modules.Identity.Application.DTOs.Responses;

/// <summary>
/// Data transfer object representing a wallet with contract-primitive shapes.
/// Tags may be redacted based on authorization context.
/// </summary>
public sealed record WalletDto(
    string WalletId,
    string ChainId,
    string Address,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt,
    string[] Tags,
    int MetadataSizeBytes,
    bool IsActive
);