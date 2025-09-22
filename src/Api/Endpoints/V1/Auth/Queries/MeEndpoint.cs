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

namespace Axon.Api.Endpoints.V1.Auth;

/// <summary>
/// GET /auth/me - Get current user information
/// Uses unified bearer token validation to support both Dynamic JWT and Axon Access Tokens
/// </summary>
public sealed class MeEndpoint : BaseIdentityQueryEndpoint<GetCurrentUserRequestDto, GetCurrentUserResponseDto, GetMyPrincipalQuery, CurrentUserResult>
{
    private readonly IUnifiedBearerTokenValidator _tokenValidator;

    public MeEndpoint(
        IMediator mediator,
        ILogger<MeEndpoint> logger,
        IUnifiedBearerTokenValidator tokenValidator)
        : base(mediator, logger)
    {
        _tokenValidator = tokenValidator ?? throw new ArgumentNullException(nameof(tokenValidator));
    }

    public override void Configure()
    {
        base.Configure();
        // Allow anonymous access since we'll handle token validation manually
        AllowAnonymous();
    }

    protected override string GetRoute() => "/api/v1/auth/me";

    protected override string GetSummary() => "Get current user information";

    protected override string GetDescription() =>
        """
        Returns information about the currently authenticated user with ETag caching support.

        **Requires**: Valid Dynamic JWT or Axon Access Token in Authorization header

        **Supported Token Types**:
        - Dynamic JWT (from Dynamic.xyz authentication)
        - Axon Access Token (from /auth/exchange endpoint)

        **ETag Support**:
        - Server returns `ETag` header with fingerprint of user data
        - Client can send `If-None-Match` header to check for changes
        - Returns `304 Not Modified` if data hasn't changed since provided ETag
        - Supports client-side caching for improved performance
        """;

    protected override string GetSuccessResponse() => "Returns current user information with claims";

    protected override async Task<Result<GetMyPrincipalQuery, Error>> ExecuteQuery(GetCurrentUserRequestDto request, CancellationToken ct)
    {
        // Extract bearer token from Authorization header
        var authHeader = HttpContext.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader) ||
            !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogWarning("/auth/me request missing Authorization header or Bearer token");
            return Result.Failure<GetMyPrincipalQuery, Error>(
                Error.Unauthorized("Authorization header with Bearer token is required"));
        }

        var bearerToken = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            Logger.LogWarning("/auth/me request has empty Bearer token");
            return Result.Failure<GetMyPrincipalQuery, Error>(
                Error.Unauthorized("Bearer token cannot be empty"));
        }

        // Validate token using unified validator
        var tokenValidationResult = await _tokenValidator.ValidateTokenAsync(bearerToken, ct);
        if (tokenValidationResult.IsFailure)
        {
            Logger.LogWarning("Token validation failed for /auth/me: {Error}", tokenValidationResult.Error.Message);
            return Result.Failure<GetMyPrincipalQuery, Error>(tokenValidationResult.Error);
        }

        var tokenContext = tokenValidationResult.Value;

        // Create provider type from validated token
        var providerTypeResult = ProviderType.Create(tokenContext.ProviderType);
        if (providerTypeResult.IsFailure)
        {
            Logger.LogError("Failed to create provider type from token: {Error}", providerTypeResult.Error.Message);
            return Result.Failure<GetMyPrincipalQuery, Error>(providerTypeResult.Error);
        }

        // Extract If-None-Match header for ETag support
        var ifNoneMatch = HttpContext.Request.Headers.IfNoneMatch.FirstOrDefault();

        Logger.LogDebug("Retrieving user info for AxonUserId: {AxonUserId}, TokenType: {TokenType}",
            tokenContext.AxonUserId, tokenContext.TokenType);

        var query = new GetMyPrincipalQuery(
            ProviderType: providerTypeResult.Value,
            Issuer: tokenContext.Issuer,
            Subject: tokenContext.Subject,
            IfNoneMatch: ifNoneMatch
        );

        return Result.Success<GetMyPrincipalQuery, Error>(query);
    }

    protected override string? ExtractETagFromDomainResult(CurrentUserResult domainResult)
    {
        return domainResult.ETag;
    }
}