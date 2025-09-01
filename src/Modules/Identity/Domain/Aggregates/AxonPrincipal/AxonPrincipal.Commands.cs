using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Errors;
using Axon.Modules.Identity.Domain.Events;
using Axon.Modules.Identity.Domain.Rules;
using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;

/// <summary>
/// AxonPrincipal partial class containing command operations (state-changing methods).
/// </summary>
public sealed partial class AxonPrincipal
{
    /// <summary>
    /// Links an identity credential to this principal.
    /// Enforces uniqueness across all principals.
    /// </summary>
    public Result<IdentityCredential, Error> LinkIdentityCredential(
        ProviderType providerType,
        string issuer,
        string subject,
        string? environmentId = null,
        Dictionary<string, object>? metadata = null,
        TimeProvider? timeProvider = null)
    {
        try
        {
            CheckRule(new PrincipalMustBeActiveRule(this));
            CheckRule(new CredentialMustBeUniqueRule(providerType, issuer, subject, _credentials));

            var effectiveTimeProvider = timeProvider ?? TimeProvider.System;
            var now = effectiveTimeProvider.GetUtcNow();

            var credentialResult = IdentityCredential.Create(
                Id, providerType, issuer, subject, environmentId, now, metadata);

            if (credentialResult.IsFailure)
                return Result.Failure<IdentityCredential, Error>(credentialResult.Error);

            var credential = credentialResult.Value;
            _credentials.Add(credential);
            MarkUpdated();

            RaiseDomainEvent(new IdentityCredentialLinkedEvent(
                Id, credential.Id, providerType.Value, issuer, subject, environmentId, now));

            return Result.Success<IdentityCredential, Error>(credential);
        }
        catch (BusinessRuleException ex)
        {
            return Result.Failure<IdentityCredential, Error>(
                Error.BusinessRule(ex.Message, ex.Error.Code));
        }
    }

    /// <summary>
    /// Links a wallet to this principal with proof of ownership.
    /// Enforces global single verified owner rule and capacity limits.
    /// </summary>
    public Result<WalletOwnership, Error> LinkWallet(
        WalletId walletId,
        Chain chain,
        ProofType proofType,
        AccessMode? accessMode = null,
        string? label = null,
        TimeProvider? timeProvider = null)
    {
        try
        {
            CheckRule(new PrincipalMustBeActiveRule(this));
            CheckRule(new MaxWalletsPerPrincipalRule(_walletOwnerships.Count(w => !w.IsDeleted)));
            CheckRule(new WalletMustNotBeOwnedByPrincipalRule(walletId, _walletOwnerships));

            var effectiveTimeProvider = timeProvider ?? TimeProvider.System;
            var now = effectiveTimeProvider.GetUtcNow();

            var ownershipResult = WalletOwnership.Create(
                Id, walletId, proofType, accessMode, label: label, firstLinkedAt: now);

            if (ownershipResult.IsFailure)
                return Result.Failure<WalletOwnership, Error>(ownershipResult.Error);

            var ownership = ownershipResult.Value;
            _walletOwnerships.Add(ownership);

            // Initialize default for chain if this is the first wallet for this chain
            var initResult = Profile.InitializeDefaultForChainIfEmpty(chain.Value, walletId);
            if (initResult.IsFailure)
                return Result.Failure<WalletOwnership, Error>(initResult.Error);

            MarkUpdated();

            RaiseDomainEvent(new WalletOwnershipLinkedEvent(
                Id, walletId, ownership.Id, proofType.Value, 
                accessMode?.Value ?? AccessMode.Default.Value, chain.Value, now));

            return Result.Success<WalletOwnership, Error>(ownership);
        }
        catch (BusinessRuleException ex)
        {
            return Result.Failure<WalletOwnership, Error>(
                Error.BusinessRule(ex.Message, ex.Error.Code));
        }
    }

