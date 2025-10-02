using Axon.Api.Contracts.V1.Auth;
using Axon.Modules.Identity.Application.Commands.VerifyWalletSignature;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Utilities;
using BuildingBlocks.Web.Endpoints.Base;
using CSharpFunctionalExtensions;
using FastEndpoints;
using MediatR;

namespace Axon.Api.Endpoints.V1.Auth.Commands;

/// <summary>
/// POST /auth/verify - Verify wallet signature and obtain access token.
/// Verifies a wallet signature against a challenge message and issues an Axon JWT.
/// </summary>
public sealed class VerifySignatureEndpoint : BaseResultEndpoint<VerifySignatureRequestDto, AuthTokenResponseDto>
{
    private readonly IMediator _mediator;

    public VerifySignatureEndpoint(IMediator mediator, ILogger<VerifySignatureEndpoint> logger)
        : base(logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    public override void Configure()
    {
        Post("/api/v1/auth/verify");
        AllowAnonymous();

        // Apply rate limiting
        Options(x => x.RequireRateLimiting("AuthVerify"));

        Summary(s =>
        {
            s.Summary = "Verify wallet signature and obtain access token";
            s.Description = """
                Verifies a wallet signature against a challenge message and issues an Axon JWT.

                **Flow:**
                1. Validates MAC to ensure challenge integrity
                2. Checks replay protection (nonce cache)
                3. Verifies Ed25519 signature (Solana only in V1)
                4. Resolves or creates Axon principal
                5. Issues access token (15-30 min TTL)

                **Supported Chains:** solana
                **Supported Environments:** mainnet, devnet, testnet
                """;
            s.Responses[200] = "Returns access token and user information";
            s.Responses[400] = "Invalid request or expired challenge";
            s.Responses[401] = "Invalid signature or MAC";
            s.Responses[409] = "Replay attempt or ownership conflict";
            s.Responses[422] = "Business rule violation";
            s.Responses[429] = "Rate limit exceeded";
            s.Responses[500] = "Internal server error";
        });

        Tags("Authentication");
    }

    protected override async Task<Result<AuthTokenResponseDto, Error>> ExecuteAsync(
        VerifySignatureRequestDto request,
        CancellationToken ct)
    {
        // Prevent caching
        HttpContext.Response.Headers.CacheControl = "no-store";
        HttpContext.Response.Headers.Pragma = "no-cache";

        // Convert to compound chainId format (e.g., "solana" -> "solana-mainnet")
        var compoundChainId = ChainIdConverter.ConvertToCompoundChainId(request.ChainId.Trim());

        var command = new VerifyWalletSignatureCommand(
            ChainId: compoundChainId,
            Address: request.Address.Trim(),
            SignedMessage: request.SignedMessage,
            Signature: request.Signature,
            Mac: request.Mac,
            Mkv: request.Mkv);

        var domainResult = await _mediator.Send(command, ct);
        if (domainResult.IsFailure)
            return Result.Failure<AuthTokenResponseDto, Error>(domainResult.Error);

        // Map domain result to response
        var response = new AuthTokenResponseDto(
            AccessToken: domainResult.Value.AccessToken,
            TokenType: domainResult.Value.TokenType,
            ExpiresIn: domainResult.Value.ExpiresIn,
            AxonUserId: domainResult.Value.AxonUserId,
            Created: domainResult.Value.Created,
            WalletsLinked: domainResult.Value.WalletsLinked,
            Conflicts: domainResult.Value.Conflicts);

        return Result.Success<AuthTokenResponseDto, Error>(response);
    }
}
