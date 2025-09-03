namespace Axon.Modules.Identity.Application.Queries.GetCurrentUser;

/// <summary>
/// Production-ready result containing current user information from domain model.
/// No stubs, no fabricated data - only verified domain-backed information.
/// Uses primitive types only for JSON serialization safety.
/// </summary>
public record CurrentUserResult
{
    public required PrincipalProfileDto Profile { get; init; }
    public required IReadOnlyList<OwnedWalletDto> OwnedWallets { get; init; }
    public required IReadOnlyDictionary<string, string> DefaultPerChain { get; init; }
    public required DateTimeOffset SyncedAt { get; init; }
}