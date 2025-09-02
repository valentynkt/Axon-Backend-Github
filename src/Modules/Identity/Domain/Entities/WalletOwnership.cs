using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Domain.Entities;

/// <summary>
/// Wallet ownership link entity proving a principal controls a wallet.
/// Child entity of AxonPrincipal with global unique constraint on verified wallets.
/// </summary>
public sealed class WalletOwnership : AuditableDeletableEntity<WalletOwnershipId>
{
    public AxonId AxonId { get; private set; }
    public WalletId WalletId { get; private set; }
    public ChainId ChainId { get; private set; }
    public ProofType ProofType { get; private set; }
    public AccessMode AccessMode { get; private set; }
    public OwnershipState State { get; private set; }
    public DateTimeOffset FirstLinkedAt { get; private set; }
    public DateTimeOffset? LastVerifiedAt { get; private set; }
    public string? Label { get; private set; }

    // EF Core parameterless constructor
    private WalletOwnership() : base() { }

    private WalletOwnership(
        WalletOwnershipId id,
        AxonId axonId,
        WalletId walletId,
        ChainId chainId,
        ProofType proofType,
        AccessMode accessMode,
        OwnershipState state,
        DateTimeOffset firstLinkedAt,
        DateTimeOffset? lastVerifiedAt = null,
        string? label = null) : base(id)
    {
        AxonId = axonId;
        WalletId = walletId;
        ChainId = chainId;
        ProofType = proofType;
        AccessMode = accessMode;
        State = state;
        FirstLinkedAt = firstLinkedAt;
        LastVerifiedAt = lastVerifiedAt;
        Label = label;
    }

    internal static Result<WalletOwnership, Error> Create(
        AxonId axonId,
        WalletId walletId,
        ChainId chainId,
        ProofType proofType,
        AccessMode? accessMode = null,
        OwnershipState? state = null,
        DateTimeOffset? firstLinkedAt = null,
        string? label = null)
    {
        if (walletId == WalletId.Empty)
            return Result.Failure<WalletOwnership, Error>(
                Error.Validation("Wallet ID cannot be empty.", "IDENTITY.WALLET.ID.INVALID"));

        if (string.IsNullOrWhiteSpace(chainId.Value))
            return Result.Failure<WalletOwnership, Error>(
                Error.Validation("Chain ID cannot be empty.", "IDENTITY.WALLET.CHAIN.INVALID"));

        var id = new WalletOwnershipId(Guid.CreateVersion7());
        var effectiveAccessMode = accessMode ?? AccessMode.Default;
        var effectiveState = state ?? OwnershipState.Default;
        var effectiveFirstLinked = firstLinkedAt ?? DateTimeOffset.UtcNow;

        DateTimeOffset? lastVerified = null;
        if (effectiveState.IsVerified)
        {
            lastVerified = effectiveFirstLinked;
        }

        var ownership = new WalletOwnership(
            id, axonId, walletId, chainId, proofType, effectiveAccessMode, 
            effectiveState, effectiveFirstLinked, lastVerified, label);

        return Result.Success<WalletOwnership, Error>(ownership);
    }

    internal Result<Unit, Error> Verify(DateTimeOffset verifiedAt)
    {
        if (IsDeleted)
            return Result.Failure<Unit, Error>(
                Error.BusinessRule("Cannot verify deleted wallet ownership.", "IDENTITY.WALLET.OWNERSHIP.DELETED"));

        if (State.IsRevoked)
            return Result.Failure<Unit, Error>(
                Error.BusinessRule("Cannot verify revoked wallet ownership.", "IDENTITY.WALLET.OWNERSHIP.REVOKED"));

        State = OwnershipState.Verified;
        LastVerifiedAt = verifiedAt;

        return Result.Success<Unit, Error>(Unit.Value);
    }

    internal Result<Unit, Error> Revoke()
    {
        if (IsDeleted)
            return Result.Failure<Unit, Error>(
                Error.BusinessRule("Cannot revoke deleted wallet ownership.", "IDENTITY.WALLET.OWNERSHIP.DELETED"));

        if (State.IsRevoked)
            return Result.Success<Unit, Error>(Unit.Value); // Already revoked

        State = OwnershipState.Revoked;
        // Note: LastVerifiedAt remains as is for audit purposes

        return Result.Success<Unit, Error>(Unit.Value);
    }

    internal Result<Unit, Error> UpdateLabel(string? newLabel)
    {
        if (IsDeleted)
            return Result.Failure<Unit, Error>(
                Error.BusinessRule("Cannot update label on deleted wallet ownership.", "IDENTITY.WALLET.OWNERSHIP.DELETED"));

        // Trim and normalize
        var normalizedLabel = string.IsNullOrWhiteSpace(newLabel) ? null : newLabel.Trim();
        
        if (normalizedLabel?.Length > 100) // Reasonable limit
            return Result.Failure<Unit, Error>(
                Error.Validation("Wallet label cannot exceed 100 characters.", "IDENTITY.WALLET.LABEL.TOO_LONG"));

        Label = normalizedLabel;
        return Result.Success<Unit, Error>(Unit.Value);
    }

    internal Result<Unit, Error> ChangeAccessMode(AccessMode newAccessMode)
    {
        if (IsDeleted)
            return Result.Failure<Unit, Error>(
                Error.BusinessRule("Cannot change access mode on deleted wallet ownership.", "IDENTITY.WALLET.OWNERSHIP.DELETED"));

        if (State.IsRevoked)
            return Result.Failure<Unit, Error>(
                Error.BusinessRule("Cannot change access mode on revoked wallet ownership.", "IDENTITY.WALLET.OWNERSHIP.REVOKED"));

        AccessMode = newAccessMode;
        return Result.Success<Unit, Error>(Unit.Value);
    }

    public bool BelongsTo(AxonId principalId) => AxonId == principalId;

    public bool IsForWallet(WalletId walletId) => WalletId == walletId;

    public bool IsForChain(ChainId chainId) => ChainId == chainId;

    public bool IsActive => !IsDeleted && State.IsVerified;

    public bool IsVerifiedSigning => IsActive && AccessMode.IsSigning;

    /// <summary>
    /// Checks if this ownership represents a verified signing relationship
    /// that would conflict with the global single-owner rule.
    /// </summary>
    public bool IsConflictingOwnership => State.IsVerified && AccessMode.IsSigning && !IsDeleted;
}