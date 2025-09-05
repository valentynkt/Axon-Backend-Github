using Axon.Modules.Identity.Domain.Entities;
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
    private readonly List<WalletTag> _walletTags = new();

    // Core immutable attributes (W1-W3: Global uniqueness, immutability, canonical form)
    public ChainId Chain { get; private set; }
    public Address Address { get; private set; }

    // Mutable attributes
    public DateTimeOffset FirstSeenAt { get; private set; }
    public DateTimeOffset LastSeenAt { get; private set; }
    public WalletProfile Profile { get; private set; } = null!;
    
    // Tags collection (W6: Tag policy) - now via join table
    public IReadOnlyCollection<WalletTag> WalletTags => _walletTags.AsReadOnly();
    public IReadOnlySet<Tag> Tags => _walletTags.Select(wt => wt.Tag).ToHashSet();

    // EF Core parameterless constructor
    private Wallet() : base() { }

    private Wallet(
        WalletId id,
        ChainId chain,
        Address address,
        DateTimeOffset firstSeenAt,
        WalletProfile profile) : base(id)
    {
        Chain = chain;
        Address = address;
        FirstSeenAt = firstSeenAt;
        LastSeenAt = firstSeenAt; // W4: Monotonicity - start equal
        Profile = profile;
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
        ProviderType? provider = null,
        string? displayName = null)
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

            var id = WalletId.New();

            // Create initial profile
            var profileResult = WalletProfile.Create(
                id, 
                provider ?? ProviderType.From("unknown"), 
                displayName);
            if (profileResult.IsFailure)
                return Result.Failure<Wallet, Error>(profileResult.Error);

            var wallet = new Wallet(id, chainId, canonicalAddress, firstSeenAt, profileResult.Value);

            // Raise domain event
            wallet.RaiseDomainEvent(new WalletRegisteredEvent(
                id.Value.ToString(), chainId.Value, canonicalAddress.Value, firstSeenAt));

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