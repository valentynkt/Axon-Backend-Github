using Axon.Modules.Identity.Application.Contracts.Services;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Utilities;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Application.Commands.GenerateChallenge;

public sealed class GenerateChallengeHandler : IRequestHandler<GenerateChallengeCommand, Result<GenerateChallengeResult, Error>>
{
    private readonly IAuthenticationOrchestrator _orchestrator;
    private readonly IAddressNormalizationService _addressNormalizationService;
    private readonly ILogger<GenerateChallengeHandler> _logger;

    public GenerateChallengeHandler(
        IAuthenticationOrchestrator orchestrator,
        IAddressNormalizationService addressNormalizationService,
        ILogger<GenerateChallengeHandler> logger)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _addressNormalizationService = addressNormalizationService ?? throw new ArgumentNullException(nameof(addressNormalizationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<GenerateChallengeResult, Error>> Handle(
        GenerateChallengeCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Extract base chain for address normalization
        var baseChain = ChainIdConverter.ExtractBaseChain(request.ChainId);

        // Apply address normalization at application layer per Story 5.3 AC#13
        var normalizedAddressResult = _addressNormalizationService.NormalizeAddress(baseChain, request.WalletAddress);
        if (normalizedAddressResult.IsFailure)
        {
            _logger.LogWarning("Failed to normalize address {Address} for chain {Chain}: {Error}",
                request.WalletAddress, request.ChainId, normalizedAddressResult.Error.Message);
            return Result.Failure<GenerateChallengeResult, Error>(normalizedAddressResult.Error);
        }

        var normalizedAddress = normalizedAddressResult.Value.Value;

        // Generate challenge via orchestrator
        var challengeResult = await _orchestrator.GenerateChallengeAsync(
            request.ChainId,
            normalizedAddress,
            request.Audience ?? string.Empty,
            cancellationToken);

        if (challengeResult.IsFailure)
        {
            _logger.LogWarning("Failed to generate challenge: {Error}", challengeResult.Error.Message);
            return Result.Failure<GenerateChallengeResult, Error>(challengeResult.Error);
        }

        var challenge = challengeResult.Value;

        var result = new GenerateChallengeResult(
            Message: challenge.Message,
            ChainId: challenge.ChainId,
            Address: challenge.Address,
            IssuedAt: challenge.IssuedAt,
            ExpiresAt: challenge.Exp,
            Nonce: challenge.Nonce,
            Audience: challenge.Aud
        );

        _logger.LogInformation("Generated challenge for {Address} on {ChainId}",
            normalizedAddress, request.ChainId);

        return Result.Success<GenerateChallengeResult, Error>(result);
    }
}