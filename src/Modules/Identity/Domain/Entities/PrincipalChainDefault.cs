using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Domain.Entities;

/// <summary>
/// Principal chain default owned entity representing a default wallet for a specific chain.
/// Owned by AxonPrincipal - enforces single default per chain constraint.
/// Replaces the JSON-based ChainDefaults with strongly-typed collection.
/// </summary>
public sealed class PrincipalChainDefault : AuditableEntity<Guid>
{
    /// <summary>
    /// The principal this default belongs to
    /// </summary>
    public AxonId AxonId { get; private set; }
    
    /// <summary>
    /// The blockchain chain identifier
    /// </summary>
    public ChainId ChainId { get; private set; }
    
    /// <summary>
    /// The wallet ID that is the default for this chain
    /// </summary>
    public WalletId WalletId { get; private set; }

    // EF Core parameterless constructor
    private PrincipalChainDefault() : base() { }

    private PrincipalChainDefault(
        AxonId axonId,
        ChainId chainId,
        WalletId walletId) : base(Guid.CreateVersion7())
    {
        AxonId = axonId;
        ChainId = chainId;
        WalletId = walletId;
    }

    /// <summary>
    /// Creates a new PrincipalChainDefault.
    /// </summary>
    internal static Result<PrincipalChainDefault, Error> Create(
        AxonId axonId,
        ChainId chainId,
        WalletId walletId)
    {
        if (string.IsNullOrWhiteSpace(chainId.Value))
        {
            return Result.Failure<PrincipalChainDefault, Error>(
                Error.Validation("Chain ID cannot be empty.", "PRINCIPAL.CHAIN_DEFAULT.CHAIN_ID.EMPTY"));
        }

        if (walletId == default(WalletId))
        {
            return Result.Failure<PrincipalChainDefault, Error>(
                Error.Validation("Wallet ID must be provided.", "PRINCIPAL.CHAIN_DEFAULT.WALLET_ID.EMPTY"));
        }

        var chainDefault = new PrincipalChainDefault(axonId, chainId, walletId);
        return Result.Success<PrincipalChainDefault, Error>(chainDefault);
    }

    /// <summary>
    /// Updates the default wallet for this chain.
    /// </summary>
    internal Result<Unit, Error> UpdateWallet(WalletId walletId)
    {
        if (walletId == default(WalletId))
        {
            return Result.Failure<Unit, Error>(
                Error.Validation("Wallet ID must be provided.", "PRINCIPAL.CHAIN_DEFAULT.WALLET_ID.EMPTY"));
        }

        WalletId = walletId;
        MarkUpdated();
        
        return Result.Success<Unit, Error>(Unit.Value);
    }

    /// <summary>
    /// Checks if this default belongs to the specified principal.
    /// </summary>
    internal bool BelongsTo(AxonId principalId) => AxonId == principalId;

    /// <summary>
    /// Checks if this default is for the specified chain.
    /// </summary>
    internal bool IsForChain(ChainId chainId) => ChainId == chainId;
}