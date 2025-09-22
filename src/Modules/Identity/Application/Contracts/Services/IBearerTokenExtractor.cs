using BuildingBlocks.Core.Diagnostics.Errors;
using Microsoft.AspNetCore.Http;

namespace Axon.Modules.Identity.Application.Contracts.Services;

/// <summary>
/// Service for extracting and validating Bearer tokens from HTTP requests
/// </summary>
public interface IBearerTokenExtractor
{
    /// <summary>
    /// Extracts bearer token from Authorization header
    /// </summary>
    /// <param name="httpContext">HTTP context containing the request</param>
    /// <returns>Result containing the bearer token or error if invalid/missing</returns>
    Result<string, Error> ExtractBearerToken(HttpContext httpContext);
}