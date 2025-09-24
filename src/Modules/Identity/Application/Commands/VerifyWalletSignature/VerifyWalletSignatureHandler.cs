using System.Text.Json;
using Axon.Modules.Identity.Application.Contracts.Providers;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Utilities;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Application.Commands.VerifyWalletSignature;

public sealed class VerifyWalletSignatureHandler
    : IRequestHandler<VerifyWalletSignatureCommand, Result<VerifyWalletSignatureResult, Error>>
{
    private readonly IAuthenticationOrchestrator _orchestrator;
    private readonly IWalletSignatureVerifier _signatureVerifier;
    private readonly IPrincipalResolutionService _principalResolver;
    private readonly IAddressNormalizationService _addressNormalizer;
    private readonly ILogger<VerifyWalletSignatureHandler> _logger;

    public VerifyWalletSignatureHandler(
        IAuthenticationOrchestrator orchestrator,
        IWalletSignatureVerifier signatureVerifier,
        IPrincipalResolutionService principalResolver,
        IAddressNormalizationService addressNormalizer,
        ILogger<VerifyWalletSignatureHandler> logger)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _signatureVerifier = signatureVerifier ?? throw new ArgumentNullException(nameof(signatureVerifier));
        _principalResolver = principalResolver ?? throw new ArgumentNullException(nameof(principalResolver));
        _addressNormalizer = addressNormalizer ?? throw new ArgumentNullException(nameof(addressNormalizer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<VerifyWalletSignatureResult, Error>> Handle(
        VerifyWalletSignatureCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Step 1: Validate MAC
        var macResult = _orchestrator.ValidateMac(
            command.SignedMessage, command.Mac, command.Mkv);

        if (macResult.IsFailure)
        {
            _logger.LogWarning("MAC validation failed for mkv={Mkv}", command.Mkv);
            return Result.Failure<VerifyWalletSignatureResult, Error>(macResult.Error);
        }

        // Step 2: Extract audience from signed message for validation
        using var doc = JsonDocument.Parse(command.SignedMessage);
        var audience = doc.RootElement.GetProperty("aud").GetString() ?? string.Empty;

        // Step 3: Validate TTL and canonical message shape
        var validationResult = _orchestrator.ValidateChallenge(
            command.SignedMessage,
            command.ChainId,
            command.Address,
            audience);

        if (validationResult.IsFailure)
        {
            return Result.Failure<VerifyWalletSignatureResult, Error>(validationResult.Error);
        }

        // Step 4: Check replay protection
        var replayResult = await _orchestrator.CheckAndMarkNonceUsedAsync(
            command.SignedMessage, command.Mkv, ct);

        if (replayResult.IsFailure)
        {
            return Result.Failure<VerifyWalletSignatureResult, Error>(replayResult.Error);
        }

        // Step 5: Verify Ed25519 signature
        var baseChain = ChainIdConverter.ExtractBaseChain(command.ChainId);
        var signatureResult = _signatureVerifier.VerifySignature(
            baseChain,
            command.Address,
            command.SignedMessage,
            command.Signature);

        if (signatureResult.IsFailure)
        {
            _logger.LogWarning("Signature verification failed for chain={Chain}", command.ChainId);
            return Result.Failure<VerifyWalletSignatureResult, Error>(signatureResult.Error);
        }

        // Step 6: Normalize address
        var normalizedAddressResult = _addressNormalizer.NormalizeAddress(
            baseChain, command.Address);

        if (normalizedAddressResult.IsFailure)
        {
            return Result.Failure<VerifyWalletSignatureResult, Error>(normalizedAddressResult.Error);
        }

        var normalizedAddress = normalizedAddressResult.Value.Value;

        // Step 7: Use orchestrator to complete authentication
        var authRequest = new WalletAuthenticationRequest(
            ChainId: command.ChainId,
            Address: normalizedAddress,
            SignedMessage: command.SignedMessage,
            Signature: command.Signature,
            Mac: command.Mac,
            Mkv: command.Mkv);

        var authResult = await _orchestrator.AuthenticateWithWalletAsync(authRequest, ct);

        if (authResult.IsFailure)
        {
            return Result.Failure<VerifyWalletSignatureResult, Error>(authResult.Error);
        }

        var response = authResult.Value;

        return Result.Success<VerifyWalletSignatureResult, Error>(
            new VerifyWalletSignatureResult(
                AccessToken: response.AccessToken,
                TokenType: "Bearer",
                ExpiresIn: (int)(response.ExpiresAt - DateTime.UtcNow).TotalSeconds,
                AxonUserId: response.UserId.ToString(),
                Created: response.AdditionalData?.ContainsKey("created") == true && (bool)response.AdditionalData["created"],
                WalletsLinked: 1,
                Conflicts: 0));
    }
}