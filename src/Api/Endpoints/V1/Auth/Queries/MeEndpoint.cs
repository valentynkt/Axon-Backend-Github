using Axon.Api.Contracts.V1.Auth;
using Axon.Api.Modules;
using Axon.Modules.Identity.Application.Queries.GetMyPrincipal;
using Axon.Modules.Identity.Application.DTOs.Responses;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Application.Common.Constants;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;

namespace Axon.Api.Endpoints.V1.Auth;

/// <summary>
/// GET /auth/me - Get current user information
/// Uses BaseIdentityQueryEndpoint for standardized error handling and authentication
/// </summary>
public sealed class MeEndpoint : BaseIdentityQueryEndpoint<GetCurrentUserRequestDto, GetCurrentUserResponseDto, GetMyPrincipalQuery, CurrentUserResult>
{
    public MeEndpoint(IMediator mediator, ILogger<MeEndpoint> logger) 
        : base(mediator, logger)
    {
    }

    protected override string GetRoute() => "/auth/me";

    protected override string GetSummary() => "Get current user information";

    protected override string GetDescription() => 
        """
        Returns information about the currently authenticated user.
        
        **Requires**: Valid JWT token in Authorization header
        """;

    protected override string GetSuccessResponse() => "Returns current user information with claims";

    protected override async Task<Result<GetMyPrincipalQuery, Error>> ExecuteQuery(GetCurrentUserRequestDto request, CancellationToken ct)
    {
        var user = HttpContext.User;
        
        if (user?.Identity?.IsAuthenticated != true)
        {
            Logger.LogWarning("Unauthenticated user attempting to access /auth/me");
            return Result.Failure<GetMyPrincipalQuery, Error>(
                Error.Unauthorized("User is not authenticated"));
        }

        // Extract Dynamic credential information from JWT claims
        var subjectClaim = user.FindFirst("sub")?.Value;
        var issuerClaim = user.FindFirst("iss")?.Value;

        if (string.IsNullOrWhiteSpace(subjectClaim))
        {
            Logger.LogWarning("JWT missing subject claim for /auth/me request");
            return Result.Failure<GetMyPrincipalQuery, Error>(
                Error.Validation("JWT missing required subject claim"));
        }

        if (string.IsNullOrWhiteSpace(issuerClaim))
        {
            Logger.LogWarning("JWT missing issuer claim for /auth/me request");
            return Result.Failure<GetMyPrincipalQuery, Error>(
                Error.Validation("JWT missing required issuer claim"));
        }

        // Create Dynamic provider type
        var providerTypeResult = ProviderType.Create(DynamicAuthConstants.ProviderType);
        if (providerTypeResult.IsFailure)
        {
            Logger.LogError("Failed to create Dynamic provider type: {Error}", providerTypeResult.Error.Message);
            return Result.Failure<GetMyPrincipalQuery, Error>(providerTypeResult.Error);
        }

        // Extract If-None-Match header for ETag support
        var ifNoneMatch = HttpContext.Request.Headers.IfNoneMatch.FirstOrDefault();

        Logger.LogDebug("Retrieving user info for subject: {Subject}, issuer: {Issuer}", subjectClaim, issuerClaim);

        var query = new GetMyPrincipalQuery(
            ProviderType: providerTypeResult.Value,
            Issuer: issuerClaim,
            Subject: subjectClaim,
            IfNoneMatch: ifNoneMatch
        );

        return Result.Success<GetMyPrincipalQuery, Error>(query);
    }
}