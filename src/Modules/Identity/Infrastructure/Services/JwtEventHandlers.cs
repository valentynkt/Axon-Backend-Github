namespace Axon.Modules.Identity.Infrastructure.Services;

using System.Security.Claims;
using Axon.Modules.Identity.Domain.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

/// <summary>
/// Centralized JWT Bearer event handlers for token validation, replay protection, and claims transformation
/// </summary>
public sealed class JwtEventHandlers
{
    private readonly IDistributedCache _distributedCache;
    private readonly UserManager<AxonUserAuth>? _userManager;
    private readonly ILogger<JwtEventHandlers> _logger;

    public JwtEventHandlers(
        IDistributedCache distributedCache,
        ILogger<JwtEventHandlers> logger,
        UserManager<AxonUserAuth>? userManager = null)
    {
        _distributedCache = distributedCache ?? throw new ArgumentNullException(nameof(distributedCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _userManager = userManager;
    }

    /// <summary>
    /// Validates Axon JWT tokens with replay protection and security stamp validation
    /// </summary>
    public async Task ValidateAxonTokenAsync(TokenValidatedContext context)
    {
        try
        {
            var principal = context.Principal;
            if (principal == null)
            {
                context.Fail("No principal found in token");
                return;
            }

            // Extract JTI for replay protection
            var jti = principal.FindFirst("jti")?.Value;
            if (string.IsNullOrEmpty(jti))
            {
                _logger.LogWarning("Axon JWT missing JTI claim");
                context.Fail("Token missing JTI claim");
                return;
            }

            // Check for token replay
            var replayKey = $"axon:jwt:nonce:{jti}";
            var cachedNonce = await _distributedCache.GetStringAsync(replayKey, context.HttpContext.RequestAborted);

            if (!string.IsNullOrEmpty(cachedNonce))
            {
                _logger.LogWarning("Token replay detected for JTI: {Jti}", jti);
                context.Fail("Token replay detected");
                return;
            }

            // Cache the JTI to prevent replay
            var expClaim = principal.FindFirst("exp")?.Value;
            if (long.TryParse(expClaim, out var exp))
            {
                var expiryTime = DateTimeOffset.FromUnixTimeSeconds(exp);
                var ttl = expiryTime - DateTimeOffset.UtcNow;

                if (ttl > TimeSpan.Zero)
                {
                    await _distributedCache.SetStringAsync(
                        replayKey,
                        "used",
                        new DistributedCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = ttl.Add(TimeSpan.FromMinutes(5)) // Add buffer
                        },
                        context.HttpContext.RequestAborted);
                }
            }

            // Validate security stamp if UserManager is available
            if (_userManager != null)
            {
                var userId = principal.FindFirst("axon_user_id")?.Value
                    ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var userGuid))
                {
                    var user = await _userManager.FindByIdAsync(userGuid.ToString());
                    if (user == null)
                    {
                        _logger.LogWarning("User not found for ID: {UserId}", userId);
                        context.Fail("User not found");
                        return;
                    }

                    // Check security stamp if present in token
                    var tokenSecurityStamp = principal.FindFirst("security_stamp")?.Value;
                    if (!string.IsNullOrEmpty(tokenSecurityStamp) && tokenSecurityStamp != user.SecurityStamp)
                    {
                        _logger.LogWarning("Security stamp mismatch for user {UserId}", userId);
                        context.Fail("Token invalidated");
                        return;
                    }

                    // Update last authenticated timestamp
                    user.UpdateLastAuthenticated();
                    await _userManager.UpdateAsync(user);
                }
            }

            _logger.LogDebug("Axon JWT validated successfully for subject: {Subject}",
                principal.FindFirst("sub")?.Value ?? "unknown");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Axon JWT validation");
            context.Fail($"Token validation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates Dynamic JWT tokens and transforms claims to Axon format
    /// </summary>
    public async Task ValidateDynamicTokenAsync(TokenValidatedContext context)
    {
        try
        {
            var principal = context.Principal;
            if (principal == null)
            {
                context.Fail("No principal found in token");
                return;
            }

            // Extract Dynamic-specific claims
            var environmentId = principal.FindFirst("environment_id")?.Value
                ?? principal.FindFirst("environmentId")?.Value;
            var verifiedCredentials = principal.FindFirst("verified_credentials")?.Value
                ?? principal.FindFirst("verifiedCredentials")?.Value;
            var sub = principal.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(sub))
            {
                _logger.LogWarning("Dynamic JWT missing sub claim");
                context.Fail("Token missing subject claim");
                return;
            }

            // Add transformed claims to the identity
            var claimsIdentity = principal.Identity as ClaimsIdentity;
            if (claimsIdentity != null)
            {
                // Add provider information
                claimsIdentity.AddClaim(new Claim("auth_provider", "dynamic"));
                claimsIdentity.AddClaim(new Claim("auth_timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()));

                // Add environment ID if present
                if (!string.IsNullOrEmpty(environmentId))
                {
                    claimsIdentity.AddClaim(new Claim("dynamic_environment_id", environmentId));
                }

                // Parse and add verified credentials if present
                if (!string.IsNullOrEmpty(verifiedCredentials))
                {
                    try
                    {
                        // Assuming verified_credentials contains wallet information
                        claimsIdentity.AddClaim(new Claim("has_verified_credentials", "true"));
                        // Additional parsing could be done here based on the actual format
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse verified credentials");
                    }
                }

                // Add a marker that this is a Dynamic-authenticated token
                claimsIdentity.AddClaim(new Claim("token_source", "dynamic_xyz"));
            }

            _logger.LogDebug("Dynamic JWT validated successfully for subject: {Subject}", sub);

            // Note: We don't do replay protection for Dynamic tokens as they are
            // only used for the exchange endpoint which generates Axon tokens
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Dynamic JWT validation");
            context.Fail($"Token validation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles authentication failures with proper logging
    /// </summary>
    public Task OnAuthenticationFailedAsync(AuthenticationFailedContext context, string scheme)
    {
        _logger.LogWarning(context.Exception,
            "{Scheme} JWT authentication failed. Path: {Path}, Error: {Error}",
            scheme,
            context.HttpContext.Request.Path,
            context.Exception?.Message ?? "Unknown error");

        // Add correlation ID to response headers for debugging
        var correlationId = Guid.NewGuid().ToString();
        context.Response.Headers["X-Correlation-Id"] = correlationId;

        _logger.LogDebug("Authentication failure correlation ID: {CorrelationId}", correlationId);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles authentication challenges with proper logging
    /// </summary>
    public Task OnChallengeAsync(JwtBearerChallengeContext context, string scheme)
    {
        _logger.LogInformation(
            "{Scheme} JWT authentication challenge. Path: {Path}, Error: {Error}, ErrorDescription: {ErrorDescription}",
            scheme,
            context.HttpContext.Request.Path,
            context.Error,
            context.ErrorDescription);

        // Customize challenge response
        if (!string.IsNullOrEmpty(context.Error))
        {
            context.Response.Headers.WWWAuthenticate =
                $"Bearer error=\"{context.Error}\", error_description=\"{context.ErrorDescription}\"";
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles forbidden access with proper logging
    /// </summary>
    public Task OnForbiddenAsync(ForbiddenContext context, string scheme)
    {
        _logger.LogWarning(
            "{Scheme} JWT access forbidden. Path: {Path}, Result: {Result}",
            scheme,
            context.HttpContext.Request.Path,
            context.Result?.Failure?.Message ?? "No failure message");

        return Task.CompletedTask;
    }
}