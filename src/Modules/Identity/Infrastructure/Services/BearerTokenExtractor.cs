using Axon.Modules.Identity.Application.Contracts.Services;
using BuildingBlocks.Core.Diagnostics.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Service for extracting and validating Bearer tokens from HTTP requests
/// </summary>
public class BearerTokenExtractor : IBearerTokenExtractor
{
    private readonly ILogger<BearerTokenExtractor> _logger;

    public BearerTokenExtractor(ILogger<BearerTokenExtractor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Extracts bearer token from Authorization header
    /// </summary>
    /// <param name="httpContext">HTTP context containing the request</param>
    /// <returns>Result containing the bearer token or error if invalid/missing</returns>
    public Result<string, Error> ExtractBearerToken(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        // Extract authorization header
        var authHeader = httpContext.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader) ||
            !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Request missing Authorization header or Bearer token for path {Path}",
                httpContext.Request.Path);
            return Result.Failure<string, Error>(
                Error.Unauthorized("Authorization header with Bearer token is required"));
        }

        // Extract token value
        var bearerToken = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            _logger.LogWarning("Request has empty Bearer token for path {Path}",
                httpContext.Request.Path);
            return Result.Failure<string, Error>(
                Error.Unauthorized("Bearer token cannot be empty"));
        }

        return Result.Success<string, Error>(bearerToken);
    }
}