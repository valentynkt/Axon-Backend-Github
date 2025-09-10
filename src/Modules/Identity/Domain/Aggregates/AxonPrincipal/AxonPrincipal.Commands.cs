using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.Errors;
using Axon.Modules.Identity.Domain.Events;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;

/// <summary>
/// Command methods for AxonPrincipal aggregate.
/// </summary>
public sealed partial class AxonPrincipal
{
    private readonly Dictionary<string, WalletId> _chainDefaults = new();

    /// <summary>
    /// Updates the risk tier with no-op guard.
    /// </summary>
    public Result<Unit, Error> UpdateRiskTier(RiskTier riskTier)
    {
        // No-op guard: if same value, don't update
        if (RiskTier == riskTier)
            return Result.Success<Unit, Error>(Unit.Value);

        // Service principals can only have Low (Conservative) risk tier
        if (Type == PrincipalType.Service && riskTier != RiskTier.Low)
            return Result.Failure<Unit, Error>(IdentityDomainErrors.Profile.ServicePrincipalRiskConstraint());

        var oldTier = RiskTier;
        RiskTier = riskTier;

        // Raise domain event only when actual change occurs
        RaiseDomainEvent(new PrincipalChangedEvent(
            Id,
            nameof(RiskTier),
            oldTier.ToString(),
            riskTier.ToString()
        ));

        return Result.Success<Unit, Error>(Unit.Value);
    }

    /// <summary>
    /// Links a wallet ownership to the principal with idempotency and conflict detection.
    /// </summary>
    public Result<Unit, Error> LinkWalletOwnership(
        WalletOwnership ownership,
        Func<WalletId, AccessMode, OwnershipStatus, Result<bool, Error>> checkExistingOwnershipFunc)
    {
        ArgumentNullException.ThrowIfNull(ownership);
        ArgumentNullException.ThrowIfNull(checkExistingOwnershipFunc);
        
        // Check if we already have this exact ownership (idempotency)
        var existingOwnership = _walletOwnerships.FirstOrDefault(o => 
            o.WalletId == ownership.WalletId &&
            o.AccessMode == ownership.AccessMode &&
            o.Status == ownership.Status);

        if (existingOwnership != null)
            return Result.Success<Unit, Error>(Unit.Value); // Idempotent - no change needed

        // Check for conflicting ownership (only one verified+signing owner per wallet)
        if (ownership.IsVerifiedSigning)
        {
            var conflictCheck = checkExistingOwnershipFunc(
                ownership.WalletId,
                AccessMode.Signing,
                OwnershipStatus.Verified
            );

            if (conflictCheck.IsFailure)
                return Result.Failure<Unit, Error>(conflictCheck.Error);

            if (conflictCheck.Value) // Another principal owns this wallet as verified+signing
                return Result.Failure<Unit, Error>(IdentityDomainErrors.Wallet.AlreadyOwned());
        }

        // Check max wallets constraint
        if (_walletOwnerships.Count >= 10)
            return Result.Failure<Unit, Error>(IdentityDomainErrors.Wallet.MaxExceeded());

        _walletOwnerships.Add(ownership);

        // Raise domain event
        RaiseDomainEvent(new OwnershipChangedEvent(
            Id,
            ownership.WalletId,
            "linked",
            ownership.AccessMode.ToString(),
            ownership.Status.ToString()
        ));

        return Result.Success<Unit, Error>(Unit.Value);
    }

    /// <summary>
    /// Adds a credential to the principal with uniqueness validation.
    /// </summary>
    public Result<Unit, Error> AddCredential(
        IdentityCredential credential,
        Func<string, string, string, Result<bool, Error>> checkCredentialUniquenessFunc)
    {
        // Check if we already have this credential (idempotency)
        var existingCredential = _credentials.FirstOrDefault(c =>
            c.Provider == credential.Provider &&
            c.Issuer == credential.Issuer &&
            c.Subject == credential.Subject);

        if (existingCredential != null)
        {
            // Update last seen if newer
            existingCredential.UpdateLastSeen(credential.LastSeenAt);
            return Result.Success<Unit, Error>(Unit.Value);
        }

        // Check global uniqueness
        var uniquenessCheck = checkCredentialUniquenessFunc(
            credential.Provider,
            credential.Issuer,
            credential.Subject
        );

        if (uniquenessCheck.IsFailure)
            return Result.Failure<Unit, Error>(uniquenessCheck.Error);

        if (uniquenessCheck.Value) // Credential exists for another principal
            return Result.Failure<Unit, Error>(IdentityDomainErrors.Credential.BelongsToOther());

        _credentials.Add(credential);

        // Raise domain event
        RaiseDomainEvent(new CredentialChangedEvent(
            Id,
            credential.Id,
            "added",
            credential.Provider
        ));

        return Result.Success<Unit, Error>(Unit.Value);
    }

