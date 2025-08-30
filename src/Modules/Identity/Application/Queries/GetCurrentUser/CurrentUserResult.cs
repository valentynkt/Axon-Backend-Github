namespace Axon.Modules.Identity.Application.Queries.GetCurrentUser;

/// <summary>
/// Result containing current user information
/// </summary>
public record CurrentUserResult
{
    public required UserProfile User { get; init; }
    public required IReadOnlyList<WalletData> Wallets { get; init; }
    public required DateTime SyncedAt { get; init; }
    public required string SyncStatus { get; init; }
}

public record UserProfile
{
    public required string Id { get; init; }
    public required Guid DynamicUserId { get; init; }
    public required string Email { get; init; }
    public string? DisplayName { get; init; }
    public string? Username { get; init; }
    public DateTime? FirstVisit { get; init; }
    public DateTime? LastVisit { get; init; }
    public required IReadOnlyDictionary<string, object> Metadata { get; init; }
}

public record WalletData
{
    public required Guid Id { get; init; }
    public required string Address { get; init; }
    public required string Chain { get; init; }
    public required string Provider { get; init; }
    public string? WalletName { get; init; }
    public DateTime? ConnectedAt { get; init; }
}