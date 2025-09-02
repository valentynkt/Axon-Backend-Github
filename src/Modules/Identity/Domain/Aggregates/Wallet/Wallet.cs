using Axon.Modules.Identity.Domain.Events;
using Axon.Modules.Identity.Domain.Errors;
using Axon.Modules.Identity.Domain.Rules;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Domain.Entities.Base;
using BuildingBlocks.Core.Domain.Rules;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Domain.Aggregates.Wallet;

/// <summary>
/// Wallet aggregate root representing a unique on-chain address on a specific chain.
/// Models chain+address as a global catalog entry independent of any principal.
/// Ownership is not stored here; it is modeled in Identity BC via WalletOwnership.
/// Enforces all domain invariants W1-W7.
/// </summary>
public sealed partial class Wallet : AggregateRoot<WalletId>
{
    private readonly HashSet<Tag> _tags = new();

    // Core immutable attributes (W1-W3: Global uniqueness, immutability, canonical form)
    public ChainId Chain { get; private set; }
    public Address Address { get; private set; }

    // Mutable attributes
    public DateTimeOffset FirstSeenAt { get; private set; }
    public DateTimeOffset LastSeenAt { get; private set; }
    public WalletMeta Meta { get; private set; } = WalletMeta.Empty;
    
    // Tags collection (W6: Tag policy)
    public IReadOnlySet<Tag> Tags => _tags.ToHashSet();

    // EF Core parameterless constructor
    private Wallet() : base() { }

    private Wallet(
        WalletId id,
        ChainId chain,
        Address address,
        DateTimeOffset firstSeenAt,
        WalletMeta? meta = null) : base(id)
    {
        Chain = chain;
        Address = address;
        FirstSeenAt = firstSeenAt;
        LastSeenAt = firstSeenAt; // W4: Monotonicity - start equal
        Meta = meta ?? WalletMeta.Empty;
    }

    /// <summary>
    /// Registers a new wallet with validation of global uniqueness.
    /// Enforces W1 (global uniqueness), W2 (immutability), W3 (canonical form).
    /// </summary>
    public static async Task<Result<Wallet, Error>> RegisterAsync(
        ChainId chainId,
        string rawAddress,
        DateTimeOffset firstSeenAt,
        Func<ChainId, Address, ValueTask<bool>> walletExistsCheck,
        Dictionary<string, object>? initialMeta = null,
        TimeProvider? timeProvider = null)
    {
        try
        {
            // Validate and normalize address for chain (W3: Canonical form)
            var addressResult = Address.CreateForChain(chainId, rawAddress);
            if (addressResult.IsFailure)
                return Result.Failure<Wallet, Error>(addressResult.Error);

            var canonicalAddress = addressResult.Value;

            // Check global uniqueness (W1)
            await CheckRuleAsync(new WalletMustNotExistRule(chainId, canonicalAddress, walletExistsCheck));

            // Validate initial metadata (W5)
            var metaResult = WalletMeta.Create(initialMeta);
            if (metaResult.IsFailure)
                return Result.Failure<Wallet, Error>(metaResult.Error);

            var effectiveTimeProvider = timeProvider ?? TimeProvider.System;
            var id = WalletId.New();

            var wallet = new Wallet(id, chainId, canonicalAddress, firstSeenAt, metaResult.Value);

            // Raise domain event
            wallet.RaiseDomainEvent(new WalletRegisteredEvent(
                id, chainId, canonicalAddress, firstSeenAt));

            return Result.Success<Wallet, Error>(wallet);
        }
        catch (BusinessRuleException ex)
        {
            return Result.Failure<Wallet, Error>(
                Error.BusinessRule(ex.Message, ex.Error.Code));
        }
        catch (Exception ex)
        {
            return Result.Failure<Wallet, Error>(
                WalletDomainErrors.General.CreationFailed(ex.Message));
        }
    }

    private static void CheckRule(IBusinessRule rule)
    {
        if (rule.IsBroken())
            throw new BusinessRuleException(rule);
    }

    private static async Task CheckRuleAsync(WalletMustNotExistRule rule, CancellationToken ct = default)
    {
        if (await rule.IsBrokenAsync(ct))
            throw new BusinessRuleException(rule);
    }
}