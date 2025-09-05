using Axon.Modules.Identity.Application.Queries.GetCurrentUser;
using Axon.Modules.Identity.Infrastructure.Authentication;
using Axon.Modules.Identity.Infrastructure.Services;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Web.Extensions;
using FastEndpoints;
using Microsoft.Net.Http.Headers;

namespace Axon.Api.Endpoints.V1.Identity.Queries.GetCurrentUser;

/// <summary>
/// Endpoint for retrieving current authenticated user information with ETag support
/// </summary>
public class GetCurrentUserEndpoint : EndpointWithoutRequest<CurrentUserResult>
{
    private readonly IMeReader _meReader;
    private readonly ILogger<GetCurrentUserEndpoint> _logger;

    public GetCurrentUserEndpoint(IMeReader meReader, ILogger<GetCurrentUserEndpoint> logger)
    {
        _meReader = meReader ?? throw new ArgumentNullException(nameof(meReader));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override void Configure()
    {
        Get("/api/v1/auth/me");
        AuthSchemes(AuthenticationSchemes.DynamicJwt);
        
        Summary(s =>
        {
            s.Summary = "Get current authenticated user information";
            s.Description = "Retrieves the current authenticated user's profile and connected wallet information with ETag support";
            s.Responses[200] = "Successfully retrieved current user information including profile and wallets";
            s.Responses[304] = "Not Modified - content hasn't changed since last request";
            s.Responses[400] = "Invalid request parameters";
            s.Responses[401] = "User not authenticated";
            s.Responses[404] = "Principal not found (expected for new users before first POST /auth/exchange)";
            s.Responses[500] = "Internal server error";
        });

        Description(d => d
            .WithTags("Authentication")
            .Produces<CurrentUserResult>(200, "application/json")
            .Produces(304)
            .ProducesProblemFE(401)
            .ProducesProblemFE(404)
            .ProducesProblemFE(500));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        using var activity = _logger.BeginScope("GetCurrentUser");

        try
        {
            // Get current user data with ETag
            var result = await _meReader.GetAsync(ct);
            
            if (result.IsFailure)
            {
                await HandleErrorAsync(result.Error);
                return;
            }

            var meData = result.Value;
            var currentETag = meData.ETag;

            // Add AxonId to logging context
            using var userScope = _logger.BeginScope("AxonId:{AxonId}", meData.UserData.Profile.AxonId);

            // Check If-None-Match header for conditional GET support
            var ifNoneMatch = HttpContext.Request.Headers.IfNoneMatch.ToString();
            if (!string.IsNullOrEmpty(ifNoneMatch) && ifNoneMatch.Contains(currentETag, StringComparison.Ordinal))
            {
                _logger.LogDebug("ETag {ETag} matches If-None-Match header, returning 304", currentETag);
                
                // Set ETag header and return 304 Not Modified
                HttpContext.Response.Headers.ETag = currentETag;
                HttpContext.Response.Headers.CacheControl = "private, max-age=30";
                HttpContext.Response.StatusCode = 304;
                
                return;
            }

            // Set caching headers
            HttpContext.Response.Headers.ETag = currentETag;
            HttpContext.Response.Headers.CacheControl = "private, max-age=30";

            _logger.LogInformation("Successfully returned user data for {AxonId} with ETag {ETag}", 
                meData.UserData.Profile.AxonId, currentETag);

            Response = meData.UserData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in GetCurrentUserEndpoint");
            var error = Error.Failure("An error occurred retrieving user data", "IDENTITY.GET_CURRENT_USER.FAILED");
            await HandleErrorAsync(error);
        }
    }

    private async Task HandleErrorAsync(Error error)
    {
        _logger.LogWarning("Request failed: {ErrorCode} - {ErrorMessage}", error.Code, error.Message);
        await HttpContext.SendProblemDetailsAsync(error, CancellationToken.None);
    }
}