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
    /// Updates wallet metadata with additive merging.
    /// Enforces W5 invariant - meta size and shape constraints.
    /// </summary>
    public Result<Unit, Error> UpdateMeta(
        Dictionary<string, object> metaPatch)
    {
        try
        {
            CheckRule(new WalletMustBeActiveRule(this));
            CheckRule(new MetaSizeLimitRule(Meta, metaPatch));

            var mergeResult = Meta.Merge(metaPatch);
            if (mergeResult.IsFailure)
                return Result.Failure<Unit, Error>(mergeResult.Error);

            var keysChanged = metaPatch.Keys.ToArray();
            Meta = mergeResult.Value;
            MarkUpdated();

            RaiseDomainEvent(new WalletMetaUpdatedEvent(Id, keysChanged));

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
            
            // Idempotent add - no error if already present
            if (_tags.Add(tag))
            {
                MarkUpdated();
                RaiseDomainEvent(new WalletTaggedEvent(Id, tag.Value));
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
    /// Removes a tag from the wallet.
    /// Idempotent operation - no error if tag not present.
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
            
            // Idempotent remove - no error if not present
            if (_tags.Remove(tag))
            {
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