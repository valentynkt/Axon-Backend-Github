using Axon.Api.Contracts.V1.Auth;
using Axon.Api.Modules;
using Axon.Modules.Identity.Application.Queries.GetMyPrincipal;
using Axon.Modules.Identity.Application.DTOs.Responses;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Application.Common.Constants;
using Axon.Modules.Identity.Application.Contracts.Services;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.AspNetCore.Authorization;

namespace Axon.Api.Endpoints.V1.Auth;

/// <summary>
/// GET /auth/me - Get current user information
/// Uses Axon JWT authentication scheme
/// </summary>
[Authorize(AuthenticationSchemes = "AxonJwt")]
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

        **Requires**: Valid Axon Access Token in Authorization header

        **Authentication**:
        - Uses AxonJwt authentication scheme
        - Token must be obtained from /auth/exchange endpoint

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

        if (principal?.Identity?.IsAuthenticated != true)
        {
            return Task.FromResult(Result.Failure<GetMyPrincipalQuery, Error>(
                Error.Unauthorized("User is not authenticated", "AUTH.NOT_AUTHENTICATED")));
        }

        // Extract claims from authenticated principal
        var subject = principal.FindFirst("sub")?.Value ?? "";
        var issuer = principal.FindFirst("iss")?.Value ?? "axon-api";

        if (string.IsNullOrEmpty(subject))
        {
            return Task.FromResult(Result.Failure<GetMyPrincipalQuery, Error>(
                Error.Unauthorized("Invalid token: missing subject claim", "AUTH.MISSING_SUBJECT")));
        }

        // Create provider type for Axon tokens
        var providerTypeResult = ProviderType.Create("axon");
        if (providerTypeResult.IsFailure)
        {
            return Task.FromResult(Result.Failure<GetMyPrincipalQuery, Error>(providerTypeResult.Error));
        }

        // Extract If-None-Match header for ETag support
        var ifNoneMatch = HttpContext.Request.Headers.IfNoneMatch.FirstOrDefault();

        Logger.LogDebug("Retrieving user info for Subject: {Subject}, Issuer: {Issuer}",
            subject, issuer);

        var query = new GetMyPrincipalQuery(
            ProviderType: providerTypeResult.Value,
            Issuer: issuer,
            Subject: subject,
            IfNoneMatch: ifNoneMatch
        );

        return Task.FromResult(Result.Success<GetMyPrincipalQuery, Error>(query));
    }

    protected override string? ExtractETagFromDomainResult(CurrentUserResult domainResult)
    {
        return domainResult.ETag;
    }
}