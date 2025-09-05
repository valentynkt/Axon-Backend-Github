using System.Diagnostics;
using System.Globalization;
using Axon.Modules.Identity.Application.Commands.EnsureWalletLinked;
using Axon.Modules.Identity.Application.Commands.UpdateProfile;
using Axon.Modules.Identity.Application.Commands.UpsertPrincipalFromCredential;
using Axon.Modules.Identity.Application.Commands.UpsertWalletActivity;
using Axon.Modules.Identity.Application.Common.Constants;
using Axon.Modules.Identity.Application.DTOs.Exchange;
using Axon.Modules.Identity.Application.DTOs.Responses.CommandResponses;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Application.Services;

/// <summary>
/// Orchestrates the complete Dynamic JWT exchange flow with command coordination.
/// 
/// Implements a 4-step process:
/// 1. JWT validation and normalization via DynamicJwtBridge
/// 2. Principal and credential upsert with conflict handling
/// 3. Wallet processing (activity, linking, defaults) via WalletProcessorService
/// 4. Optional profile updates based on JWT claims
/// 
/// Designed for high reliability with soft-failure handling for wallet operations
/// and comprehensive structured logging throughout the process.
/// </summary>
public sealed class DynamicAuthOrchestrator : IDynamicAuthOrchestrator
{
    private readonly IDynamicJwtBridge _jwtBridge;
    private readonly IDynamicToCommandsMapper _mapper;
    private readonly IWalletProcessorService _walletProcessor;
    private readonly IMediator _mediator;
    private readonly IExchangeMetricsService _metricsService;
    private readonly ILogger<DynamicAuthOrchestrator> _logger;

    public DynamicAuthOrchestrator(
        IDynamicJwtBridge jwtBridge,
        IDynamicToCommandsMapper mapper,
        IWalletProcessorService walletProcessor,
        IMediator mediator,
        IExchangeMetricsService metricsService,
        ILogger<DynamicAuthOrchestrator> logger)
    {
        _jwtBridge = jwtBridge ?? throw new ArgumentNullException(nameof(jwtBridge));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _walletProcessor = walletProcessor ?? throw new ArgumentNullException(nameof(walletProcessor));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<ExchangeOutcome, Error>> ExchangeAsync(
        string jwt, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jwt))
        {
            return Result.Failure<ExchangeOutcome, Error>(
                Error.Validation("JWT token is required", DynamicAuthConstants.ErrorCodes.TokenRequired));
        }

        _logger.LogDebug("Starting Dynamic JWT exchange flow");
        
        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Step 1: Validate and normalize JWT token
            var validationResult = await _jwtBridge.ValidateAndNormalizeAsync(jwt, cancellationToken);
            if (validationResult.IsFailure)
            {
                _logger.LogWarning("JWT validation failed: {Error}", validationResult.Error.Message);
                return Result.Failure<ExchangeOutcome, Error>(validationResult.Error);
            }

