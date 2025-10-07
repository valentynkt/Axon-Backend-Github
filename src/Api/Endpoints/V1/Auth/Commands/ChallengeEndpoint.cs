using Axon.Api.Contracts.V1.Auth;
using Axon.Api.Validators.V1.Auth;
using Axon.Modules.Identity.Application.Commands.GenerateChallenge;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Utilities;
using BuildingBlocks.Web.Endpoints.Base;
using CSharpFunctionalExtensions;
using FastEndpoints;
using MediatR;

namespace Axon.Api.Endpoints.V1.Auth.Commands;

/// <summary>
/// POST /auth/challenge - Generate canonical message for wallet authentication.
/// Returns a canonical message that the wallet must sign.
/// </summary>
public sealed class ChallengeEndpoint : BaseResultEndpoint<ChallengeRequestDto, ChallengeResponseDto>
{
    private readonly IMediator _mediator;

    public ChallengeEndpoint(IMediator mediator, ILogger<ChallengeEndpoint> logger)
        : base(logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    public override void Configure()
    {
        Post("/api/v1/auth/challenge");
        AllowAnonymous(); // No authentication required for challenge generation

        Validator<ChallengeRequestDtoValidator>();

        // Apply rate limiting policy for challenge endpoint
        Options(x => x.RequireRateLimiting("AuthChallenge"));

        Summary(s =>
        {
            s.Summary = "Generate wallet authentication challenge";
            s.Description = """
                Generates a canonical challenge message for wallet authentication.

                The returned message must be signed by the wallet and then submitted to the exchange endpoint.

                **Behavior**:
                • Rate Limited: 10 requests/min/IP (configured globally)
                • Message Format: Canonical JSON with strict field ordering
                • TTL: up to 5 minutes (300 seconds)
                • Environments: mainnet, devnet, testnet
                """;
            s.Responses[200] = "Returns canonical challenge message for signing";
            s.Responses[400] = "Invalid request parameters";
            s.Responses[422] = "Business rule violation";
            s.Responses[429] = "Too Many Requests - Rate limit exceeded";
            s.Responses[500] = "Internal server error";
        });

        Tags("Authentication");
    }

    protected override async Task<Result<ChallengeResponseDto, Error>> ExecuteAsync(
        ChallengeRequestDto request,
        CancellationToken ct)
    {
        // Validate required fields
        var rawChainId = request.ChainId.Trim();
        var rawAddress = request.WalletAddress.Trim();

        if (string.IsNullOrWhiteSpace(rawAddress) || string.IsNullOrWhiteSpace(rawChainId))
        {
            return Result.Failure<ChallengeResponseDto, Error>(
                Error.Validation("Chain ID and wallet address are required"));
        }

        // Convert to compound chainId format (e.g., "solana" -> "solana-mainnet")
        var compoundChainId = ChainIdConverter.ConvertToCompoundChainId(rawChainId);

        var audience = string.IsNullOrWhiteSpace(request.Audience) ? null : request.Audience.Trim();

        // Prevent caching of auth responses
        HttpContext.Response.Headers.CacheControl = "no-store";
        HttpContext.Response.Headers.Pragma = "no-cache";

        // Create and send command
        var command = new GenerateChallengeCommand(
            ChainId: compoundChainId,
            WalletAddress: rawAddress,
            Audience: audience
        );

        var domainResult = await _mediator.Send(command, ct);
        if (domainResult.IsFailure)
            return Result.Failure<ChallengeResponseDto, Error>(domainResult.Error);

        // Map domain result to response
        var response = new ChallengeResponseDto(
            Message: domainResult.Value.Message,
            ChainId: domainResult.Value.ChainId,
            Address: domainResult.Value.Address,
            IssuedAt: domainResult.Value.IssuedAt,
            ExpiresAt: domainResult.Value.ExpiresAt,
            Nonce: domainResult.Value.Nonce,
            Audience: domainResult.Value.Audience
        );

        return Result.Success<ChallengeResponseDto, Error>(response);
    }
}
