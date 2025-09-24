using Axon.Modules.Identity.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Axon.Modules.Identity.Application.Contracts.Services;

/// <summary>
/// Service for generating and validating refresh tokens using Identity's token system
/// </summary>
public interface IRefreshTokenProvider
{
    /// <summary>
    /// Generates a refresh token for the user
    /// </summary>
    Task<string> GenerateAsync(string purpose, UserManager<AxonUserAuth> manager, AxonUserAuth user);

    /// <summary>
    /// Validates a refresh token
    /// </summary>
    Task<bool> ValidateAsync(string purpose, string token, UserManager<AxonUserAuth> manager, AxonUserAuth user);

    /// <summary>
    /// Gets the JTI from a refresh token for tracking
    /// </summary>
    string? GetJtiFromToken(string token);

    /// <summary>
    /// Gets the user ID from a refresh token without full validation (for performance optimization)
    /// </summary>
    Guid? GetUserIdFromToken(string token);
}