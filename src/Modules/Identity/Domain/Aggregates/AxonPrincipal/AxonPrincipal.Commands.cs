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
    /// Enforces in-aggregate uniqueness; cross-principal uniqueness is handled by the application/domain service.
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
    /// Enforces in-aggregate uniqueness; cross-principal uniqueness is handled by the application/domain service.
    /// </summary>
    public Result<WalletOwnership, Error> LinkWallet(
        WalletId walletId,
        ChainId chainId,
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
                Id, walletId, chainId, proofType, accessMode, label: label, firstLinkedAt: now);

            if (ownershipResult.IsFailure)
                return Result.Failure<WalletOwnership, Error>(ownershipResult.Error);

            var ownership = ownershipResult.Value;
            _walletOwnerships.Add(ownership);

            // If link creates a verified-signing ownership, set default (via aggregate command)
            if (ownership.IsVerifiedSigning && !Profile.HasDefaultWalletForChain(chainId))
            {
                var setDefault = SetDefaultWalletForChain(chainId, walletId, timeProvider);
                if (setDefault.IsFailure)
                    return Result.Failure<WalletOwnership, Error>(setDefault.Error);
            }

            MarkUpdated();

            RaiseDomainEvent(new WalletOwnershipLinkedEvent(
                Id.Value.ToString(), walletId.Value.ToString(), ownership.Id.Value.ToString(), proofType.Value, 
                accessMode?.Value ?? AccessMode.Default.Value, chainId.Value, now));

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

            // Clear any chain defaults that point to this wallet using dedicated method
            var chainsToUpdate = Profile.DefaultPerChain.Value
                .Where(kvp => kvp.Value == walletId)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var chain in chainsToUpdate)
            {
                var clearResult = ClearDefaultWalletForChain(chain, timeProvider);
                if (clearResult.IsFailure)
                    return clearResult;
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
    /// Validates that the wallet belongs to the correct chain and is verified signing.
    /// Clears any previous default for the same chain.
    /// </summary>
    public Result<Unit, Error> SetDefaultWalletForChain(
        ChainId chainId, 
        WalletId walletId, 
        TimeProvider? timeProvider = null)
    {
        try
        {
            CheckRule(new PrincipalMustBeActiveRule(this));
            CheckRule(new WalletMustBeOwnedByPrincipalRule(walletId, _walletOwnerships));

            // Find the ownership to validate chain and signing status
            var ownership = FindWalletOwnership(walletId);
            if (ownership is null)
                return Result.Failure<Unit, Error>(IdentityDomainErrors.Wallet.NotOwned());

            // Validate chain matches ownership's chain (E5)
            if (ownership.ChainId != chainId)
                return Result.Failure<Unit, Error>(IdentityDomainErrors.Wallet.ChainMismatch());

            // Validate ownership is verified signing (E9)
            if (!ownership.IsVerifiedSigning)
                return Result.Failure<Unit, Error>(IdentityDomainErrors.Wallet.WatchOnlyNotAllowedAsDefault());

            var effectiveTimeProvider = timeProvider ?? TimeProvider.System;
            var now = effectiveTimeProvider.GetUtcNow();

            var previousDefault = Profile.GetDefaultWalletForChain(chainId);
            var setResult = Profile.SetDefaultWalletForChain(chainId, walletId);

            if (setResult.IsFailure)
                return Result.Failure<Unit, Error>(setResult.Error);

            MarkUpdated();

            RaiseDomainEvent(new DefaultWalletChangedEvent(
                Id.Value.ToString(), chainId.Value, walletId.Value.ToString(), previousDefault?.Value.ToString(), now));

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
            CheckRule(new ValidRiskTierRule(riskTier, Type));

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
    /// Enforces business rules to ensure safe deletion.
    /// </summary>
    public Result<Unit, Error> SoftDelete(TimeProvider? timeProvider = null)
    {
        try
        {
            CheckRule(new PrincipalCanBeDeletedRule(_walletOwnerships, _credentials));

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

        // After verification, make it default if none exists on this chain and it's signing
        if (ownership.IsVerifiedSigning && !Profile.HasDefaultWalletForChain(ownership.ChainId))
        {
            var setDefault = SetDefaultWalletForChain(ownership.ChainId, walletId, timeProvider);
            if (setDefault.IsFailure)
                return setDefault;
        }

        return Result.Success<Unit, Error>(Unit.Value);
    }

    /// <summary>
    /// Updates the label for a wallet ownership.
    /// </summary>
    public Result<Unit, Error> UpdateWalletLabel(
        WalletId walletId,
        string? label,
        TimeProvider? timeProvider = null)
    {
        try
        {
            CheckRule(new PrincipalMustBeActiveRule(this));

            var ownership = FindWalletOwnership(walletId);
            if (ownership is null)
                return Result.Failure<Unit, Error>(IdentityDomainErrors.Wallet.NotOwned());

            if (ownership.IsDeleted)
                return Result.Failure<Unit, Error>(IdentityDomainErrors.Wallet.NotOwned());

            var effectiveTimeProvider = timeProvider ?? TimeProvider.System;
            var now = effectiveTimeProvider.GetUtcNow();

            var previousLabel = ownership.Label;
            
            // Only update if there's an actual change
            if (previousLabel == label)
                return Result.Success<Unit, Error>(Unit.Value);
                
            var updateResult = ownership.UpdateLabel(label);
            
            if (updateResult.IsFailure)
                return updateResult;

            MarkUpdated();

            RaiseDomainEvent(new WalletLabelUpdatedEvent(
                Id, walletId, ownership.Id, label, previousLabel, now));

            return Result.Success<Unit, Error>(Unit.Value);
        }
        catch (BusinessRuleException ex)
        {
            return Result.Failure<Unit, Error>(
                Error.BusinessRule(ex.Message, ex.Error.Code));
        }
    }

    /// <summary>
    /// Updates the access mode for a wallet ownership.
    /// </summary>
    public Result<Unit, Error> UpdateWalletAccessMode(
        WalletId walletId,
        AccessMode accessMode,
        TimeProvider? timeProvider = null)
    {
        try
        {
            CheckRule(new PrincipalMustBeActiveRule(this));

            var ownership = FindWalletOwnership(walletId);
            if (ownership is null)
                return Result.Failure<Unit, Error>(IdentityDomainErrors.Wallet.NotOwned());

            if (ownership.IsDeleted)
                return Result.Failure<Unit, Error>(IdentityDomainErrors.Wallet.NotOwned());

            var effectiveTimeProvider = timeProvider ?? TimeProvider.System;
            var now = effectiveTimeProvider.GetUtcNow();

            var previousAccessMode = ownership.AccessMode;
            
            // Only update if there's an actual change
            if (previousAccessMode == accessMode)
                return Result.Success<Unit, Error>(Unit.Value);
                
            var updateResult = ownership.UpdateAccessMode(accessMode);
            
            if (updateResult.IsFailure)
                return updateResult;

            MarkUpdated();

            RaiseDomainEvent(new WalletAccessModeUpdatedEvent(
                Id, walletId, ownership.Id, accessMode.Value, previousAccessMode.Value, now));

            return Result.Success<Unit, Error>(Unit.Value);
        }
        catch (BusinessRuleException ex)
        {
            return Result.Failure<Unit, Error>(
                Error.BusinessRule(ex.Message, ex.Error.Code));
        }
    }

    /// <summary>
    /// Clears the default wallet for a specific chain and raises appropriate events.
    /// Provides a centralized method for clearing defaults with proper event emission.
    /// </summary>
    public Result<Unit, Error> ClearDefaultWalletForChain(
        ChainId chainId, 
        TimeProvider? timeProvider = null)
    {
        try
        {
            CheckRule(new PrincipalMustBeActiveRule(this));

            var effectiveTimeProvider = timeProvider ?? TimeProvider.System;
            var now = effectiveTimeProvider.GetUtcNow();

            var previousDefault = Profile.GetDefaultWalletForChain(chainId);
            
            // No-op if there's no default to clear
            if (previousDefault is null)
                return Result.Success<Unit, Error>(Unit.Value);

            var clearResult = Profile.ClearDefaultForChain(chainId);
            if (clearResult.IsFailure)
                return clearResult;

            MarkUpdated();

            RaiseDomainEvent(new DefaultWalletChangedEvent(
                Id.Value.ToString(), chainId.Value, null, previousDefault.Value.ToString(), now));

            return Result.Success<Unit, Error>(Unit.Value);
        }
        catch (BusinessRuleException ex)
        {
            return Result.Failure<Unit, Error>(
                Error.BusinessRule(ex.Message, ex.Error.Code));
        }
    }

}