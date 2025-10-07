using System.Text.Json;
using Axon.Modules.Identity.Application.Contracts.Services;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Implementation of challenge validation service for wallet authentication.
/// Extracted from WalletAuthenticationProvider for improved testability and separation of concerns.
/// </summary>
public sealed class ChallengeValidationService : IChallengeValidationService
{
    private readonly IChallengeService _challengeService;
    private readonly ILogger<ChallengeValidationService> _logger;

    public ChallengeValidationService(
        IChallengeService challengeService,
        ILogger<ChallengeValidationService> logger)
    {
        _challengeService = challengeService ?? throw new ArgumentNullException(nameof(challengeService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<bool, Error>> ValidateWalletChallengeAsync(
        string signedMessage,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(signedMessage))
        {
            return Result.Failure<bool, Error>(Error.Validation("Signed message cannot be empty"));
        }

        try
        {
            // Step 1: Parse JSON challenge message
            using var doc = JsonDocument.Parse(signedMessage);
            var root = doc.RootElement;

            // Extract required fields
            var chainId = root.TryGetProperty("chain_id", out var chainIdElement)
                ? chainIdElement.GetString()
                : null;
            var address = root.TryGetProperty("wallet_address", out var addressElement)
                ? addressElement.GetString()
                : null;
            var audience = root.TryGetProperty("aud", out var audElement)
                ? audElement.GetString()
                : "axon-challenge";

            // Validate required fields are present
            if (string.IsNullOrEmpty(chainId))
            {
                _logger.LogWarning("Challenge validation failed: missing chain_id");
                return Result.Failure<bool, Error>(
                    Error.Validation("Challenge message must contain chain_id", "CHALLENGE.MISSING_CHAIN_ID"));
            }

            if (string.IsNullOrEmpty(address))
            {
                _logger.LogWarning("Challenge validation failed: missing wallet_address");
                return Result.Failure<bool, Error>(
                    Error.Validation("Challenge message must contain wallet_address", "CHALLENGE.MISSING_ADDRESS"));
            }

            // Step 2: Validate challenge structure and TTL
            var challengeValidation = _challengeService.ValidateChallenge(
                signedMessage,
                chainId,
                address,
                audience ?? "axon-challenge");

            if (challengeValidation.IsFailure)
            {
                _logger.LogWarning("Challenge validation failed: {Error}", challengeValidation.Error);
                return Result.Failure<bool, Error>(challengeValidation.Error);
            }

            if (!challengeValidation.Value)
            {
                _logger.LogWarning("Challenge validation returned false");
                return Result.Failure<bool, Error>(
                    Error.Validation("Challenge validation failed", "CHALLENGE.INVALID"));
            }

            _logger.LogDebug("Challenge validation successful for chain {ChainId}, address {Address}",
                chainId, MaskAddress(address));

            return Result.Success<bool, Error>(true);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Challenge JSON parsing failed");
            return Result.Failure<bool, Error>(
                Error.Validation("Invalid challenge message format - JSON parsing failed", "CHALLENGE.INVALID_JSON"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during challenge validation");
            return Result.Failure<bool, Error>(
                Error.Internal("Challenge validation failed unexpectedly", "CHALLENGE.VALIDATION_ERROR"));
        }
    }

    private static string MaskAddress(string address)
    {
        return address.Length > 8
            ? $"{address[..4]}...{address[^4..]}"
            : address;
    }
}