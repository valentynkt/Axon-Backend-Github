using Axon.Api.Contracts.V1.Identity.Authentication;
using Axon.Api.Endpoints.V1.Auth;
using Axon.Api.Modules;
using Axon.Modules.Identity.Application.Queries.GetCurrentUser;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using FastEndpoints;
using MediatR;

namespace Axon.Api.Endpoints.V1.Identity.Queries.GetCurrentUser;

/// <summary>
/// Endpoint for retrieving current authenticated user information
/// </summary>
public class GetCurrentUserEndpoint : BaseIdentityQueryEndpoint<Contracts.V1.Identity.Common.EmptyRequest, CurrentUserResponseDto, GetCurrentUserQuery, CurrentUserResult>
{
    public GetCurrentUserEndpoint(IMediator mediator, ILogger<GetCurrentUserEndpoint> logger) 
        : base(mediator, logger)
    {
    }

    public override void Configure()
    {
        base.Configure();
        
        // Add Authorization header requirement for OpenAPI documentation
        Description(d => d
            .WithTags("Authentication")
            .Accepts<Contracts.V1.Identity.Common.EmptyRequest>("application/json")
            .Produces<CurrentUserResponseDto>(200, "application/json")
            .ProducesProblemFE(401)
            .ProducesProblemFE(403)
            .ProducesProblemFE(500));
    }

    protected override Task<Result<GetCurrentUserQuery, Error>> ExecuteQuery(Contracts.V1.Identity.Common.EmptyRequest request, CancellationToken ct)
    {
        // Extract JWT from Authorization header
        var authHeader = HttpContext.Request.Headers.Authorization.FirstOrDefault();
        var token = authHeader?.Replace("Bearer ", "", StringComparison.Ordinal);
        
        if (string.IsNullOrEmpty(token))
        {
            return Task.FromResult(Result.Failure<GetCurrentUserQuery, Error>(
                Error.Unauthorized("Authorization header with Bearer token is required")));
        }
        
        // S1: Just check token presence - S2 will validate with Dynamic.xyz
        var query = new GetCurrentUserQuery(token);
        return Task.FromResult(Result.Success<GetCurrentUserQuery, Error>(query));
    }

    protected override string GetRoute() => "/api/v1/auth/me";

    protected override string GetSummary() => "Get current authenticated user information";

    protected override string GetDescription() => 
        "Retrieves the current authenticated user's profile and connected wallet information";

    protected override string GetSuccessResponse() => 
        "Successfully retrieved current user information including profile and wallets";
}