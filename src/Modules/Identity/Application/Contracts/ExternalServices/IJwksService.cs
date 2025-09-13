using Microsoft.IdentityModel.Tokens;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Application.Contracts.ExternalServices;

/// <summary>
/// Service for fetching and caching JWKS (JSON Web Key Set) keys from an external provider.
/// Handles key retrieval, caching, and retry logic for JWT token validation.
/// </summary>
public interface IJwksService
{
    /// <summary>
    /// Gets JWKS keys for JWT token validation, with automatic caching and retry logic.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>
    /// Result containing collection of security keys for JWT validation,
    /// or error if keys cannot be fetched or parsed
    /// </returns>
    Task<Result<ICollection<SecurityKey>, Error>> GetJwksKeysAsync(CancellationToken cancellationToken = default);
}