using Axon.Modules.Identity.Domain.Errors;
using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Domain.Aggregates.Wallet;

/// <summary>
/// Wallet partial class containing query operations (read-only methods).
/// </summary>
public sealed partial class Wallet
{
    /// <summary>
    /// Checks if the wallet matches the specified chain and address.
    /// Used for lookup and identity verification.
    /// </summary>
    public bool IsForChainAndAddress(ChainId chainId, Address address)
    {
        return Chain.Equals(chainId) && Address.Equals(address);
    }

    /// <summary>
    /// Checks if the wallet has the specified tag.
    /// </summary>
    public bool HasTag(string tagValue)
    {
        var tagResult = Tag.Create(tagValue);
        return tagResult.IsSuccess && _walletTags.Any(wt => wt.Tag.Equals(tagResult.Value));
    }

    /// <summary>
    /// Gets a metadata value by key with type conversion.
    /// </summary>
    public string? GetDisplayName()
    {
        return Profile.DisplayName;
    }
    
    public ProviderType GetProvider()
    {
        return Profile.Provider;
    }

    /// <summary>
    /// Checks if the wallet is active (not soft-deleted).
    /// </summary>
    public bool IsActive => !IsDeleted;

    /// <summary>
    /// Gets the age of the wallet since first registration.
    /// </summary>
    public TimeSpan Age => DateTimeOffset.UtcNow - FirstSeenAt;

    /// <summary>
    /// Gets the time since last activity.
    /// </summary>
    public TimeSpan TimeSinceLastSeen => DateTimeOffset.UtcNow - LastSeenAt;

    /// <summary>
    /// Checks if the wallet has been recently active within the specified timespan.
    /// </summary>
    public bool IsRecentlyActive(TimeSpan within)
    {
        return TimeSinceLastSeen <= within;
    }

    /// <summary>
    /// Gets the number of tags currently applied to the wallet.
    /// </summary>
    public int TagCount => _walletTags.Count;

    /// <summary>
    /// Checks if the wallet has any metadata.
    /// </summary>
    public bool HasDisplayName => !string.IsNullOrWhiteSpace(Profile.DisplayName);

    /// <summary>
    /// Gets the estimated size of the wallet's metadata in bytes.
    /// </summary>
    public bool IsFromProvider(ProviderType provider) => Profile.Provider.Equals(provider);

    /// <summary>
    /// Creates a summary string for the wallet (useful for logging/debugging).
    /// </summary>
    public string ToSummaryString()
    {
        var status = IsDeleted ? "DELETED" : "ACTIVE";
        var tagList = _walletTags.Count > 0 ? $", Tags: [{string.Join(", ", _walletTags.Select(wt => wt.Tag.Value))}]" : "";
        var provider = $", Provider: {Profile.Provider.Value}";
        var displayName = !string.IsNullOrWhiteSpace(Profile.DisplayName) ? $", Name: '{Profile.DisplayName}'" : "";
        
        return $"Wallet[{Id}]: {Chain.Value}:{Address.Value} ({status}), " +
               $"FirstSeen: {FirstSeenAt:yyyy-MM-dd}, LastSeen: {LastSeenAt:yyyy-MM-dd}" +
               $"{provider}{displayName}{tagList}";
    }

    /// <summary>
    /// Validates that the wallet's current state is consistent with all invariants.
    /// Useful for testing and diagnostics.
    /// </summary>
    public Result<Unit, Error> ValidateInvariants()
    {
        // W4: Time monotonicity
        if (LastSeenAt < FirstSeenAt)
            return Result.Failure<Unit, Error>(WalletDomainErrors.Wallet.TimestampRegression());

        // W6: All tags must be valid
        foreach (var walletTag in _walletTags)
        {
            var tagValidation = Tag.Create(walletTag.Tag.Value);
            if (tagValidation.IsFailure)
                return Result.Failure<Unit, Error>(tagValidation.Error);
        }

        return Result.Success<Unit, Error>(Unit.Value);
    }
}