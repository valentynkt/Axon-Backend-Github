using System.Security.Claims;
using BuildingBlocks.Core.Abstractions.Authentication;
using Microsoft.AspNetCore.Http;

namespace Axon.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Implementation of ICurrentUserService that reads user information from HTTP context claims
/// This service extracts user data from the authenticated principal created by DynamicXyzAuthHandler
/// </summary>
public sealed class HttpContextUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    
    public HttpContextUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    /// <summary>
    /// Gets the current authenticated user's unique identifier from claims
    /// </summary>
    public string? UserId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.User.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            // Try to get user ID from standard ClaimTypes.NameIdentifier
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);
            return userIdClaim?.Value;
        }
    }

    /// <summary>
    /// Gets the current authenticated user's display name (email in Dynamic.xyz case)
    /// </summary>
    public string? UserName
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.User.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            // Try email first, then name claim
            var emailClaim = httpContext.User.FindFirst(ClaimTypes.Email);
            if (emailClaim != null)
            {
                return emailClaim.Value;
            }

            var nameClaim = httpContext.User.FindFirst(ClaimTypes.Name);
            return nameClaim?.Value;
        }
    }

    /// <summary>
    /// Indicates whether a user is currently authenticated
    /// </summary>
    public bool IsAuthenticated
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            return httpContext?.User.Identity?.IsAuthenticated == true;
        }
    }

    /// <summary>
    /// Gets the current user ID or returns a default system user identifier
    /// </summary>
    public string GetUserIdOrDefault(string systemUserId = "SYSTEM")
    {
        return UserId ?? systemUserId;
    }

    /// <summary>
    /// Gets the current user ID or returns "SYSTEM" for system operations
    /// </summary>
    public string GetCurrentUserIdOrSystem()
    {
        return UserId ?? "SYSTEM";
    }
    
    /// <summary>
    /// Gets additional user claims for extended functionality
    /// </summary>
    public IEnumerable<Claim> GetUserClaims()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User.Identity?.IsAuthenticated != true)
        {
            return Enumerable.Empty<Claim>();
        }

        return httpContext.User.Claims;
    }
    
    /// <summary>
    /// Gets user's wallet addresses from claims
    /// </summary>
    public IEnumerable<string> GetUserWallets()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User.Identity?.IsAuthenticated != true)
        {
            return Enumerable.Empty<string>();
        }

        return httpContext.User.Claims
            .Where(c => c.Type == "wallet")
            .Select(c => c.Value)
            .Distinct();
    }
    
    /// <summary>
    /// Checks if the current user is a new user
    /// </summary>
    public bool IsNewUser()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var newUserClaim = httpContext.User.FindFirst("is_new_user");
        return newUserClaim != null && bool.TryParse(newUserClaim.Value, out var isNew) && isNew;
    }
}