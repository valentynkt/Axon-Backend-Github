using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Application.DTOs;

/// <summary>
/// Lightweight result from wallet resolution containing only essential data
/// without entity tracking concerns. Used when we need wallet information
/// but don't want to track the entity in the current DbContext.
/// </summary>
public sealed record WalletResolutionResult(
    WalletId Id,
    ChainId ChainId,
    Address Address,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt,
    bool WasCreated
);