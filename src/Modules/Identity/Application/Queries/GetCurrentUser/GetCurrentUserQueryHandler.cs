using Axon.Modules.Identity.Application.Common.Queries;
using BuildingBlocks.Application.Services;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Application.Queries.GetCurrentUser;

/// <summary>
/// Handler for getting current user information from JWT claims
/// </summary>
public sealed class GetCurrentUserQueryHandler : BaseIdentityQueryHandler<GetCurrentUserQuery, CurrentUserInfo>
{
    public GetCurrentUserQueryHandler(ICurrentUserService currentUserService) : base(currentUserService)
    {
    }

    public override async Task<Result<CurrentUserInfo, Error>> Handle(GetCurrentUserQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var principal = query.Principal;
            
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return Result.Failure<CurrentUserInfo, Error>(
                    Error.Authorization("User is not authenticated"));
            }

            var subjectId = principal.FindFirst("sub")?.Value ?? "unknown";
            
            // Extract all claims as a dictionary
            var claims = principal.Claims.ToDictionary(c => c.Type, c => (object)c.Value);

            // Create mock AxonId for now - in real implementation this would be looked up or derived
            var axonId = $"axon_{subjectId[..Math.Min(8, subjectId.Length)]}";

            var userInfo = new CurrentUserInfo(
                AxonId: axonId,
                Subject: subjectId,
                IsAuthenticated: true,
                Claims: claims
            );

            return Result.Success<CurrentUserInfo, Error>(userInfo);
        }
        catch (Exception ex)
        {
            return Result.Failure<CurrentUserInfo, Error>(
                Error.Unexpected("Failed to get current user information", ex.Message));
        }
    }
}