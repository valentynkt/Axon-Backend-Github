using System.Globalization;
using Axon.Modules.Identity.Application.Commands.EnsureWalletLinked;
using Axon.Modules.Identity.Application.Commands.UpsertWalletActivity;
using Axon.Modules.Identity.Application.Common.Constants;
using Axon.Modules.Identity.Application.DTOs.Exchange;
using Axon.Modules.Identity.Application.DTOs.Responses.CommandResponses;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Application.Services;

/// <summary>
/// Service responsible for processing multiple wallets during JWT exchange
/// </summary>
public sealed class WalletProcessorService : IWalletProcessorService
{
    private readonly IDynamicToCommandsMapper _mapper;
    private readonly IMediator _mediator;
    private readonly ILogger<WalletProcessorService> _logger;

    public WalletProcessorService(
        IDynamicToCommandsMapper mapper,
        IMediator mediator,
        ILogger<WalletProcessorService> logger)
    {
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<WalletProcessingResult, Error>> ProcessWalletsAsync(
        List<ExchangeWalletData> wallets, 
        string axonId,
        HashSet<string> seenChainsWithDefault,
        string correlationId, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(wallets);
        ArgumentException.ThrowIfNullOrWhiteSpace(axonId);
        ArgumentNullException.ThrowIfNull(seenChainsWithDefault);

        var linkedCount = 0;
        var defaultsApplied = 0;
        var skippedCount = 0;
        var conflictsCount = 0;

        _logger.LogDebug("Processing {WalletCount} wallets for principal {AxonId}", wallets.Count, axonId);

        foreach (var wallet in wallets)
        {
            try
            {
                // Step 1: Upsert wallet activity
                var activityResult = await UpsertWalletActivityAsync(wallet, correlationId, cancellationToken);
                if (activityResult.IsFailure)
                {
                    _logger.LogWarning("Wallet activity upsert failed for {Chain}:{Address}, skipping: {ErrorCode} - {ErrorMessage}", 
                        wallet.Chain, wallet.Address, activityResult.Error.Code, activityResult.Error.Message);
                    skippedCount++;
                    continue;
                }

                // Step 2: Ensure wallet is linked with default logic
                var linkResult = await EnsureWalletLinkedAsync(
                    axonId, 
                    wallet, 
                    seenChainsWithDefault, 
                    correlationId, 
                    cancellationToken);

                if (linkResult.IsFailure)
                {
                    _logger.LogWarning("Wallet linking failed for {Chain}:{Address}, skipping: {ErrorCode} - {ErrorMessage}", 
                        wallet.Chain, wallet.Address, linkResult.Error.Code, linkResult.Error.Message);
                    skippedCount++;
                    continue;
                }

                var linkResponse = linkResult.Value;

                // Count successful links
                if (linkResponse.Status is EnsureWalletStatus.RegisteredAndLinked 
                                         or EnsureWalletStatus.Linked 
                                         or EnsureWalletStatus.Updated
                                         or EnsureWalletStatus.AlreadyLinked)
                {
                    linkedCount++;
                }

                // Count conflicts (don't fail, just track)
                if (linkResponse.Status == EnsureWalletStatus.ConflictOwnedByOther)
                {
                    conflictsCount++;
                    _logger.LogInformation("Wallet ownership conflict: {Chain}:{Address} owned by another principal", 
                        wallet.Chain, wallet.Address);
                }

                // Count defaults applied
                if (linkResponse.AppliedDefault == true)
                {
                    defaultsApplied++;
                }
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency conflict processing wallet {Chain}:{Address}", 
                    wallet.Chain, wallet.Address);
                // Return error instead of continuing - concurrency conflicts should fail the operation
                return Result.Failure<WalletProcessingResult, Error>(
                    Error.Concurrency(
                        $"Concurrent update conflict for wallet {wallet.Chain}:{wallet.Address}. Please retry.", 
                        "IDENTITY.WALLET.CONCURRENCY_CONFLICT"));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Wallet processing cancelled for {Chain}:{Address}", 
                    wallet.Chain, wallet.Address);
                // Don't increment skipped count for cancellations - operation was interrupted, not failed
                throw; // Re-throw to propagate cancellation
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing wallet {Chain}:{Address}, skipping", 
                    wallet.Chain, wallet.Address);
                skippedCount++;
            }
        }

        var result = new WalletProcessingResult(linkedCount, defaultsApplied, skippedCount, conflictsCount);
        _logger.LogDebug("Wallet processing completed: {Result}", result);
        
        return Result.Success<WalletProcessingResult, Error>(result);
    }

    #region Private Methods

    /// <summary>
    /// Upserts wallet activity for a single wallet
    /// </summary>
    private async Task<Result<WalletActivityResponse, Error>> UpsertWalletActivityAsync(
        ExchangeWalletData wallet, 
        string correlationId, 
        CancellationToken cancellationToken)
    {
        try
        {
            var command = _mapper.MapToUpsertWalletActivityCommand(wallet, correlationId);
            var result = await _mediator.Send(command, cancellationToken);
            
            if (result.IsSuccess)
            {
                if (result.Value != null)
                {
                    _logger.LogDebug("Wallet activity updated for {Chain}:{Address} with status {Status}", 
                        wallet.Chain, wallet.Address, result.Value.Status);
                }
                else
                {
                    _logger.LogWarning("Wallet activity result was null for {Chain}:{Address}", 
                        wallet.Chain, wallet.Address);
                    return Result.Failure<WalletActivityResponse, Error>(
                        Error.Internal("Wallet activity result was null", "NULL_ACTIVITY_RESULT"));
                }
            }
            
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Operation was cancelled - let it propagate
            throw;
        }
        catch (ArgumentException ex) when (ex.Message.Contains("Invalid chain format", StringComparison.Ordinal))
        {
            // Invalid chain/address format - skip this wallet
            return Result.Failure<WalletActivityResponse, Error>(
                Error.Validation($"Invalid chain format: {wallet.Chain}", DynamicAuthConstants.ErrorCodes.InvalidChain));
        }
    }

    /// <summary>
    /// Ensures wallet is linked with default-per-chain logic
    /// </summary>
    private async Task<Result<EnsureWalletResponse, Error>> EnsureWalletLinkedAsync(
        string axonId, 
        ExchangeWalletData wallet, 
        HashSet<string> seenChainsWithDefault,
        string correlationId, 
        CancellationToken cancellationToken)
    {
        try
        {
            // Determine if this should be set as default for this chain
            var walletActivityCommand = _mapper.MapToUpsertWalletActivityCommand(wallet, correlationId);
            var normalizedChain = walletActivityCommand.ChainId!.Value.ToString(CultureInfo.InvariantCulture);
            var setAsDefault = !seenChainsWithDefault.Contains(normalizedChain);

            var command = _mapper.MapToEnsureWalletLinkedCommand(
                AxonId.Parse(axonId, CultureInfo.InvariantCulture), 
                wallet, 
                setAsDefault, 
                correlationId);
                
            var result = await _mediator.Send(command, cancellationToken);
            
            if (result.IsSuccess)
            {
                _logger.LogDebug("Wallet linking completed for {Chain}:{Address} with status {Status}, setAsDefault={SetAsDefault}", 
                    wallet.Chain, wallet.Address, result.Value.Status, setAsDefault);

                // If we successfully set as default, track this chain
                if (setAsDefault && result.Value.AppliedDefault == true)
                {
                    seenChainsWithDefault.Add(normalizedChain);
                }
            }
            
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Operation was cancelled - let it propagate
            throw;
        }
        catch (ArgumentException ex) when (ex.Message.Contains("Invalid chain format", StringComparison.Ordinal))
        {
            // Invalid chain/address format - skip this wallet
            return Result.Failure<EnsureWalletResponse, Error>(
                Error.Validation($"Invalid chain format: {wallet.Chain}", DynamicAuthConstants.ErrorCodes.InvalidChain));
        }
    }

    #endregion
}

