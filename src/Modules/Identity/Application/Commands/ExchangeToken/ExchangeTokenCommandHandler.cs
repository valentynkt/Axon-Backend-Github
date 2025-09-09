using Axon.Modules.Identity.Application.Common.Commands;
using Axon.Modules.Identity.Application.DTOs.Exchange;
using Axon.Modules.Identity.Application.Services;
using BuildingBlocks.Application.Services;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Application.Commands.ExchangeToken;

/// <summary>
/// Handler for exchanging Dynamic JWT tokens for Axon identity
/// </summary>
public sealed class ExchangeTokenCommandHandler : BaseIdentityCommandHandler<ExchangeTokenCommand, ExchangeOutcome>
{
    private readonly IDynamicAuthOrchestrator _dynamicAuthOrchestrator;

    public ExchangeTokenCommandHandler(
        ICurrentUserService currentUserService,
        IDynamicAuthOrchestrator dynamicAuthOrchestrator) 
        : base(currentUserService)
    {
        _dynamicAuthOrchestrator = dynamicAuthOrchestrator ?? throw new ArgumentNullException(nameof(dynamicAuthOrchestrator));
    }

    public override async Task<Result<ExchangeOutcome, Error>> Handle(ExchangeTokenCommand command, CancellationToken cancellationToken)
    {
        return await _dynamicAuthOrchestrator.ExchangeAsync(command.Jwt, cancellationToken);
    }
}