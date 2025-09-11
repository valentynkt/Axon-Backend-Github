using Axon.Modules.Identity.Application.Common.Queries;
using Axon.Modules.Identity.Application.DTOs.Responses;
using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Application.Queries.GetMyPrincipal;

/// <summary>
/// Query to get current authenticated user's principal information with ETag support.
/// Used by GET /auth/me endpoint to provide efficient client-side caching.
/// </summary>
public sealed record GetMyPrincipalQuery(
    ProviderType ProviderType,
    string Issuer,
    string Subject,
    string? IfNoneMatch = null) : IdentityBaseQuery<CurrentUserResult>;