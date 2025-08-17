using System.Security.Claims;
using Axon.Modules.Chat.Application.Services;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Axon.Modules.Chat.Application.Abstractions.Security;

namespace Axon.Modules.Chat.Infrastructure.Services;

/// <summary>
/// Implementation of ICurrentUserService that retrieves user information from HTTP context
/// Provides secure access to authenticated user data for audit trails and domain logic
/// </summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Gets the current authenticated user's unique identifier from JWT claims
    /// </summary>
    public string? UserId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            return user?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? user?.FindFirst("sub")?.Value 
                ?? user?.FindFirst("user_id")?.Value;
        }
    }

    /// <summary>
    /// Gets the current authenticated user's display name from JWT claims
    /// </summary>
    public string? UserName
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            return user?.FindFirst(ClaimTypes.Name)?.Value 
                ?? user?.FindFirst("name")?.Value 
                ?? user?.FindFirst("preferred_username")?.Value;
        }
    }

    /// <summary>
    /// Indicates whether a user is currently authenticated
    /// </summary>
    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;

    /// <summary>
    /// Gets the current user ID or returns a default system user identifier
    /// </summary>
    public string GetUserIdOrDefault(string systemUserId = "SYSTEM")
    {
        return UserId ?? systemUserId;
    }

    /// <summary>
    /// Gets the current user ID or returns "SYSTEM" for system operations
    /// Used by SPARC Event Sourcing infrastructure for domain event metadata
    /// </summary>
    public string GetCurrentUserIdOrSystem()
    {
        return UserId ?? "SYSTEM";
    }
}