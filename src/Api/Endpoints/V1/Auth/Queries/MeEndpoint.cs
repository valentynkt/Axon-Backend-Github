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
    private readonly IAuthenticationService _authenticationService;
    private readonly IBearerTokenExtractor _bearerTokenExtractor;

    public MeEndpoint(
        IMediator mediator,
        ILogger<MeEndpoint> logger,
        IAuthenticationService authenticationService,
        IBearerTokenExtractor bearerTokenExtractor)
        : base(mediator, logger)
    {
        _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
        _bearerTokenExtractor = bearerTokenExtractor ?? throw new ArgumentNullException(nameof(bearerTokenExtractor));
    }

    public override void Configure()
    {
        base.Configure();
        // Allow anonymous access since we'll handle token validation manually
        AllowAnonymous();

        // Apply rate limiting for auth endpoints
        Options(x => x.RequireRateLimiting("AuthExchange"));
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
        var tokenResult = _bearerTokenExtractor.ExtractBearerToken(HttpContext);
        if (tokenResult.IsFailure)
        {
            return Result.Failure<GetMyPrincipalQuery, Error>(tokenResult.Error);
        }

        var bearerToken = tokenResult.Value;

        // Validate token using unified authentication service
        var tokenValidationResult = await _authenticationService.ValidateTokenAsync(bearerToken, ct);
        if (tokenValidationResult.IsFailure)
        {
            Logger.LogWarning("Token validation failed for /auth/me: {Error}", tokenValidationResult.Error.Message);
            return Result.Failure<GetMyPrincipalQuery, Error>(tokenValidationResult.Error);
        }

        var tokenContext = tokenValidationResult.Value;

        // Provider type is already available in validated token context
        var providerType = tokenContext.ProviderType;

        // Extract If-None-Match header for ETag support
        var ifNoneMatch = HttpContext.Request.Headers.IfNoneMatch.FirstOrDefault();

        Logger.LogDebug("Retrieving user info for AxonUserId: {AxonUserId}, TokenType: {TokenType}",
            tokenContext.AxonUserId.Value, tokenContext.TokenType);

        var query = new GetMyPrincipalQuery(
            ProviderType: providerType,
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