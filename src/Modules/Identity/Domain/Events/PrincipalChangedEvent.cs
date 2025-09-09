namespace Axon.Modules.Identity.Domain.Events;

/// <summary>
/// Consolidated event for all principal-related changes.
/// Replaces: PrincipalCreated, PrincipalSoftDeleted, PrincipalRestored, 
/// ProfileLanguageChanged, ProfileRiskTierChanged, DefaultWalletChanged, and other profile events.
/// </summary>
public sealed record PrincipalChangedEvent(
    string AxonId,
    string ChangeType, // "created", "deleted", "restored", "risk_tier_changed", "language_changed", "default_wallet_set", "default_wallet_removed", etc.
    string? PrincipalType = null,
    string? EmailHash = null,
    string? RiskTier = null, 
    string? Language = null,
    string? Reason = null,
    string? ChainId = null,
    string? NewWalletId = null,
    string? PreviousWalletId = null,
    DateTime? EventOccurredAt = null
) : DomainEvent(EventOccurredAt ?? DateTime.UtcNow);