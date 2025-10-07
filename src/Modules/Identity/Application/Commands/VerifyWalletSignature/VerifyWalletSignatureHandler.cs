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

        // Step 1: Extract audience from signed message for validation
        string audience;
        try
        {
            using var doc = JsonDocument.Parse(command.SignedMessage);

            if (!doc.RootElement.TryGetProperty("aud", out var audElement))
            {
                _logger.LogWarning("Invalid challenge format: missing 'aud' field");
                return Result.Failure<VerifyWalletSignatureResult, Error>(
                    Error.Validation("Invalid challenge format: missing 'aud' field", "AUTH.INVALID_CHALLENGE"));
            }

            audience = audElement.GetString() ?? string.Empty;

            if (string.IsNullOrEmpty(audience))
            {
                _logger.LogWarning("Invalid challenge format: empty 'aud' field");
                return Result.Failure<VerifyWalletSignatureResult, Error>(
                    Error.Validation("Invalid challenge format: empty 'aud' field", "AUTH.INVALID_CHALLENGE"));
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in signed message");
            return Result.Failure<VerifyWalletSignatureResult, Error>(
                Error.Validation("Invalid challenge format: malformed JSON", "AUTH.INVALID_CHALLENGE"));
        }

        // Step 2: Validate TTL and canonical message shape
        var validationResult = _orchestrator.ValidateChallenge(
            command.SignedMessage,
            command.ChainId,
            command.Address,
            audience);

        if (validationResult.IsFailure)
        {
            // Log detailed error internally but return generic message to client
            _logger.LogWarning("Challenge validation failed, reason={Reason}", validationResult.Error.Code);
            return Result.Failure<VerifyWalletSignatureResult, Error>(
                Error.Unauthorized("Authentication failed", "AUTH.AUTHENTICATION_FAILED"));
        }

        // Step 3: Verify Ed25519 signature
        var baseChain = ChainIdConverter.ExtractBaseChain(command.ChainId);
        var signatureResult = _signatureVerifier.VerifySignature(
            baseChain,
            command.Address,
            command.SignedMessage,
            command.Signature);

        if (signatureResult.IsFailure)
        {
            // Log detailed error internally but return generic message to client for security
            _logger.LogWarning("Signature verification failed for chain={Chain}, reason={Reason}",
                command.ChainId, signatureResult.Error.Code);
            return Result.Failure<VerifyWalletSignatureResult, Error>(
                Error.Unauthorized("Authentication failed", "AUTH.AUTHENTICATION_FAILED"));
        }

        // Step 4: Normalize address
        var normalizedAddressResult = _addressNormalizer.NormalizeAddress(
            baseChain, command.Address);

        if (normalizedAddressResult.IsFailure)
        {
            return Result.Failure<VerifyWalletSignatureResult, Error>(normalizedAddressResult.Error);
        }

        var normalizedAddress = normalizedAddressResult.Value.Value;

        // Step 5: Use orchestrator to complete authentication
        var authRequest = new WalletAuthenticationRequest(
            ChainId: command.ChainId,
            Address: normalizedAddress,
            SignedMessage: command.SignedMessage,
            Signature: command.Signature);

        var authResult = await _orchestrator.AuthenticateWithWalletAsync(authRequest, ct);

        if (authResult.IsFailure)
        {
            return Result.Failure<VerifyWalletSignatureResult, Error>(authResult.Error);
        }

        var response = authResult.Value;

        return Result.Success<VerifyWalletSignatureResult, Error>(
            new VerifyWalletSignatureResult(
                AccessToken: response.AccessToken,
                RefreshToken: response.RefreshToken,
                TokenType: "Bearer",
                ExpiresIn: (int)(response.ExpiresAt - DateTime.UtcNow).TotalSeconds,
                AxonUserId: response.UserId.ToString(),
                Created: response.AdditionalData?.ContainsKey("created") == true && (bool)response.AdditionalData["created"],
                WalletsLinked: 1,
                Conflicts: 0));
    }
}