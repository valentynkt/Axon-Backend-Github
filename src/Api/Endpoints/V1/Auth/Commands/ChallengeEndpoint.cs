using Axon.Api.Contracts.V1.Auth;
using Axon.Api.Validators.V1.Auth;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Infrastructure.Services;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Web.Endpoints.Base;
using BuildingBlocks.Web.Extensions;
using CSharpFunctionalExtensions;

namespace Axon.Api.Endpoints.V1.Auth.Commands;

/// <summary>
/// POST /auth/challenge - Generate canonical message for wallet authentication.
/// Returns a canonical message that the wallet must sign.
/// </summary>
public sealed class ChallengeEndpoint : BaseResultEndpoint<ChallengeRequestDto, ChallengeResponseDto>
{
    private readonly ICanonicalMessageService _canonicalMessageService;

    public ChallengeEndpoint(ICanonicalMessageService canonicalMessageService, ILogger<ChallengeEndpoint> logger)
        : base(logger)
    {
        _canonicalMessageService = canonicalMessageService ?? throw new ArgumentNullException(nameof(canonicalMessageService));
    }

    public override void Configure()
    {
        Post("/api/v1/auth/challenge");
        AllowAnonymous(); // Public endpoint for generating challenges
        Validator<ChallengeRequestDtoValidator>();

        Summary(s =>
        {
            s.Summary = "Generate wallet authentication challenge";
            s.Description = """
                Generates a canonical challenge message for wallet authentication.

                The returned message must be signed by the wallet and then submitted to the exchange endpoint.

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
        LogRequestReceived();

        // Map/validate environment (DTO uses string "Environment" -> VO)
        var envResult = NetworkEnvironment.Create(request.NetworkEnvironment);
        if (envResult.IsFailure)
        {
            Logger.LogWarning("Invalid environment: {Environment}", request.NetworkEnvironment);
            return Result.Failure<ChallengeResponseDto, Error>(envResult.Error);
        }

        // Normalize light inputs (keep challenge lenient; hard checks happen at exchange)
        var chainId = request.ChainId?.Trim().ToLowerInvariant();
        var address = request.WalletAddress?.Trim();
        var audience = string.IsNullOrWhiteSpace(request.Audience) ? string.Empty : request.Audience!.Trim();

        // Generate canonical challenge via domain service
        var challengeResult = await _canonicalMessageService.GenerateChallengeAsync(
            envResult.Value,
            chainId!,
            address!,
            audience,
            ct);

        if (challengeResult.IsFailure)
        {
            Logger.LogWarning("Failed to generate challenge: {Error}", challengeResult.Error.Message);
            return Result.Failure<ChallengeResponseDto, Error>(challengeResult.Error);
        }

        var ch = challengeResult.Value;

        // Avoid caching sensitive auth payloads
        HttpContext.Response.Headers.CacheControl = "no-store";
        HttpContext.Response.Headers.Pragma = "no-cache";

        var response = new ChallengeResponseDto(
            Message:      ch.CanonicalJson,
            NetworkEnvironment:  ch.NetworkEnvironment,
            ChainId:      ch.ChainId,
            Address:      ch.Address,
            IssuedAt:     ch.IssuedAt,   // unix seconds
            ExpiresAt:    ch.Exp,  // unix seconds
            Nonce:        ch.Nonce,
            Audience:     ch.Aud
        );

        Logger.LogInformation("Generated challenge for {Address} on {Environment}", address, ch.NetworkEnvironment);
        LogRequestCompleted();

        return Result.Success<ChallengeResponseDto, Error>(response);
    }
}
