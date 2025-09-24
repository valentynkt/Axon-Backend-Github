using Axon.Api.Contracts.V1.Auth;
using Axon.Api.Modules;
using Axon.Modules.Identity.Application.Queries.GetMyPrincipal;
using Axon.Modules.Identity.Application.DTOs.Responses;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Axon.Api.Endpoints.V1.Auth;

/// <summary>
/// GET /auth/me - Get current user information
/// Accepts both Dynamic JWT and Axon JWT tokens
/// </summary>
[Authorize(Policy = "DynamicOrAxon")]
public sealed class MeEndpoint : BaseIdentityQueryEndpoint<GetCurrentUserRequestDto, GetCurrentUserResponseDto, GetMyPrincipalQuery, CurrentUserResult>
{
    public MeEndpoint(
        IMediator mediator,
        ILogger<MeEndpoint> logger)
        : base(mediator, logger)
    {
    }

    public override void Configure()
    {
        base.Configure();

        // Apply rate limiting for auth endpoints
        Options(x => x.RequireRateLimiting("AuthExchange"));
    }

    protected override string GetRoute() => "/api/v1/auth/me";

    protected override string GetSummary() => "Get current user information";

    protected override string GetDescription() =>
        """
        Returns information about the currently authenticated user with ETag caching support.

        **Requires**: Valid JWT Access Token in Authorization header

        **Authentication**:
        - Accepts both Dynamic JWT and Axon JWT tokens
        - Dynamic tokens can be used directly without exchange
        - Axon tokens obtained from /auth/exchange endpoint

        **ETag Support**:
        - Server returns `ETag` header with fingerprint of user data
        - Client can send `If-None-Match` header to check for changes
        - Returns `304 Not Modified` if data hasn't changed since provided ETag
        - Supports client-side caching for improved performance
        """;

    protected override string GetSuccessResponse() => "Returns current user information with claims";

    protected override Task<Result<GetMyPrincipalQuery, Error>> ExecuteQuery(GetCurrentUserRequestDto request, CancellationToken ct)
    {
        // User is already authenticated via [Authorize] attribute
        var principal = HttpContext.User;

        // Extract AxonPrincipalId from JWT token with fallback support
        var axonPrincipalIdClaim = principal.FindFirst("axon_user_id")?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(axonPrincipalIdClaim) || !Guid.TryParse(axonPrincipalIdClaim, out var principalIdGuid))
        {
            return Task.FromResult(Result.Failure<GetMyPrincipalQuery, Error>(
                Error.Unauthorized("Invalid token: missing or invalid user identifier", "AUTH.MISSING_PRINCIPAL_ID")));
        }

        var axonPrincipalId = new AxonUserId(principalIdGuid);

        // Extract If-None-Match header for ETag support
        var ifNoneMatch = HttpContext.Request.Headers.IfNoneMatch.FirstOrDefault();

        Logger.LogDebug("Retrieving user info for AxonPrincipalId: {PrincipalId}",
            axonPrincipalId.Value);

        var query = new GetMyPrincipalQuery(
            PrincipalId: axonPrincipalId,
            IfNoneMatch: ifNoneMatch
        );

        return Task.FromResult(Result.Success<GetMyPrincipalQuery, Error>(query));
    }

    protected override string? ExtractETagFromDomainResult(CurrentUserResult domainResult)
    {
        return domainResult.ETag;
    }
}