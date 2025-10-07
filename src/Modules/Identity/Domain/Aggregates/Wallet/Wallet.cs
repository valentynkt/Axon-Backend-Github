using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Domain.Entities.Base;

namespace Axon.Modules.Identity.Domain.Aggregates.Wallet;

/// <summary>
/// Wallet aggregate representing a blockchain wallet.
/// ChainId contains chain and network information (e.g., "solana:mainnet", "ethereum:mainnet").
/// </summary>
public sealed partial class Wallet : AggregateRoot<WalletId>
{
    public string ChainId { get; private set; } = string.Empty;
    public Address Address { get; private set; }
    public DateTime FirstSeenAt { get; private set; }
    public DateTime LastSeenAt { get; private set; }

    // EF Core constructor
    private Wallet() { }

    private Wallet(WalletId id, string chainId, Address address, DateTime timestamp) : base(id)
    {
        ChainId = chainId;
        Address = address;
        FirstSeenAt = timestamp;
        LastSeenAt = timestamp;
    }

    public static Wallet Create(WalletId? id, string chainId, Address address, DateTime? timestamp = null)
    {
        var effectiveTimestamp = timestamp ?? DateTime.UtcNow;
        return new Wallet(id ?? WalletId.New(), chainId, address, effectiveTimestamp);
    }

    public void UpdateLastSeen(DateTime timestamp)
    {
        if (timestamp > LastSeenAt)
            LastSeenAt = timestamp;
    }
}