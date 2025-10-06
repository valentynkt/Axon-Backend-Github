using Axon.Modules.Identity.Application.Common.Queries;
using Axon.Modules.Identity.Application.DTOs.Responses;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Application.Queries.GetMyPrincipal;

/// <summary>
/// Query to get current authenticated user's principal information.
/// Used by GET /auth/me endpoint.
/// Simplified to use AxonPrincipalId directly from JWT token for better performance.
/// </summary>
public sealed record GetMyPrincipalQuery(
    AxonUserId PrincipalId) : IdentityBaseQuery<CurrentUserResult>;