using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.DTOs;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Axon.Modules.Identity.Application.Services;

/// <summary>
/// Service implementation for resolving existing wallets or registering new ones.
/// Consolidates wallet lookup/creation logic to eliminate duplication across handlers.
/// </summary>
public sealed class WalletResolutionService : IWalletResolutionService
{
    private readonly IWalletWriteRepository _walletRepository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WalletResolutionService> _logger;

    public WalletResolutionService(
        IWalletWriteRepository walletRepository,
        TimeProvider timeProvider,
        ILogger<WalletResolutionService> logger)
    {
        _walletRepository = walletRepository ?? throw new ArgumentNullException(nameof(walletRepository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<(Wallet wallet, bool wasCreated), Error>> ResolveOrRegisterAsync(
        ChainId chainId,
        string rawAddress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Resolving wallet for chain {ChainId} and address {RawAddress}", chainId, rawAddress);

            // Canonicalize address via domain value object
            var addressResult = Address.CreateForChain(chainId, rawAddress);
            if (addressResult.IsFailure)
            {
                _logger.LogWarning("Failed to canonicalize address {RawAddress} for chain {ChainId}: {Error}",
                    rawAddress, chainId, addressResult.Error.Message);
                return Result.Failure<(Wallet, bool), Error>(addressResult.Error);
            }

            var canonicalAddress = addressResult.Value;

            // Check if wallet already exists
            var existingWallet = await _walletRepository.GetByChainAndAddressAsync(
                chainId, canonicalAddress, cancellationToken);

            if (existingWallet is not null)
            {
                _logger.LogDebug("Found existing wallet {WalletId} for chain {ChainId} and address {Address}",
                    existingWallet.Id, chainId, canonicalAddress);
                return Result.Success<(Wallet, bool), Error>((existingWallet, false));
            }

            // Register new wallet using domain factory method (preserves eventing/rules)
            var walletResult = await Wallet.RegisterAsync(
                chainId,
                rawAddress,
                _timeProvider.GetUtcNow(),
                async (chain, address) =>
                {
                    var existing = await _walletRepository.GetByChainAndAddressAsync(chain, address, cancellationToken);
                    return existing is not null;
                });

            if (walletResult.IsFailure)
            {
                _logger.LogError("Failed to register new wallet for chain {ChainId} and address {RawAddress}: {Error}",
                    chainId, rawAddress, walletResult.Error.Message);
                return Result.Failure<(Wallet, bool), Error>(walletResult.Error);
            }

            var newWallet = walletResult.Value;
            await _walletRepository.AddAsync(newWallet, cancellationToken);

            // Save the new wallet immediately to ensure it's persisted
            // before any subsequent modifications are applied
            await _walletRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

            // Reload the wallet after save to ensure all navigation properties
            // and related data are properly hydrated for subsequent operations
            var reloadedWallet = await _walletRepository.GetByIdAsync(newWallet.Id, cancellationToken);
            if (reloadedWallet is null)
            {
                _logger.LogError("Failed to reload newly created wallet {WalletId} after save", newWallet.Id);
                return Result.Failure<(Wallet, bool), Error>(
                    Error.Failure("Failed to reload wallet after creation", "IDENTITY.WALLET.RELOAD_FAILED"));
            }

            _logger.LogDebug("Created new wallet {WalletId} for chain {ChainId} and address {Address}",
                reloadedWallet.Id, chainId, canonicalAddress);

            return Result.Success<(Wallet, bool), Error>((reloadedWallet, true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error resolving wallet for chain {ChainId} and address {RawAddress}",
                chainId, rawAddress);
            return Result.Failure<(Wallet, bool), Error>(
                Error.Failure("Failed to resolve or register wallet", "IDENTITY.WALLET.RESOLUTION_FAILED"));
        }
    }
}