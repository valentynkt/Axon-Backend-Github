namespace Axon.Modules.Chat.Application.Abstractions.Security;

/// <summary>
/// Service interface for accessing current user information in the application context
/// Provides secure access to authenticated user data for audit trails and domain logic
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Gets the current authenticated user's unique identifier
    /// Returns null if no user is authenticated or in system context
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// Gets the current authenticated user's display name
    /// Returns null if no user is authenticated or display name is not available
    /// </summary>
    string? UserName { get; }

    /// <summary>
    /// Indicates whether a user is currently authenticated
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Gets the current user ID or returns a default system user identifier
    /// Useful for audit trails when operations are performed by the system
    /// </summary>
    /// <param name="systemUserId">Default system user ID to use when no user is authenticated</param>
    /// <returns>Current user ID or the provided system user ID</returns>
    string GetUserIdOrDefault(string systemUserId = "SYSTEM");

    /// <summary>
    /// Gets the current user ID or returns "SYSTEM" for system operations
    /// Used by SPARC Event Sourcing infrastructure for domain event metadata
    /// </summary>
    /// <returns>Current user ID or "SYSTEM"</returns>
    string GetCurrentUserIdOrSystem();
}