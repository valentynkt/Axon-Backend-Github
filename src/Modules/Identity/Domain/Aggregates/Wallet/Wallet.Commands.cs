using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Events;
using Axon.Modules.Identity.Domain.Errors;
using Axon.Modules.Identity.Domain.Rules;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Domain.Rules;

namespace Axon.Modules.Identity.Domain.Aggregates.Wallet;

/// <summary>
/// Wallet partial class containing command operations (state-changing methods).
/// </summary>
public sealed partial class Wallet
{
    /// <summary>
    /// Updates LastSeenAt timestamp reflecting external observation.
    /// Enforces W4 invariant - time monotonicity.
    /// </summary>
    public Result<Unit, Error> TouchSeen(DateTimeOffset observedAt)
    {
        try
        {
            CheckRule(new WalletMustBeActiveRule(this));
            CheckRule(new TimestampMonotonicityRule(LastSeenAt, FirstSeenAt, observedAt));

            var previousLastSeen = LastSeenAt;
            
            // Apply monotonic update - take the maximum
            LastSeenAt = observedAt > LastSeenAt ? observedAt : LastSeenAt;
            MarkUpdated();

            // Only raise event if timestamp actually changed
            if (LastSeenAt > previousLastSeen)
            {
                RaiseDomainEvent(new WalletTouchedEvent(Id, LastSeenAt));
            }

            return Result.Success<Unit, Error>(Unit.Value);
        }
        catch (BusinessRuleException ex)
        {
            return Result.Failure<Unit, Error>(
                Error.BusinessRule(ex.Message, ex.Error.Code));
        }
    }

    /// <summary>
    /// Updates wallet profile information.
    /// Replaces the old UpdateMeta method with typed profile updates.
    /// </summary>
    public Result<Unit, Error> UpdateProfile(ProviderType provider, string? displayName = null)
    {
        try
        {
            CheckRule(new WalletMustBeActiveRule(this));

            var updateResult = Profile.Update(provider, displayName);
            if (updateResult.IsFailure)
                return Result.Failure<Unit, Error>(updateResult.Error);

            MarkUpdated();
            RaiseDomainEvent(new WalletMetaUpdatedEvent(Id, new[] { "provider", "displayName" }));

            return Result.Success<Unit, Error>(Unit.Value);
        }
        catch (BusinessRuleException ex)
        {
            return Result.Failure<Unit, Error>(
                Error.BusinessRule(ex.Message, ex.Error.Code));
        }
    }

    /// <summary>
    /// Adds a tag to the wallet.
    /// Enforces W6 invariant - tag policy validation.
    /// Now uses proper join table instead of JSON array.
    /// </summary>
    public Result<Unit, Error> AddTag(string tagValue)
    {
        try
        {
            CheckRule(new WalletMustBeActiveRule(this));
            CheckRule(new TagMustBeAllowedRule(tagValue));

            var tagResult = Tag.Create(tagValue);
            if (tagResult.IsFailure)
                return Result.Failure<Unit, Error>(tagResult.Error);

            var tag = tagResult.Value;
            
            // Check if tag already exists (idempotent add)
            if (_walletTags.Any(wt => wt.IsTag(tag)))
            {
                return Result.Success<Unit, Error>(Unit.Value); // Already present
            }

            // Create new WalletTag association
            var walletTagResult = WalletTag.Create(Id, tag);
            if (walletTagResult.IsFailure)
                return Result.Failure<Unit, Error>(walletTagResult.Error);

            _walletTags.Add(walletTagResult.Value);
            MarkUpdated();
            RaiseDomainEvent(new WalletTaggedEvent(Id, tag.Value));

            return Result.Success<Unit, Error>(Unit.Value);
        }
        catch (BusinessRuleException ex)
        {
            return Result.Failure<Unit, Error>(
                Error.BusinessRule(ex.Message, ex.Error.Code));
        }
    }

    /// <summary>
    /// Removes a tag from the wallet.
    /// Idempotent operation - no error if tag not present.
    /// Now uses proper join table instead of JSON array.
    /// </summary>
    public Result<Unit, Error> RemoveTag(string tagValue)
    {
        try
        {
            CheckRule(new WalletMustBeActiveRule(this));

            var tagResult = Tag.Create(tagValue);
            if (tagResult.IsFailure)
                return Result.Failure<Unit, Error>(tagResult.Error);

            var tag = tagResult.Value;
            
            // Find and remove the WalletTag association
            var walletTag = _walletTags.FirstOrDefault(wt => wt.IsTag(tag));
            if (walletTag != null)
            {
                _walletTags.Remove(walletTag);
                MarkUpdated();
                RaiseDomainEvent(new WalletUntaggedEvent(Id, tag.Value));
            }

            return Result.Success<Unit, Error>(Unit.Value);
        }
        catch (BusinessRuleException ex)
        {
            return Result.Failure<Unit, Error>(
                Error.BusinessRule(ex.Message, ex.Error.Code));
        }
    }

    /// <summary>
    /// Adds a tag using the Tag value object directly.
    /// </summary>
    public Result<Unit, Error> AddTag(Tag tag)
    {
        return AddTag(tag.Value);
    }

    /// <summary>
    /// Removes a tag using the Tag value object directly.
    /// </summary>
    public Result<Unit, Error> RemoveTag(Tag tag)
    {
        return RemoveTag(tag.Value);
    }

    /// <summary>
    /// Soft deletes the wallet, marking it as inactive.
    /// Enforces W7 invariant - prevents further mutations except Restore.
    /// </summary>
    public Result<Unit, Error> SoftDelete(TimeProvider? timeProvider = null)
    {
        try
        {
            if (IsDeleted)
                return Result.Success<Unit, Error>(Unit.Value); // Already deleted

            var effectiveTimeProvider = timeProvider ?? TimeProvider.System;
            var now = effectiveTimeProvider.GetUtcNow();

            base.SoftDelete();

            RaiseDomainEvent(new WalletSoftDeletedEvent(Id, now));

            return Result.Success<Unit, Error>(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result.Failure<Unit, Error>(
                Error.Internal($"Failed to soft delete wallet: {ex.Message}", "WALLET.SOFT_DELETE_FAILED"));
        }
    }

    /// <summary>
    /// Restores a soft-deleted wallet to active status.
    /// Allows mutations again while preserving W2/W3 (immutability of core fields).
    /// </summary>
    public Result<Unit, Error> Restore(TimeProvider? timeProvider = null)
    {
        try
        {
            if (!IsDeleted)
                return Result.Success<Unit, Error>(Unit.Value); // Already active

            var effectiveTimeProvider = timeProvider ?? TimeProvider.System;
            var now = effectiveTimeProvider.GetUtcNow();

            base.Restore();

            RaiseDomainEvent(new WalletRestoredEvent(Id, now));

            return Result.Success<Unit, Error>(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result.Failure<Unit, Error>(
                Error.Internal($"Failed to restore wallet: {ex.Message}", "WALLET.RESTORE_FAILED"));
        }
    }
}