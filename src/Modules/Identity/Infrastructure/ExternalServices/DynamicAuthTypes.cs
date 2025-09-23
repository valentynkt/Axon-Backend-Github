using System.Security.Claims;
using Axon.Modules.Identity.Application.Contracts.ExternalServices;

namespace Axon.Modules.Identity.Infrastructure.ExternalServices;

/// <summary>
/// Cached token validation data containing both ClaimsPrincipal and normalized user data
/// </summary>
internal sealed record CachedTokenData(
    ClaimsPrincipal Principal,
    DynamicUserData UserData,
    DateTimeOffset CachedAt);