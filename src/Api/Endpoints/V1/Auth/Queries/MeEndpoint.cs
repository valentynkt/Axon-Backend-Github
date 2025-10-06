using Axon.Api.Contracts.V1.Auth;
using Axon.Api.Modules.Identity.Processors;
using Axon.Modules.Identity.Application.Queries.GetMyPrincipal;
using Axon.Modules.Identity.Application.DTOs.Responses;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Domain.ValueObjects;
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
    private readonly IUserProfileService _userProfileService;

    public MeEndpoint(
        IMediator mediator,
        IUserProfileService userProfileService,
        ILogger<MeEndpoint> logger)
        : base(logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _userProfileService = userProfileService ?? throw new ArgumentNullException(nameof(userProfileService));
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
                Returns information about the currently authenticated user.

                **Requires**: Valid JWT Access Token in Authorization header

                **Authentication**:
                - Accepts both Dynamic JWT and Axon JWT tokens
                - Dynamic tokens can be used directly without exchange
                - Axon tokens obtained from /auth/exchange endpoint
                """;
            s.Responses[200] = "Returns current user information with claims";
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

        // Extract AxonPrincipalId from JWT token with fallback to Dynamic JWT
        var axonPrincipalIdClaim = principal.FindFirst("axon_user_id")?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        Result<CurrentUserResult, Error> domainResult;

        if (!string.IsNullOrEmpty(axonPrincipalIdClaim) && Guid.TryParse(axonPrincipalIdClaim, out var principalIdGuid))
        {
            // Path 1: Axon JWT with axon_user_id claim (from /auth/exchange)
            var axonPrincipalId = new AxonUserId(principalIdGuid);

            Logger.LogDebug("Retrieving user info for AxonPrincipalId: {PrincipalId}", axonPrincipalId.Value);

            var query = new GetMyPrincipalQuery(PrincipalId: axonPrincipalId);

            domainResult = await _mediator.Send(query, ct);
        }
        else
        {
            // Path 2: Dynamic JWT without axon_user_id (direct Dynamic token)
            // Extract Dynamic JWT claims
            var subject = principal.FindFirst("sub")?.Value;
            var issuer = principal.FindFirst("iss")?.Value;

            if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(issuer))
            {
                return Result.Failure<GetCurrentUserResponseDto, Error>(
                    Error.Unauthorized("Invalid token: missing subject or issuer", "AUTH.MISSING_CLAIMS"));
            }

            Logger.LogDebug("Retrieving user info via Dynamic JWT - Subject: {Subject}, Issuer: {Issuer}",
                subject, issuer);

            // Use UserProfileService to look up principal by credential
            // This will return 404 if principal doesn't exist (never exchanged)
            domainResult = await _userProfileService.GetUserProfileByCredentialAsync(
                ProviderType.Dynamic,
                issuer,
                subject,
                ct);
        }

        if (domainResult.IsFailure)
            return Result.Failure<GetCurrentUserResponseDto, Error>(domainResult.Error);

        // Map domain result to response using Mapster
        var responseResult = domainResult.Value.AdaptSafely<GetCurrentUserResponseDto>();

        return responseResult;
    }
}
