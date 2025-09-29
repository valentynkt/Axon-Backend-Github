using Axon.Api.Contracts.V1.Auth;
using Axon.Api.Modules;
using Axon.Modules.Identity.Application.Commands.VerifyWalletSignature;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Utilities;
using CSharpFunctionalExtensions;
using MediatR;

namespace Axon.Api.Endpoints.V1.Auth.Commands;

/// <summary>
/// POST /auth/verify - Verify wallet signature and obtain access token.
/// Verifies a wallet signature against a challenge message and issues an Axon JWT.
/// </summary>
public sealed class VerifySignatureEndpoint
    : BaseIdentityCommandEndpoint<
        VerifySignatureRequestDto,
        AuthTokenResponseDto,
        VerifyWalletSignatureCommand,
        VerifyWalletSignatureResult>
{
    public VerifySignatureEndpoint(IMediator mediator, ILogger<VerifySignatureEndpoint> logger)
        : base(mediator, logger)
    {
    }

    public override void Configure()
    {
        base.Configure();

        // Apply rate limiting
        Options(x => x.RequireRateLimiting("AuthVerify"));

        // Update summary and responses for verify endpoint
        Summary(s =>
        {
            s.Summary = GetSummary();
            s.Description = GetDescription();
            s.Responses[200] = GetSuccessResponse();
            s.Responses[400] = "Invalid request or expired challenge";
            s.Responses[401] = "Invalid signature or MAC";
            s.Responses[409] = "Replay attempt or ownership conflict";
            s.Responses[422] = "Business rule violation";
            s.Responses[429] = "Rate limit exceeded";
            s.Responses[500] = "Internal server error";
        });
    }

    protected override string GetRoute() => "/api/v1/auth/verify";
    protected override string GetSummary() => "Verify wallet signature and obtain access token";
    protected override string GetDescription() =>
        """
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
    protected override string GetSuccessResponse() => "Returns access token and user information";

    protected override Task<Result<VerifyWalletSignatureCommand, Error>> ExecuteCommand(
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

        return Task.FromResult(Result.Success<VerifyWalletSignatureCommand, Error>(command));
    }

    protected override Task<Result<AuthTokenResponseDto, Error>> MapDomainToResponseAsync(VerifyWalletSignatureResult result, CancellationToken ct)
    {
        var response = new AuthTokenResponseDto(
            AccessToken: result.AccessToken,
            TokenType: result.TokenType,
            ExpiresIn: result.ExpiresIn,
            AxonUserId: result.AxonUserId,
            Created: result.Created,
            WalletsLinked: result.WalletsLinked,
            Conflicts: result.Conflicts);

        return Task.FromResult(Result.Success<AuthTokenResponseDto, Error>(response));
    }

}