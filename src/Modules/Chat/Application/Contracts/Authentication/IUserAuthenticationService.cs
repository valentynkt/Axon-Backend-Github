using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Chat.Application.Contracts.Authentication;

/// <summary>
/// Service for authenticating and validating the current user in chat operations.
/// </summary>
public interface IUserAuthenticationService
{
    /// <summary>
    /// Gets the authenticated user's ID with proper validation.
    /// </summary>
    /// <returns>
    /// Success with UserId if user is authenticated and valid,
    /// Failure with appropriate error if unauthenticated or invalid.
    /// </returns>
    Result<UserId, Error> GetAuthenticatedUserId();
}