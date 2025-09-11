using System.Security.Claims;
using Axon.Modules.Identity.Application.Contracts.ExternalServices;

namespace Axon.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Service interface for normalizing Dynamic.xyz JWT claims into structured user data
/// </summary>
public interface IDynamicClaimNormalizer
{
    /// <summary>
    /// Normalizes claims from a validated Dynamic.xyz JWT into structured user data
    /// </summary>
    /// <param name="claimsPrincipal">The validated claims principal from JWT token</param>
    /// <returns>Normalized user data with all claims properly parsed and deduplicated</returns>
    DynamicUserData NormalizeClaimsPrincipal(ClaimsPrincipal claimsPrincipal);
}