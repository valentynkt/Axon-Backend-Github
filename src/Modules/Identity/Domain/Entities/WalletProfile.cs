using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;
using WalletProfileEntity = BuildingBlocks.Core.Domain.Entities.Base.AuditableEntity<BuildingBlocks.Primitives.Ids.WalletId>;

namespace Axon.Modules.Identity.Domain.Entities;

/// <summary>
/// Wallet profile owned entity containing typed metadata about a wallet.
/// Owned by Wallet aggregate - can only be modified through the aggregate.
/// Replaces the JSON-based WalletMeta with strongly-typed properties.
/// </summary>
public sealed class WalletProfile : WalletProfileEntity
{
    /// <summary>
    /// The provider/source of this wallet (e.g., Dynamic, MetaMask, etc.)
    /// </summary>
    public ProviderType Provider { get; private set; }
    
    /// <summary>
    /// Optional user-friendly display name for the wallet
    /// </summary>
    public string? DisplayName { get; private set; }

    // EF Core parameterless constructor
    private WalletProfile() : base() 
    {
        Provider = ProviderType.From("unknown");
    }

    private WalletProfile(
        WalletId walletId,
        ProviderType provider,
        string? displayName = null) : base(walletId)
    {
        Provider = provider;
        DisplayName = displayName?.Trim();
    }

    /// <summary>
    /// Creates a new WalletProfile for the specified wallet.
    /// </summary>
    internal static Result<WalletProfile, Error> Create(
        WalletId walletId,
        ProviderType provider,
        string? displayName = null)
    {
        // Validate display name if provided
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            var trimmed = displayName.Trim();
            if (trimmed.Length > 255)
            {
                return Result.Failure<WalletProfile, Error>(
                    Error.Validation("Display name cannot exceed 255 characters.", "WALLET.PROFILE.DISPLAY_NAME.TOO_LONG"));
            }

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                displayName = null; // Normalize empty strings to null
            }
            else
            {
                displayName = trimmed;
            }
        }

        var profile = new WalletProfile(walletId, provider, displayName);
        return Result.Success<WalletProfile, Error>(profile);
    }

    /// <summary>
    /// Updates the wallet profile information.
    /// </summary>
    internal Result<Unit, Error> Update(ProviderType provider, string? displayName = null)
    {
        // Validate display name if provided
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            var trimmed = displayName.Trim();
            if (trimmed.Length > 255)
            {
                return Result.Failure<Unit, Error>(
                    Error.Validation("Display name cannot exceed 255 characters.", "WALLET.PROFILE.DISPLAY_NAME.TOO_LONG"));
            }

            displayName = string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        }

        Provider = provider;
        DisplayName = displayName;
        MarkUpdated();

        return Result.Success<Unit, Error>(Unit.Value);
    }
}