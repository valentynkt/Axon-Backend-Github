using Axon.Modules.Chat.Application.Services;

namespace Axon.Modules.Chat.Infrastructure.Services;

/// <summary>
/// Default implementation of ICurrentUserService for testing and development
/// </summary>
public sealed class DefaultCurrentUserService : ICurrentUserService
{
    public string? UserId => "system";
    public string? UserName => "System";
    public bool IsAuthenticated => false;

    public string GetUserIdOrDefault(string systemUserId = "SYSTEM")
    {
        return UserId ?? systemUserId;
    }

    public string GetCurrentUserIdOrSystem()
    {
        return UserId ?? "SYSTEM";
    }
}