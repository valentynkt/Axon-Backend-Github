using System.Text.Json;
using Axon.Api.Contracts.Common;

namespace Axon.Api.Middleware;

/// <summary>
/// Middleware that formats 401 Unauthorized responses as JSON error messages.
/// Respects Clean Architecture by keeping response formatting in the presentation layer.
/// </summary>
public sealed class AuthenticationErrorMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<AuthenticationErrorMiddleware> _logger;

    public AuthenticationErrorMiddleware(RequestDelegate next, ILogger<AuthenticationErrorMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Capture the original response body stream
        var originalBodyStream = context.Response.Body;

        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await _next(context);

            // Only process 401 Unauthorized responses with empty body
            if (context.Response.StatusCode == StatusCodes.Status401Unauthorized && responseBody.Length == 0)
            {
                await FormatUnauthorizedResponseAsync(context);
            }
            else
            {
                // Copy the response back to the original stream
                context.Response.Body = originalBodyStream;
                responseBody.Seek(0, SeekOrigin.Begin);
                await responseBody.CopyToAsync(originalBodyStream);
            }
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }

    private async Task FormatUnauthorizedResponseAsync(HttpContext context)
    {
        // Extract error information from HttpContext.Items (set by JwtEventHandlers)
        var errorDescription = context.Items["AuthChallenge_ErrorDescription"] as string;
        var error = context.Items["AuthChallenge_Error"] as string;

        var errorMessage = !string.IsNullOrEmpty(errorDescription)
            ? errorDescription
            : "Authentication failed";

        var errorResponse = ApiError.Unauthorized(errorMessage);

        _logger.LogDebug(
            "Formatting 401 response as JSON. Path: {Path}, Error: {Error}",
            context.Request.Path,
            error);

        // Write JSON response to the response body
        context.Response.ContentType = "application/json";

        var json = JsonSerializer.Serialize(errorResponse, JsonOptions);

        await context.Response.WriteAsync(json);
    }
}

/// <summary>
/// Extension methods for registering AuthenticationErrorMiddleware.
/// </summary>
public static class AuthenticationErrorMiddlewareExtensions
{
    /// <summary>
    /// Registers the AuthenticationErrorMiddleware in the request pipeline.
    /// Should be called after UseAuthentication() and UseAuthorization().
    /// </summary>
    public static IApplicationBuilder UseAuthenticationErrorFormatting(this IApplicationBuilder app)
    {
        return app.UseMiddleware<AuthenticationErrorMiddleware>();
    }
}