/// <summary>
/// Service responsible for processing multiple wallets during JWT exchange operations.
/// Handles activity updates, ownership linking, and default-per-chain assignment in a coordinated manner.
/// </summary>
public interface IWalletProcessorService
{
    /// <summary>
    /// Processes a list of wallets in sequence, handling activity updates, ownership linking, and default assignment.
    /// Implements the 20/80 rule: first wallet per chain becomes default automatically.
    /// Gracefully handles individual wallet failures without stopping the entire process.
    /// </summary>
    /// <param name="wallets">The list of wallets to process</param>
    /// <param name="axonId">The principal's Axon ID for ownership linking</param>
    /// <param name="seenChainsWithDefault">Mutable set tracking chains that already have defaults</param>
    /// <param name="correlationId">Correlation ID for request tracking</param>
    /// <param name="cancellationToken">Cancellation token for async operations</param>
    /// <returns>A result containing counts of linked, skipped, conflicted, and default wallets</returns>
    Task<Result<WalletProcessingResult, Error>> ProcessWalletsAsync(
        List<ExchangeWalletData> wallets, 
        string axonId,
        HashSet<string> seenChainsWithDefault,
        string correlationId, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of wallet processing operation containing detailed metrics.
/// Used for telemetry, logging, and building the final exchange response.
/// </summary>
/// <param name="LinkedCount">Number of wallets successfully linked to the principal</param>
/// <param name="DefaultsApplied">Number of wallets set as chain defaults during processing</param>
/// <param name="SkippedCount">Number of wallets skipped due to validation errors or other issues</param>
/// <param name="ConflictsCount">Number of wallets that couldn't be linked due to ownership conflicts</param>
public sealed record WalletProcessingResult(
    int LinkedCount, 
    int DefaultsApplied, 
    int SkippedCount, 
    int ConflictsCount)
{
    public override string ToString() => 
        $"Linked: {LinkedCount}, Defaults: {DefaultsApplied}, Skipped: {SkippedCount}, Conflicts: {ConflictsCount}";
};