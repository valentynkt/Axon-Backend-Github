using System.Globalization;
using Axon.Modules.Identity.Application.Common.Commands;
using Axon.Modules.Identity.Application.Contracts.Providers;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Application.DTOs.Exchange;
using global::BuildingBlocks.Core.Abstractions.Authentication;
using global::BuildingBlocks.Core.Diagnostics.Errors;
using global::BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Application.Commands.ExchangeCredential;

/// <summary>
/// Handler for ExchangeCredentialCommand that delegates all processing to AuthenticationOrchestrator.
/// This handler is a thin wrapper that converts the orchestrator response to ExchangeOutcome.
/// </summary>
public sealed class ExchangeCredentialHandler : BaseIdentityCommandHandler<ExchangeCredentialCommand, ExchangeOutcome>
{
    private readonly IAuthenticationOrchestrator _orchestrator;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<ExchangeCredentialHandler> _logger;

    public ExchangeCredentialHandler(
        ICurrentUserService currentUserService,
        IAuthenticationOrchestrator orchestrator,
        IJwtTokenService jwtTokenService,
        ILogger<ExchangeCredentialHandler> logger)
        : base(currentUserService)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _jwtTokenService = jwtTokenService ?? throw new ArgumentNullException(nameof(jwtTokenService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task<Result<ExchangeOutcome, Error>> Handle(
        ExchangeCredentialCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var handlerCallId = Guid.NewGuid();
        _logger.LogInformation("HANDLER CALLED: Call ID={CallId}, Command={Command}", handlerCallId, command.GetType().Name);

        if (string.IsNullOrWhiteSpace(command.BearerToken))
        {
            _logger.LogWarning("Exchange command received with empty bearer token");
            return Result.Failure<ExchangeOutcome, Error>(
                Error.Unauthorized("Bearer token is required", "AUTH.TOKEN_REQUIRED"));
        }

        _logger.LogInformation("Starting Dynamic token exchange via orchestrator (CallID={CallId})", handlerCallId);

        // Delegate EVERYTHING to the orchestrator
        Result<AuthenticationResponse, Error> exchangeResult;
        try
        {
            exchangeResult = await _orchestrator.ExchangeDynamicTokenAsync(
                command.BearerToken,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CRITICAL: Unhandled exception in ExchangeDynamicTokenAsync - Type: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                ex.GetType().FullName, ex.Message, ex.StackTrace);

            // Log inner exception if present
            if (ex.InnerException != null)
            {
                _logger.LogError("Inner Exception - Type: {InnerType}, Message: {InnerMessage}, StackTrace: {InnerStackTrace}",
                    ex.InnerException.GetType().FullName, ex.InnerException.Message, ex.InnerException.StackTrace);
            }

            return Result.Failure<ExchangeOutcome, Error>(
                Error.Internal($"Token exchange failed with exception: {ex.Message}", "AUTH.EXCHANGE_EXCEPTION"));
        }

        if (exchangeResult.IsFailure)
        {
            _logger.LogWarning("Dynamic token exchange failed: {Error}", exchangeResult.Error.Message);
            return Result.Failure<ExchangeOutcome, Error>(exchangeResult.Error);
        }

        var response = exchangeResult.Value;

        // Convert AuthenticationResponse to ExchangeOutcome
        // The orchestrator should return all the necessary data including refresh token
        var outcome = new ExchangeOutcome(
            AccessToken: response.AccessToken,
            RefreshToken: response.RefreshToken,
            TokenType: "Bearer",
            ExpiresIn: (int)(response.ExpiresAt - DateTime.UtcNow).TotalSeconds,
            AxonUserId: new AxonUserId(response.UserId),
            Created: GetBoolValue(response.AdditionalData, "created", false),
            WalletsProcessed: GetIntValue(response.AdditionalData, "wallets_processed", 0),
            WalletsLinked: GetIntValue(response.AdditionalData, "wallets_linked", 0),
            DefaultsApplied: GetIntValue(response.AdditionalData, "defaults_applied", 0),
            Skipped: GetIntValue(response.AdditionalData, "skipped", 0),
            Conflicts: GetIntValue(response.AdditionalData, "conflicts", 0));

        _logger.LogInformation("Dynamic token exchange successful for principal {PrincipalId}", response.UserId);

        return Result.Success<ExchangeOutcome, Error>(outcome);
    }

    /// <summary>
    /// Safely extracts an integer value from additional data dictionary
    /// </summary>
    private static int GetIntValue(Dictionary<string, object>? additionalData, string key, int defaultValue)
    {
        if (additionalData?.TryGetValue(key, out var value) == true && value != null)
        {
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        return defaultValue;
    }

    /// <summary>
    /// Safely extracts a boolean value from additional data dictionary
    /// </summary>
    private static bool GetBoolValue(Dictionary<string, object>? additionalData, string key, bool defaultValue)
    {
        if (additionalData?.TryGetValue(key, out var value) == true && value != null)
        {
            return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        }
        return defaultValue;
    }
}