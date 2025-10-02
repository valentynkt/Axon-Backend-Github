using Axon.Api.Contracts.V1.Auth;
using Axon.Api.Modules.Identity.Processors;
using Axon.Modules.Identity.Application.Queries.GetMyPrincipal;
using Axon.Modules.Identity.Application.DTOs.Responses;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using BuildingBlocks.Web.Endpoints.Base;
using BuildingBlocks.Web.Extensions;
using CSharpFunctionalExtensions;
using FastEndpoints;
using MediatR;
using System.Security.Claims;

namespace Axon.Api.Endpoints.V1.Auth;

/// <summary>
/// GET /auth/me - Get current user information
/// Accepts both Dynamic JWT and Axon JWT tokens
/// </summary>
public sealed class MeEndpoint : BaseResultEndpoint<GetCurrentUserRequestDto, GetCurrentUserResponseDto>
{
    private readonly IMediator _mediator;

    public MeEndpoint(
        IMediator mediator,
        ILogger<MeEndpoint> logger)
        : base(logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    public override void Configure()
    {
        Get("/api/v1/auth/me");

        // Require authorization with DynamicOrAxon policy
        Policies("DynamicOrAxon");

        // Add IdentityAuthProcessor for authentication validation
        PreProcessor<IdentityAuthProcessor<GetCurrentUserRequestDto>>();

        // Apply rate limiting for auth endpoints
        Options(x => x.RequireRateLimiting("AuthExchange"));

        Summary(s =>
        {
            s.Summary = "Get current user information";
            s.Description = """
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
            s.Responses[200] = "Returns current user information with claims";
            s.Responses[304] = "Not Modified - Content hasn't changed since last request (ETag match)";
            s.Responses[400] = "Invalid request parameters";
            s.Responses[401] = "User not authenticated";
            s.Responses[403] = "User does not have access to this resource";
            s.Responses[404] = "Resource not found";
            s.Responses[500] = "Internal server error";
        });

        Tags("Authentication");
    }

    protected override async Task<Result<GetCurrentUserResponseDto, Error>> ExecuteAsync(
        GetCurrentUserRequestDto request,
        CancellationToken ct)
    {
        // User is already authenticated via IdentityAuthProcessor
        var principal = HttpContext.User;

        // Extract AxonPrincipalId from JWT token with fallback support
        var axonPrincipalIdClaim = principal.FindFirst("axon_user_id")?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(axonPrincipalIdClaim) || !Guid.TryParse(axonPrincipalIdClaim, out var principalIdGuid))
        {
            return Result.Failure<GetCurrentUserResponseDto, Error>(
                Error.Unauthorized("Invalid token: missing or invalid user identifier", "AUTH.MISSING_PRINCIPAL_ID"));
        }

        var axonPrincipalId = new AxonUserId(principalIdGuid);

        // Get client ETag from ETagPreProcessor (stored in HttpContext.Items)
        var ifNoneMatch = HttpContext.Items["ClientETag"]?.ToString();

        Logger.LogDebug("Retrieving user info for AxonPrincipalId: {PrincipalId}",
            axonPrincipalId.Value);

        var query = new GetMyPrincipalQuery(
            PrincipalId: axonPrincipalId,
            IfNoneMatch: ifNoneMatch
        );

        var domainResult = await _mediator.Send(query, ct);
        if (domainResult.IsFailure)
            return Result.Failure<GetCurrentUserResponseDto, Error>(domainResult.Error);

        // Map domain result to response using Mapster
        var responseResult = domainResult.Value.AdaptSafely<GetCurrentUserResponseDto>();

        // ETag handling is done automatically by ETagPostProcessor since GetCurrentUserResponseDto implements IHaveETag
        return responseResult;
    }
}
