using Axon.Modules.Identity.Application.Contracts.Services;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Application.Commands.GenerateChallenge;

public sealed class GenerateChallengeHandler : IRequestHandler<GenerateChallengeCommand, Result<GenerateChallengeResult, Error>>
{
    private readonly IAuthenticationService _authenticationService;
    private readonly IAddressNormalizationService _addressNormalizationService;
    private readonly ILogger<GenerateChallengeHandler> _logger;

    public GenerateChallengeHandler(
        IAuthenticationService authenticationService,
        IAddressNormalizationService addressNormalizationService,
        ILogger<GenerateChallengeHandler> logger)
    {
        _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
        _addressNormalizationService = addressNormalizationService ?? throw new ArgumentNullException(nameof(addressNormalizationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<GenerateChallengeResult, Error>> Handle(
        GenerateChallengeCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Apply address normalization at application layer per Story 5.3 AC#13
        var normalizedAddressResult = _addressNormalizationService.NormalizeAddress(request.ChainId, request.WalletAddress);
        if (normalizedAddressResult.IsFailure)
        {
            _logger.LogWarning("Failed to normalize address {Address} for chain {Chain}: {Error}",
                request.WalletAddress, request.ChainId, normalizedAddressResult.Error.Message);
            return Result.Failure<GenerateChallengeResult, Error>(normalizedAddressResult.Error);
        }

        var normalizedAddress = normalizedAddressResult.Value.Value;

        // Generate challenge via unified authentication service
        var challengeResult = await _authenticationService.GenerateChallengeAsync(
            request.NetworkEnvironment,
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
            NetworkEnvironment: challenge.NetworkEnvironment,
            ChainId: challenge.ChainId,
            Address: challenge.Address,
            IssuedAt: challenge.IssuedAt,
            ExpiresAt: challenge.Exp,
            Nonce: challenge.Nonce,
            Audience: challenge.Aud
        );

        _logger.LogInformation("Generated challenge for {Address} on {Environment}",
            normalizedAddress, challenge.NetworkEnvironment);

        return Result.Success<GenerateChallengeResult, Error>(result);
    }
}