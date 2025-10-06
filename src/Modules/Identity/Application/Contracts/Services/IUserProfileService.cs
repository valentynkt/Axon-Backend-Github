using Axon.Modules.Identity.Application.DTOs.Responses;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Application.Contracts.Services;

/// <summary>
/// Service responsible for retrieving user profile information including principal data
/// and wallet information.
/// Separated from authentication concerns following Single Responsibility Principle.
/// </summary>
public interface IUserProfileService
{
    /// <summary>
    /// Get current user profile by AxonPrincipalId.
    /// This is the primary method used by authenticated endpoints.
    /// </summary>
    /// <param name="principalId">The Axon Principal ID from JWT token</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>
    /// Success: CurrentUserResult with profile and wallet data
    /// NotFound: When principal doesn't exist
    /// </returns>
    Task<Result<CurrentUserResult, Error>> GetCurrentUserProfileAsync(
        AxonUserId principalId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Legacy method for backward compatibility - gets user profile by provider credentials.
    /// Used when only provider information is available instead of direct principal ID.
    /// </summary>
    /// <param name="providerType">Provider type (e.g., "dynamic", "wallet")</param>
    /// <param name="issuer">Token issuer</param>
    /// <param name="subject">Provider subject/user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>
    /// Success: CurrentUserResult with profile and wallet data
    /// NotFound: When principal doesn't exist for given credentials
    /// </returns>
    Task<Result<CurrentUserResult, Error>> GetUserProfileByCredentialAsync(
        ProviderType providerType,
        string issuer,
        string subject,
        CancellationToken cancellationToken = default);
}