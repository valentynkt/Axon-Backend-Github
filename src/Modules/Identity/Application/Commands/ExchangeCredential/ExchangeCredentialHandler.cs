using System.Globalization;
using Axon.Modules.Identity.Application.Common.Commands;
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

        if (string.IsNullOrWhiteSpace(command.BearerToken))
        {
            _logger.LogWarning("Exchange command received with empty bearer token");
            return Result.Failure<ExchangeOutcome, Error>(
                Error.Validation("Bearer token is required", "AUTH.TOKEN_REQUIRED"));
        }

        _logger.LogInformation("Starting Dynamic token exchange via orchestrator");

        // Delegate EVERYTHING to the orchestrator
        var exchangeResult = await _orchestrator.ExchangeDynamicTokenAsync(
            command.BearerToken,
            cancellationToken);

        if (exchangeResult.IsFailure)
        {
            _logger.LogWarning("Dynamic token exchange failed: {Error}", exchangeResult.Error.Message);
            return Result.Failure<ExchangeOutcome, Error>(exchangeResult.Error);
        }

        var response = exchangeResult.Value;

        // Convert AuthenticationResponse to ExchangeOutcome
        // The orchestrator should return all the necessary data
        var outcome = new ExchangeOutcome(
            AccessToken: response.AccessToken,
            TokenType: "Bearer",
            ExpiresIn: (int)(response.ExpiresAt - DateTime.UtcNow).TotalSeconds,
            AxonUserId: new AxonUserId(response.UserId),
            Created: response.AdditionalData?.ContainsKey("created") == true && (bool)response.AdditionalData["created"],
            WalletsProcessed: response.AdditionalData?.ContainsKey("wallets_processed") == true
                ? Convert.ToInt32(response.AdditionalData["wallets_processed"], CultureInfo.InvariantCulture) : 0,
            WalletsLinked: response.AdditionalData?.ContainsKey("wallets_linked") == true
                ? Convert.ToInt32(response.AdditionalData["wallets_linked"], CultureInfo.InvariantCulture) : 0,
            DefaultsApplied: response.AdditionalData?.ContainsKey("defaults_applied") == true
                ? Convert.ToInt32(response.AdditionalData["defaults_applied"], CultureInfo.InvariantCulture) : 0,
            Skipped: response.AdditionalData?.ContainsKey("skipped") == true
                ? Convert.ToInt32(response.AdditionalData["skipped"], CultureInfo.InvariantCulture) : 0,
            Conflicts: response.AdditionalData?.ContainsKey("conflicts") == true
                ? Convert.ToInt32(response.AdditionalData["conflicts"], CultureInfo.InvariantCulture) : 0);

        _logger.LogInformation("Dynamic token exchange successful for principal {PrincipalId}", response.UserId);

        return Result.Success<ExchangeOutcome, Error>(outcome);
    }
}