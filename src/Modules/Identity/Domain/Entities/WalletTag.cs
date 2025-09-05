using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Domain.Entities;

/// <summary>
/// Join entity representing the many-to-many relationship between wallets and tags.
/// Replaces JSON-based tag storage with proper relational model.
/// </summary>
public sealed class WalletTag : AuditableEntity<Guid>
{
    /// <summary>
    /// The wallet this tag is associated with
    /// </summary>
    public WalletId WalletId { get; private set; }
    
    /// <summary>
    /// The tag value
    /// </summary>
    public Tag Tag { get; private set; }

    // EF Core parameterless constructor
    private WalletTag() : base() 
    {
        Tag = Tag.From("unknown");
    }

    private WalletTag(
        WalletId walletId,
        Tag tag) : base(Guid.CreateVersion7())
    {
        WalletId = walletId;
        Tag = tag;
    }

    /// <summary>
    /// Creates a new WalletTag association.
    /// </summary>
    internal static Result<WalletTag, Error> Create(
        WalletId walletId,
        Tag tag)
    {
        if (walletId == default(WalletId))
        {
            return Result.Failure<WalletTag, Error>(
                Error.Validation("Wallet ID must be provided.", "WALLET.TAG.WALLET_ID.EMPTY"));
        }

        var walletTag = new WalletTag(walletId, tag);
        return Result.Success<WalletTag, Error>(walletTag);
    }

    /// <summary>
    /// Checks if this tag belongs to the specified wallet.
    /// </summary>
    internal bool BelongsTo(WalletId walletId) => WalletId == walletId;

    /// <summary>
    /// Checks if this represents the specified tag.
    /// </summary>
    internal bool IsTag(Tag tag) => Tag == tag;
}