using Axon.Modules.Identity.Application.DTOs.Responses;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Application.Contracts.Services;

/// <summary>
/// Service responsible for retrieving user profile information including principal data,
/// wallet information, and ETag-based caching support.
/// Separated from authentication concerns following Single Responsibility Principle.
/// </summary>
public interface IUserProfileService
{
    /// <summary>
    /// Get current user profile by AxonPrincipalId with ETag caching support.
    /// This is the primary method used by authenticated endpoints.
    /// </summary>
    /// <param name="principalId">The Axon Principal ID from JWT token</param>
    /// <param name="ifNoneMatch">Optional If-None-Match header for ETag validation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>
    /// Success: CurrentUserResult with profile and wallet data
    /// Conflict: When ETag matches (304 Not Modified scenario)
    /// NotFound: When principal doesn't exist
    /// </returns>
    Task<Result<CurrentUserResult, Error>> GetCurrentUserProfileAsync(
        AxonUserId principalId,
        string? ifNoneMatch = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Legacy method for backward compatibility - gets user profile by provider credentials.
    /// Used when only provider information is available instead of direct principal ID.
    /// </summary>
    /// <param name="providerType">Provider type (e.g., "dynamic", "wallet")</param>
    /// <param name="issuer">Token issuer</param>
    /// <param name="subject">Provider subject/user ID</param>
    /// <param name="ifNoneMatch">Optional If-None-Match header for ETag validation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>
    /// Success: CurrentUserResult with profile and wallet data
    /// Conflict: When ETag matches (304 Not Modified scenario)
    /// NotFound: When principal doesn't exist for given credentials
    /// </returns>
    Task<Result<CurrentUserResult, Error>> GetUserProfileByCredentialAsync(
        ProviderType providerType,
        string issuer,
        string subject,
        string? ifNoneMatch = null,
        CancellationToken cancellationToken = default);
}