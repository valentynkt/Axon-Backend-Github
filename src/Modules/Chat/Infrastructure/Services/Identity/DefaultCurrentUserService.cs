using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Infrastructure.Services.Identity;

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

    /// <summary>
    /// Default implementation returns null since this is a test service
    /// In real scenarios, would require actual user resolution
    /// </summary>
    public Task<AxonUserId?> GetAxonUserIdAsync(CancellationToken cancellationToken = default)
    {
        // For test/development purposes, return null since we don't have actual user resolution
        return Task.FromResult<AxonUserId?>(null);
    }

    /// <summary>
    /// Default implementation returns false since this is a test service
    /// In real scenarios, would check cache for actual user
    /// </summary>
    public bool TryGetAxonUserId(out AxonUserId axonUserId)
    {
        axonUserId = default;
        return false;
    }
}