            var userData = validationResult.Value;
            var correlationId = Guid.NewGuid().ToString("N");
            
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["UserId"] = userData.UserId,
                ["EnvironmentId"] = userData.EnvironmentId
            });
            
            _logger.LogInformation("Processing Dynamic user {UserId} with {WalletCount} wallets (CorrelationId: {CorrelationId})", 
                userData.UserId, userData.Wallets.Count, correlationId);

            // Step 2: Upsert principal and credential
            var principalResult = await UpsertPrincipalFromCredentialAsync(userData, correlationId, cancellationToken);
            if (principalResult.IsFailure)
            {
                _logger.LogError("Principal upsert failed: {Error}", principalResult.Error.Message);
                return Result.Failure<ExchangeOutcome, Error>(principalResult.Error);
            }

            var upsertResponse = principalResult.Value;
            var axonId = upsertResponse.Principal.AxonId;
            var created = upsertResponse.Status == UpsertPrincipalStatus.Created;

            // Track which chains already have defaults (from principal data)
            var seenChainsWithDefault = new HashSet<string>(upsertResponse.Principal.DefaultPerChain.Keys);

            // Step 3: Process all wallets (activity + linking + defaults)
            var walletResults = await _walletProcessor.ProcessWalletsAsync(
                userData.Wallets, 
                axonId, 
                seenChainsWithDefault,
                correlationId, 
                cancellationToken);

            // Step 4: Update profile if needed (currently no-op)
            await UpdateProfileIfNeededAsync(axonId, userData, upsertResponse.Principal.PreferredLanguage, correlationId, cancellationToken);

            // Step 5: Build final outcome
            var outcome = new ExchangeOutcome(
                AxonId: axonId,
                Created: created,
                WalletsProcessed: userData.Wallets.Count,
                WalletsLinked: walletResults.LinkedCount,
                DefaultsApplied: walletResults.DefaultsApplied,
                Skipped: walletResults.SkippedCount,
                Conflicts: walletResults.ConflictsCount
            );

            stopwatch.Stop();
            _metricsService.RecordExchangeSuccess(userData.UserId, outcome.Created, outcome.WalletsProcessed, outcome.WalletsLinked, outcome.Conflicts, stopwatch.ElapsedMilliseconds);
            
            _logger.LogInformation("Exchange completed for user {UserId} in {ElapsedMs}ms: Created={Created}, WalletsProcessed={WalletsProcessed}, WalletsLinked={WalletsLinked}, DefaultsApplied={DefaultsApplied}, Skipped={Skipped}, Conflicts={Conflicts}", 
                userData.UserId, stopwatch.ElapsedMilliseconds, outcome.Created, outcome.WalletsProcessed, outcome.WalletsLinked, outcome.DefaultsApplied, outcome.Skipped, outcome.Conflicts);
            return Result.Success<ExchangeOutcome, Error>(outcome);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metricsService.RecordExchangeFailure(DynamicAuthConstants.ErrorCodes.ExchangeError, "External", stopwatch.ElapsedMilliseconds);
            
            _logger.LogError(ex, "Unexpected error during Dynamic JWT exchange");
            return Result.Failure<ExchangeOutcome, Error>(
                Error.External("JWT exchange failed", DynamicAuthConstants.ErrorCodes.ExchangeError, ex));
        }
    }

    #region Private Methods

    /// <summary>
    /// Step 2: Upsert principal from credential
    /// </summary>
    private async Task<Result<UpsertPrincipalResponse, Error>> UpsertPrincipalFromCredentialAsync(
        ExchangeUserData userData, 
        string correlationId, 
        CancellationToken cancellationToken)
    {
        var command = _mapper.MapToUpsertPrincipalCommand(userData, correlationId);
        
        var result = await _mediator.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            // Check for conflicts (shouldn't happen in credential path)
            if (result.Error.Code == "IDENTITY.WALLET_OWNERSHIP_CONFLICT")
            {
                _logger.LogWarning("Unexpected ownership conflict during principal upsert for user {UserId}", userData.UserId);
                return Result.Failure<UpsertPrincipalResponse, Error>(
                    Error.Conflict("Principal creation conflicted with existing wallet ownership", DynamicAuthConstants.ErrorCodes.PrincipalConflict));
            }
            return Result.Failure<UpsertPrincipalResponse, Error>(result.Error);
        }

        _logger.LogDebug("Principal upserted with status {Status} for user {UserId}, AxonId={AxonId}", 
            result.Value.Status, userData.UserId, result.Value.Principal.AxonId);
        return Result.Success<UpsertPrincipalResponse, Error>(result.Value);
    }



    /// <summary>
    /// Step 4: Update profile if language differs (currently no-op)
    /// </summary>
    private async Task UpdateProfileIfNeededAsync(
        string axonId, 
        ExchangeUserData userData, 
        string currentLanguage,
        string correlationId, 
        CancellationToken cancellationToken)
    {
        var command = _mapper.MapToUpdateProfileCommand(AxonId.Parse(axonId, CultureInfo.InvariantCulture), userData, currentLanguage, correlationId);
        if (command == null)
        {
            // No profile update needed
            return;
        }

        try
        {
            var result = await _mediator.Send(command, cancellationToken);
            if (result.IsSuccess)
            {
                _logger.LogDebug("Profile updated for user {UserId} with status {Status}", 
                    userData.UserId, result.Value.Status);
            }
            else
            {
                // Profile update failures are non-critical - log and continue
                _logger.LogWarning("Profile update failed for user {UserId}: {Error}", 
                    userData.UserId, result.Error.Message);
            }
        }
        catch (Exception ex)
        {
            // Profile update failures are non-critical - log and continue
            _logger.LogWarning(ex, "Unexpected error during profile update for user {UserId}", userData.UserId);
        }
    }

    #endregion

}