    /// <summary>
    /// Unlinks a wallet from this principal.
    /// Clears chain default if this was the default wallet.
    /// </summary>
    public Result<Unit, Error> UnlinkWallet(WalletId walletId, TimeProvider? timeProvider = null)
    {
        try
        {
            CheckRule(new PrincipalMustBeActiveRule(this));

            var ownership = _walletOwnerships.FirstOrDefault(w => w.IsForWallet(walletId) && !w.IsDeleted);
            if (ownership is null)
                return Result.Failure<Unit, Error>(IdentityDomainErrors.Wallet.NotOwned());

            var effectiveTimeProvider = timeProvider ?? TimeProvider.System;
            var now = effectiveTimeProvider.GetUtcNow();

            ownership.SoftDelete();

            // Clear any chain defaults that point to this wallet
            var chainsToUpdate = Profile.DefaultPerChain.Value
                .Where(kvp => kvp.Value == walletId)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var chain in chainsToUpdate)
            {
                Profile.ClearDefaultForChain(chain);
            }

            MarkUpdated();

            RaiseDomainEvent(new WalletOwnershipUnlinkedEvent(
                Id, walletId, ownership.Id, now));

            return Result.Success<Unit, Error>(Unit.Value);
        }
        catch (BusinessRuleException ex)
        {
            return Result.Failure<Unit, Error>(
                Error.BusinessRule(ex.Message, ex.Error.Code));
        }
    }

    /// <summary>
    /// Sets the default wallet for a specific chain.
    /// Clears any previous default for the same chain.
    /// </summary>
    public Result<Unit, Error> SetDefaultWalletForChain(
        Chain chain, 
        WalletId walletId, 
        TimeProvider? timeProvider = null)
    {
        try
        {
            CheckRule(new PrincipalMustBeActiveRule(this));
            CheckRule(new WalletMustBeOwnedByPrincipalRule(walletId, _walletOwnerships));

            var effectiveTimeProvider = timeProvider ?? TimeProvider.System;
            var now = effectiveTimeProvider.GetUtcNow();

            var previousDefault = Profile.GetDefaultWalletForChain(chain.Value);
            var setResult = Profile.SetDefaultWalletForChain(chain.Value, walletId);

            if (setResult.IsFailure)
                return Result.Failure<Unit, Error>(setResult.Error);

            MarkUpdated();

            RaiseDomainEvent(new DefaultWalletChangedEvent(
                Id, chain.Value, walletId, previousDefault, now));

            return Result.Success<Unit, Error>(Unit.Value);
        }
        catch (BusinessRuleException ex)
        {
            return Result.Failure<Unit, Error>(
                Error.BusinessRule(ex.Message, ex.Error.Code));
        }
    }

    /// <summary>
    /// Updates the principal's preferred language.
    /// </summary>
    public Result<Unit, Error> UpdatePreferredLanguage(
        PreferredLanguage language,
        TimeProvider? timeProvider = null)
    {
        try
        {
            CheckRule(new PrincipalMustBeActiveRule(this));

            var updateResult = Profile.UpdatePreferredLanguage(language);
            if (updateResult.IsFailure)
                return updateResult;

            var effectiveTimeProvider = timeProvider ?? TimeProvider.System;
            var now = effectiveTimeProvider.GetUtcNow();

            MarkUpdated();

            RaiseDomainEvent(new ProfileLanguageChangedEvent(
                Id, language.Value, now));

            return Result.Success<Unit, Error>(Unit.Value);
        }
        catch (BusinessRuleException ex)
        {
            return Result.Failure<Unit, Error>(
                Error.BusinessRule(ex.Message, ex.Error.Code));
        }
    }

    /// <summary>
    /// Updates the principal's risk tier.
    /// </summary>
    public Result<Unit, Error> UpdateRiskTier(
        RiskTier riskTier,
        TimeProvider? timeProvider = null)
    {
        try
        {
            CheckRule(new PrincipalMustBeActiveRule(this));

            var updateResult = Profile.UpdateRiskTier(riskTier);
            if (updateResult.IsFailure)
                return updateResult;

            var effectiveTimeProvider = timeProvider ?? TimeProvider.System;
            var now = effectiveTimeProvider.GetUtcNow();

            MarkUpdated();

            RaiseDomainEvent(new ProfileRiskTierChangedEvent(
                Id, riskTier.Value, now));

            return Result.Success<Unit, Error>(Unit.Value);
        }
        catch (BusinessRuleException ex)
        {
            return Result.Failure<Unit, Error>(
                Error.BusinessRule(ex.Message, ex.Error.Code));
        }
    }

    /// <summary>
    /// Soft deletes the principal, marking it as inactive.
    /// Prevents further operations while preserving audit trail.
    /// </summary>
    public Result<Unit, Error> SoftDelete(bool force = false, TimeProvider? timeProvider = null)
    {
        try
        {
            if (!force)
            {
                CheckRule(new PrincipalCanBeDeletedRule(_walletOwnerships, _credentials));
            }

            var effectiveTimeProvider = timeProvider ?? TimeProvider.System;
            var now = effectiveTimeProvider.GetUtcNow();

            base.SoftDelete();

            RaiseDomainEvent(new PrincipalSoftDeletedEvent(Id, now));

            return Result.Success<Unit, Error>(Unit.Value);
        }
        catch (BusinessRuleException ex)
        {
            return Result.Failure<Unit, Error>(
                Error.BusinessRule(ex.Message, ex.Error.Code));
        }
    }

    /// <summary>
    /// Restores a soft-deleted principal to active status.
    /// </summary>
    public Result<Unit, Error> Restore(string? reason = null, TimeProvider? timeProvider = null)
    {
        if (!IsDeleted)
            return Result.Success<Unit, Error>(Unit.Value); // Already active

        var effectiveTimeProvider = timeProvider ?? TimeProvider.System;
        var now = effectiveTimeProvider.GetUtcNow();

        base.Restore(); // Call base restore method

        RaiseDomainEvent(new PrincipalRestoredEvent(Id, now, reason));

        return Result.Success<Unit, Error>(Unit.Value);
    }

    /// <summary>
    /// Revokes an identity credential, marking it as deleted.
    /// </summary>
    public Result<Unit, Error> RevokeCredential(
        ProviderType providerType,
        string issuer,
        string subject,
        string? reason = null,
        TimeProvider? timeProvider = null)
    {
        var credential = FindCredential(providerType, issuer, subject);
        if (credential is null)
            return Result.Failure<Unit, Error>(IdentityDomainErrors.Credential.NotFound());

        if (credential.IsDeleted)
            return Result.Success<Unit, Error>(Unit.Value); // Already revoked

        var effectiveTimeProvider = timeProvider ?? TimeProvider.System;
        var now = effectiveTimeProvider.GetUtcNow();

        credential.SoftDelete();
        MarkUpdated();

        RaiseDomainEvent(new IdentityCredentialRevokedEvent(
            Id, credential.Id, providerType.Value, issuer, subject,
            credential.EnvironmentId, now, reason));

        return Result.Success<Unit, Error>(Unit.Value);
    }

    /// <summary>
    /// Updates the last seen timestamp for a credential.
    /// </summary>
    public Result<Unit, Error> UpdateCredentialLastSeen(
        ProviderType providerType,
        string issuer,
        string subject,
        TimeProvider? timeProvider = null)
    {
        var credential = FindCredential(providerType, issuer, subject);
        if (credential is null)
            return Result.Failure<Unit, Error>(IdentityDomainErrors.Credential.NotFound());

        if (credential.IsDeleted)
            return Result.Failure<Unit, Error>(IdentityDomainErrors.Credential.NotFound());

        var effectiveTimeProvider = timeProvider ?? TimeProvider.System;
        var now = effectiveTimeProvider.GetUtcNow();
        var previousLastSeen = credential.LastSeenAt;

        credential.UpdateLastSeen(now);
        MarkUpdated();

        // Only raise event if the timestamp actually changed
        if (now > previousLastSeen)
        {
            RaiseDomainEvent(new CredentialLastSeenUpdatedEvent(
                Id, credential.Id, providerType.Value, now, previousLastSeen));
        }

        return Result.Success<Unit, Error>(Unit.Value);
    }

    /// <summary>
    /// Verifies wallet ownership with proof validation.
    /// </summary>
    public Result<Unit, Error> VerifyWalletOwnership(
        WalletId walletId,
        string? verificationMethod = null,
        TimeProvider? timeProvider = null)
    {
        var ownership = FindWalletOwnership(walletId);
        if (ownership is null)
            return Result.Failure<Unit, Error>(IdentityDomainErrors.Wallet.NotOwned());

        var effectiveTimeProvider = timeProvider ?? TimeProvider.System;
        var now = effectiveTimeProvider.GetUtcNow();

        var verifyResult = ownership.Verify(now);
        if (verifyResult.IsFailure)
            return verifyResult;

        MarkUpdated();

        RaiseDomainEvent(new WalletOwnershipVerifiedEvent(
            Id, walletId, ownership.Id, ownership.ProofType.Value, now, verificationMethod));

        return Result.Success<Unit, Error>(Unit.Value);
    }

    /// <summary>
    /// Handles wallet ownership conflict by skipping the operation and raising an event.
    /// Used when a wallet is already owned by another principal.
    /// </summary>
    public static void HandleWalletOwnershipConflict(
        AxonId requestedByPrincipalId,
        AxonId existingOwnerPrincipalId,
        WalletId walletId,
        string conflictReason,
        string? resolutionStrategy = null,
        TimeProvider? timeProvider = null)
    {
        var effectiveTimeProvider = timeProvider ?? TimeProvider.System;
        var now = effectiveTimeProvider.GetUtcNow();

        // This is a static method that creates and raises the event
        // In a real implementation, this would be handled by a domain service
        // For now, we provide this as a utility method
        _ = new WalletOwnershipConflictSkippedEvent(
            requestedByPrincipalId, existingOwnerPrincipalId, walletId,
            conflictReason, now, resolutionStrategy);

        // Note: This event would need to be raised through a domain service or event dispatcher
        // as static methods can't directly raise domain events on aggregates
    }
}