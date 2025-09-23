using System.Text.Json;
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
    private readonly IAuthenticationService _authService;
    private readonly IWalletSignatureVerifier _signatureVerifier;
    private readonly IPrincipalResolutionService _principalResolver;
    private readonly IAddressNormalizationService _addressNormalizer;
    private readonly ILogger<VerifyWalletSignatureHandler> _logger;

    public VerifyWalletSignatureHandler(
        IAuthenticationService authService,
        IWalletSignatureVerifier signatureVerifier,
        IPrincipalResolutionService principalResolver,
        IAddressNormalizationService addressNormalizer,
        ILogger<VerifyWalletSignatureHandler> logger)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
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
        var macResult = _authService.ValidateMac(
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
        var validationResult = _authService.ValidateChallenge(
            command.SignedMessage,
            command.ChainId,
            command.Address,
            audience);

        if (validationResult.IsFailure)
        {
            return Result.Failure<VerifyWalletSignatureResult, Error>(validationResult.Error);
        }

        // Step 4: Check replay protection
        var replayResult = await _authService.CheckAndMarkNonceUsedAsync(
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

        // Step 7: Resolve principal
        var chainId = ChainId.Create(command.ChainId).Value;
        var address = Address.Create(normalizedAddress).Value;

        var principalResult = await _principalResolver.ResolveAsync(
            ProviderType.Siws,  // Use Siws for signature-based sign-in
            $"siws:{baseChain}",
            normalizedAddress,
            chainId,
            address,
            ct);

        if (principalResult.IsFailure)
        {
            return Result.Failure<VerifyWalletSignatureResult, Error>(principalResult.Error);
        }

        var principal = principalResult.Value.Principal;
        var wasCreated = principalResult.Value.Path == ResolutionPath.Created;

        // Step 8: Generate Axon JWT (uses configuration default expiration)
        var tokenResult = await _authService.GenerateAccessTokenAsync(
            new AxonUserId(principal.Id.Value),
            ProviderType.Siws,
            $"siws:{command.ChainId}",
            normalizedAddress,
            -1, // Use configuration default
            ct);

        if (tokenResult.IsFailure)
        {
            return Result.Failure<VerifyWalletSignatureResult, Error>(tokenResult.Error);
        }

        return Result.Success<VerifyWalletSignatureResult, Error>(
            new VerifyWalletSignatureResult(
                AccessToken: tokenResult.Value.AccessToken,
                TokenType: "Bearer",
                ExpiresIn: tokenResult.Value.ExpiresIn,
                AxonUserId: principal.Id.Value.ToString(),
                Created: wasCreated,
                WalletsLinked: 1,
                Conflicts: 0));
    }
}