    /// <summary>
    /// Applies a chain default with verified-first enforcement.
    /// </summary>
    public Result<Unit, Error> ApplyChainDefault(string chainId, WalletId walletId)
    {
        ArgumentNullException.ThrowIfNull(chainId);
        
        // Check if the wallet is owned by this principal, prioritize verified signing ownership
        var ownership = _walletOwnerships
            .Where(o => o.WalletId == walletId)
            .OrderByDescending(o => o.IsVerifiedSigning)
            .ThenByDescending(o => o.Status == OwnershipStatus.Verified)
            .FirstOrDefault();
            
        if (ownership == null)
            return Result.Failure<Unit, Error>(IdentityDomainErrors.Wallet.NotOwnedByPrincipal());

        // Only verified+signing wallets can be defaults
        if (!ownership.IsVerifiedSigning)
            return Result.Failure<Unit, Error>(IdentityDomainErrors.Wallet.WatchOnlyNotAllowedAsDefault());

        // No-op guard: if same default, don't update
        if (_chainDefaults.TryGetValue(chainId, out var currentDefault) && currentDefault == walletId)
            return Result.Success<Unit, Error>(Unit.Value);

        var oldDefault = _chainDefaults.TryGetValue(chainId, out var existing) ? existing : (WalletId?)null;
        _chainDefaults[chainId] = walletId;

        // Raise domain event only when actual change occurs
        RaiseDomainEvent(new PrincipalChangedEvent(
            Id,
            $"ChainDefault.{chainId}",
            oldDefault?.ToString() ?? "none",
            walletId.ToString()
        ));

        return Result.Success<Unit, Error>(Unit.Value);
    }

    /// <summary>
    /// Removes a wallet ownership from the principal.
    /// </summary>
    public Result<Unit, Error> RemoveWalletOwnership(WalletId walletId)
    {
        var ownership = _walletOwnerships.FirstOrDefault(o => o.WalletId == walletId);
        if (ownership == null)
            return Result.Failure<Unit, Error>(IdentityDomainErrors.Wallet.NotOwnedByPrincipal());

        _walletOwnerships.Remove(ownership);

        // Remove from defaults if it was a default
        var defaultChains = _chainDefaults.Where(kv => kv.Value == walletId).Select(kv => kv.Key).ToList();
        foreach (var chain in defaultChains)
        {
            _chainDefaults.Remove(chain);
        }

        // Raise domain event
        RaiseDomainEvent(new OwnershipChangedEvent(
            Id,
            walletId,
            "removed",
            ownership.AccessMode.ToString(),
            ownership.Status.ToString()
        ));

        return Result.Success<Unit, Error>(Unit.Value);
    }

    /// <summary>
    /// Maps risk tier from wire format to domain enum.
    /// Wire: "low" | "medium" | "high"
    /// Product: "conservative" | "balanced" | "aggressive"
    /// </summary>
    public static Result<RiskTier, Error> MapRiskTierFromWire(string wireValue)
    {
        return wireValue?.ToLowerInvariant() switch
        {
            "low" or "conservative" => Result.Success<RiskTier, Error>(RiskTier.Low),
            "medium" or "balanced" => Result.Success<RiskTier, Error>(RiskTier.Medium),
            "high" or "aggressive" => Result.Success<RiskTier, Error>(RiskTier.High),
            _ => Result.Failure<RiskTier, Error>(IdentityDomainErrors.Profile.InvalidRiskTier())
        };
    }

    /// <summary>
    /// Maps risk tier from domain enum to wire format.
    /// </summary>
    public static string MapRiskTierToWire(RiskTier riskTier, bool useProductTerms = false)
    {
        if (useProductTerms)
        {
            return riskTier switch
            {
                RiskTier.Low => "conservative",
                RiskTier.Medium => "balanced",
                RiskTier.High => "aggressive",
                _ => "conservative"
            };
        }

        return riskTier switch
        {
            RiskTier.Low => "low",
            RiskTier.Medium => "medium",
            RiskTier.High => "high",
            _ => "low"
        };
    }
}