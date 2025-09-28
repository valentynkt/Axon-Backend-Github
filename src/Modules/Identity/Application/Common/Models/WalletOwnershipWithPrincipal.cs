using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Entities;

namespace Axon.Modules.Identity.Application.Common.Models;

/// <summary>
/// DTO that combines WalletOwnership with its associated AxonPrincipal.
/// Used when both the ownership and principal data are needed together.
/// </summary>
public sealed record WalletOwnershipWithPrincipal(
    WalletOwnership Ownership,
    AxonPrincipal Principal
);