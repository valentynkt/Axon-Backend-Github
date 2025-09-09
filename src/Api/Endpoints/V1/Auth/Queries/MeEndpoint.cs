using Axon.Api.Contracts.V1.Auth;
using Axon.Api.Modules;
using Axon.Modules.Identity.Application.Queries.GetCurrentUser;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;

namespace Axon.Api.Endpoints.V1.Auth;

/// <summary>
/// GET /auth/me - Get current user information
/// Uses BaseIdentityQueryEndpoint for standardized error handling and authentication
/// </summary>
public sealed class GetCurrentUserEndpoint : BaseIdentityQueryEndpoint<GetCurrentUserRequestDto, GetCurrentUserResponseDto, GetCurrentUserQuery, CurrentUserInfo>
{
    public GetCurrentUserEndpoint(IMediator mediator, ILogger<GetCurrentUserEndpoint> logger) 
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

    protected override async Task<Result<GetCurrentUserQuery, Error>> ExecuteQuery(GetCurrentUserRequestDto request, CancellationToken ct)
    {
        var user = HttpContext.User;
        
        if (user?.Identity?.IsAuthenticated != true)
        {
            Logger.LogWarning("Unauthenticated user attempting to access /auth/me");
            return Result.Failure<GetCurrentUserQuery, Error>(
                Error.Unauthorized("User is not authenticated"));
        }

        Logger.LogDebug("Retrieving user info for subject: {Subject}", 
            user.FindFirst("sub")?.Value ?? "unknown");

        return Result.Success<GetCurrentUserQuery, Error>(new GetCurrentUserQuery(user));
    }
}