using Axon.Modules.Identity.Application.Commands.ExchangeCredential;
using Axon.Modules.Identity.Application.Common.Commands;
using Axon.Modules.Identity.Application.Contracts.ExternalServices;
using Axon.Modules.Identity.Application.DTOs.Exchange;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;

namespace Axon.Modules.Identity.Application.Commands.ExchangeToken;

/// <summary>
/// Handler for exchanging Dynamic JWT tokens for Axon identity.
/// Validates the JWT using IDynamicAuthService, maps the data to ExchangeUserData,
/// and delegates to ExchangeCredentialCommand for the core business logic.
/// </summary>
public sealed class ExchangeTokenCommandHandler : BaseIdentityCommandHandler<ExchangeTokenCommand, ExchangeOutcome>
{
    private readonly IDynamicAuthService _dynamicAuthService;
    private readonly IMediator _mediator;

    public ExchangeTokenCommandHandler(
        ICurrentUserService currentUserService,
        IDynamicAuthService dynamicAuthService,
        IMediator mediator) 
        : base(currentUserService)
    {
        _dynamicAuthService = dynamicAuthService ?? throw new ArgumentNullException(nameof(dynamicAuthService));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    public override async Task<Result<ExchangeOutcome, Error>> Handle(ExchangeTokenCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Jwt))
        {
            return Result.Failure<ExchangeOutcome, Error>(
                Error.Validation("JWT token is required", "JWT_REQUIRED"));
        }

        // Step 1: Validate JWT using Dynamic Auth Service
        var validationResult = await _dynamicAuthService.ValidateTokenAsync(command.Jwt, cancellationToken);
        if (validationResult.IsFailure)
        {
            return Result.Failure<ExchangeOutcome, Error>(
                Error.Unauthorized("Invalid or expired JWT token", "JWT_VALIDATION_FAILED"));
        }

        var dynamicUserData = validationResult.Value;

        // Step 2: Map DynamicUserData to ExchangeUserData
        var exchangeUserData = MapToExchangeUserData(dynamicUserData);

        // Step 3: Delegate to ExchangeCredentialCommand for business logic
        var exchangeCommand = new ExchangeCredentialCommand(exchangeUserData);
        var exchangeResult = await _mediator.Send(exchangeCommand, cancellationToken);

        // Step 4: Return the result from ExchangeCredentialCommand
        return exchangeResult;
    }

    /// <summary>
    /// Maps DynamicUserData from JWT validation to ExchangeUserData for internal processing
    /// </summary>
    private static ExchangeUserData MapToExchangeUserData(DynamicUserData dynamicData)
    {
        var wallets = dynamicData.Wallets
            .Select(w => new ExchangeWalletData(
                Address: w.Address,
                Chain: w.Chain,
                WalletName: w.WalletName,
                Provider: w.Provider,
                ConnectedAtUtc: w.ConnectedAtUtc))
            .ToList();

        return new ExchangeUserData(
            UserId: dynamicData.UserId,
            Email: dynamicData.Email,
            EnvironmentId: dynamicData.EnvironmentId,
            Wallets: wallets,
            FirstVisitUtc: dynamicData.FirstVisitUtc,
            LastVisitUtc: dynamicData.LastVisitUtc,
            IsNewUser: dynamicData.IsNewUser,
            AdditionalMetadata: dynamicData.VerifiedCredentialsHashes);
    }
}