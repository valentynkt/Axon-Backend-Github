namespace Axon.Modules.Identity.Application.DTOs.Responses;

/// <summary>
/// Lightweight DTO for wallet search results.
/// Contains only essential fields for UI autocomplete/lookup scenarios.
/// No tags or metadata to ensure fast queries and minimal data exposure.
/// </summary>
public sealed record WalletSearchResultDto(
    string WalletId,
    string ChainId,
    string Address,
    DateTimeOffset LastSeenAt
);