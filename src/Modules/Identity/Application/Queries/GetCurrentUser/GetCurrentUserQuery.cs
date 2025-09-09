using Axon.Modules.Identity.Application.Common.Queries;
using System.Security.Claims;

namespace Axon.Modules.Identity.Application.Queries.GetCurrentUser;

/// <summary>
/// Query to get current user information from JWT claims
/// </summary>
public sealed record GetCurrentUserQuery(ClaimsPrincipal Principal) : IdentityBaseQuery<CurrentUserInfo>;