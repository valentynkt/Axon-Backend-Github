using Axon.Api.Contracts.V1.Auth;
using Axon.Api.Modules;
using Axon.Api.Validators.V1.Auth;
using Axon.Modules.Identity.Application.Commands.GenerateChallenge;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;

namespace Axon.Api.Endpoints.V1.Auth.Commands;

/// <summary>
/// POST /auth/challenge - Generate canonical message for wallet authentication.
/// Returns a canonical message that the wallet must sign.
/// </summary>
public sealed class ChallengeEndpoint
    : BaseIdentityCommandEndpoint<
        ChallengeRequestDto,
        ChallengeResponseDto,
        GenerateChallengeCommand,
        GenerateChallengeResult>
{
    public ChallengeEndpoint(IMediator mediator, ILogger<ChallengeEndpoint> logger)
        : base(mediator, logger)
    {
    }

    public override void Configure()
    {
        base.Configure();

        Validator<ChallengeRequestDtoValidator>();

        // Apply rate limiting policy for challenge endpoint
        Options(x => x.RequireRateLimiting("AuthChallenge"));

        // Document responses succinctly
        Summary(s =>
        {
            s.Summary = GetSummary();
            s.Description = GetDescription();
            s.Responses[200] = GetSuccessResponse();
            s.Responses[400] = "Invalid request parameters";
            s.Responses[422] = "Business rule violation";
            s.Responses[429] = "Too Many Requests - Rate limit exceeded";
            s.Responses[500] = "Internal server error";
        });
    }

    protected override string GetRoute() => "/api/v1/auth/challenge";
    protected override string GetSummary() => "Generate wallet authentication challenge";
    protected override string GetDescription() =>
        """
        Generates a canonical challenge message for wallet authentication.

        The returned message must be signed by the wallet and then submitted to the exchange endpoint.

        **Behavior**:
        • Rate Limited: 10 requests/min/IP (configured globally)
        • Message Format: Canonical JSON with strict field ordering
        • TTL: up to 5 minutes (300 seconds)
        • Environments: mainnet, devnet, testnet
        """;
    protected override string GetSuccessResponse() => "Returns canonical challenge message for signing";

    protected override Task<Result<GenerateChallengeCommand, Error>> ExecuteCommand(
        ChallengeRequestDto request,
        CancellationToken ct)
    {
        // Validate environment
        var envResult = NetworkEnvironment.Create(request.NetworkEnvironment);
        if (envResult.IsFailure)
        {
            Logger.LogWarning("Invalid environment: {Environment}", request.NetworkEnvironment);
            return Task.FromResult(Result.Failure<GenerateChallengeCommand, Error>(envResult.Error));
        }

        // Validate required fields
        var chainId = request.ChainId.Trim().ToLowerInvariant();
        var rawAddress = request.WalletAddress.Trim();

        if (string.IsNullOrWhiteSpace(rawAddress) || string.IsNullOrWhiteSpace(chainId))
        {
            return Task.FromResult(Result.Failure<GenerateChallengeCommand, Error>(
                Error.Validation("Chain ID and wallet address are required")));
        }

        var audience = string.IsNullOrWhiteSpace(request.Audience) ? null : request.Audience.Trim();

        // Prevent caching of auth responses
        HttpContext.Response.Headers.CacheControl = "no-store";
        HttpContext.Response.Headers.Pragma = "no-cache";

        var command = new GenerateChallengeCommand(
            NetworkEnvironment: envResult.Value,
            ChainId: chainId,
            WalletAddress: rawAddress,
            Audience: audience
        );

        return Task.FromResult(Result.Success<GenerateChallengeCommand, Error>(command));
    }

    protected override Task<Result<ChallengeResponseDto, Error>> MapDomainToResponseAsync(GenerateChallengeResult result, CancellationToken ct)
    {
        var response = new ChallengeResponseDto(
            Message: result.Message,
            NetworkEnvironment: result.NetworkEnvironment,
            ChainId: result.ChainId,
            Address: result.Address,
            IssuedAt: result.IssuedAt,
            ExpiresAt: result.ExpiresAt,
            Nonce: result.Nonce,
            Audience: result.Audience
        );

        return Task.FromResult(Result.Success<ChallengeResponseDto, Error>(response));
    }
}
