using BuildingBlocks.Core.Abstractions.Authentication;

namespace Axon.Modules.Chat.Infrastructure.Services;

/// <summary>
/// Default implementation of ICurrentUserService for testing and development
/// </summary>
public sealed class DefaultCurrentUserService : ICurrentUserService
{
    // Use a well-known GUID for the system user instead of a string
    private static readonly Guid SystemUserGuid = new("00000000-0000-0000-0000-000000000001");
    
    public string? UserId => SystemUserGuid.ToString();
    public string? UserName => "System";
    public bool IsAuthenticated => true;

    public string GetUserIdOrDefault(string systemUserId = "SYSTEM")
    {
        return UserId ?? systemUserId;
    }

    public string GetCurrentUserIdOrSystem()
    {
        return UserId ?? "SYSTEM";
    }
}