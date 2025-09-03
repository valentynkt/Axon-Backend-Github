using System.Security.Cryptography;
using System.Text;
using Axon.Modules.Identity.Application.Queries.GetCurrentUser;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Infrastructure orchestrator for GET /me endpoint.
/// Thin layer that invokes Application queries and handles ETag generation.
/// </summary>
public interface IMeReader
{
    /// <summary>
    /// Gets current user information with ETag support.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing current user data and ETag</returns>
    Task<Result<MeReaderResult, Error>> GetAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result containing user data and ETag for caching
/// </summary>
public record MeReaderResult
{
    public required CurrentUserResult UserData { get; init; }
    public required string ETag { get; init; }
}

/// <summary>
/// Implementation of IMeReader that orchestrates the GET /me flow
/// </summary>
public sealed class MeReader : IMeReader
{
    private readonly IMediator _mediator;
    private readonly ILogger<MeReader> _logger;

    public MeReader(IMediator mediator, ILogger<MeReader> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<MeReaderResult, Error>> GetAsync(CancellationToken cancellationToken = default)
    {
        using var activity = _logger.BeginScope("MeReader.GetAsync");
        
        try
        {
            // Invoke Application layer query
            var query = new GetCurrentUserQuery();
            var result = await _mediator.Send(query, cancellationToken);

            if (result.IsFailure)
            {
                _logger.LogWarning("GetCurrentUserQuery failed: {ErrorCode} - {ErrorMessage}", 
                    result.Error.Code, result.Error.Message);
                return result.Error;
            }

            var userData = result.Value;

            // Generate ETag from stable response data
            var etag = GenerateETag(userData);
            
            _logger.LogDebug("Generated ETag {ETag} for user {AxonId}", 
                etag, userData.Profile.AxonId);

            var meResult = new MeReaderResult
            {
                UserData = userData,
                ETag = etag
            };

            return Result.Success<MeReaderResult, Error>(meResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in MeReader.GetAsync");
            return Result.Failure<MeReaderResult, Error>(
                Error.Failure("An error occurred retrieving user data", "IDENTITY.ME_READER.FAILED"));
        }
    }

    /// <summary>
    /// Generates a stable ETag from user data for caching support.
    /// Based on profile.updatedAt, wallet count, latest wallet verification time, and defaultPerChain.
    /// </summary>
    private static string GenerateETag(CurrentUserResult userData)
    {
        var hashInput = new StringBuilder();
        
        // Include profile updated timestamp (ticks for precision)
        hashInput.Append(userData.Profile.UpdatedAt.Ticks);
        
        // Include wallet count
        hashInput.Append('|');
        hashInput.Append(userData.OwnedWallets.Count);
        
        // Include max of last verified or first linked timestamps from wallets
        if (userData.OwnedWallets.Count > 0)
        {
            var maxWalletTimestamp = userData.OwnedWallets
                .Select(w => w.LastVerifiedAt ?? w.FirstLinkedAt)
                .Max();
            hashInput.Append('|');
            hashInput.Append(maxWalletTimestamp.Ticks);
        }
        
        // Include stable hash of defaultPerChain
        if (userData.DefaultPerChain.Count > 0)
        {
            hashInput.Append('|');
            var sortedDefaults = userData.DefaultPerChain
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => $"{kvp.Key}:{kvp.Value}");
            hashInput.Append(string.Join(",", sortedDefaults));
        }
        
        // Generate SHA256 hash
        var inputBytes = Encoding.UTF8.GetBytes(hashInput.ToString());
        var hashBytes = SHA256.HashData(inputBytes);
        var hashHex = Convert.ToHexString(hashBytes);
        
        // Return as W3C compliant ETag with quotes (full hash for better collision avoidance)
        return $"\"{hashHex}\"";
    }
}