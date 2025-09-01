using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Errors;
using Axon.Modules.Identity.Domain.Events;
using Axon.Modules.Identity.Domain.Rules;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Domain.Entities.Base;
using BuildingBlocks.Core.Domain.Events;
using BuildingBlocks.Core.Domain.Rules;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;

/// <summary>
/// AxonPrincipal aggregate root representing a canonical subject (human or service)
/// that owns identity credentials, wallet ownerships, and profile preferences.
/// Enforces domain invariants and maintains identity integrity.
/// </summary>
public sealed partial class AxonPrincipal : AggregateRoot<AxonId>
{
    private readonly List<IdentityCredential> _credentials = new();
    private readonly List<WalletOwnership> _walletOwnerships = new();

    public IReadOnlyCollection<IdentityCredential> Credentials => _credentials;
    public IReadOnlyCollection<WalletOwnership> WalletOwnerships => _walletOwnerships;

    public PrincipalType Type { get; private set; }
    public EmailHash? PrimaryEmailHash { get; private set; }
    public PrincipalProfile Profile { get; private set; } = null!;

    // Computed properties
    public bool IsHuman => Type.IsHuman;
    public bool IsService => Type.IsService;

    // EF Core parameterless constructor
    private AxonPrincipal() : base() { }

    private AxonPrincipal(
        AxonId id,
        PrincipalType type,
        EmailHash? primaryEmailHash = null) : base(id)
    {
        Type = type;
        PrimaryEmailHash = primaryEmailHash;
        Profile = PrincipalProfile.CreateDefault(id);
    }

    /// <summary>
    /// Creates a new human principal.
    /// </summary>
    public static Result<AxonPrincipal, Error> CreateHumanPrincipal(
        EmailHash? primaryEmailHash = null,
        TimeProvider? timeProvider = null)
    {
        try
        {
            var effectiveTimeProvider = timeProvider ?? TimeProvider.System;
            var id = AxonId.New();
            var now = effectiveTimeProvider.GetUtcNow();

            var principal = new AxonPrincipal(id, PrincipalType.Human, primaryEmailHash);

            principal.RaiseDomainEvent(new PrincipalCreatedEvent(
                id, PrincipalType.Human.Value, primaryEmailHash?.Value, now));

            return Result.Success<AxonPrincipal, Error>(principal);
        }
        catch (Exception ex)
        {
            return Result.Failure<AxonPrincipal, Error>(
                IdentityDomainErrors.Principal.CreationFailed(ex.Message));
        }
    }

    /// <summary>
    /// Creates a new service principal.
    /// </summary>
    public static Result<AxonPrincipal, Error> CreateServicePrincipal(
        TimeProvider? timeProvider = null)
    {
        try
        {
            var effectiveTimeProvider = timeProvider ?? TimeProvider.System;
            var id = AxonId.New();
            var now = effectiveTimeProvider.GetUtcNow();

            var principal = new AxonPrincipal(id, PrincipalType.Service);

            principal.RaiseDomainEvent(new PrincipalCreatedEvent(
                id, PrincipalType.Service.Value, null, now));

            return Result.Success<AxonPrincipal, Error>(principal);
        }
        catch (Exception ex)
        {
            return Result.Failure<AxonPrincipal, Error>(
                IdentityDomainErrors.Principal.CreationFailed(ex.Message));
        }
    }


    private static void CheckRule(IBusinessRule rule)
    {
        if (rule.IsBroken())
            throw new BusinessRuleException(rule);
    }
}