using StronglyTypedIds;

namespace BuildingBlocks.Primitives.Ids;

/// <summary>
/// Strongly-typed ID for Wallet aggregate.
/// Uses Guid as underlying type for global uniqueness.
/// </summary>
[StronglyTypedId]
public partial struct WalletId { }