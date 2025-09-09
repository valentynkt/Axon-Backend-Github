namespace Axon.Modules.Identity.Application.Queries.GetCurrentUser;

/// <summary>
/// Information about the current user derived from JWT claims
/// </summary>
public sealed record CurrentUserInfo(
    string AxonId,
    string Subject,
    bool IsAuthenticated,
    Dictionary<string, object> Claims);