using System.Globalization;
using Axon.Api.Contracts.V1.Identity.Authentication;
using Axon.Api.Modules;
using Axon.Modules.Identity.Application.Common.Constants;
using Axon.Modules.Identity.Application.Services;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using FastEndpoints;

namespace Axon.Api.Endpoints.V1.Identity.Commands.ExchangeDynamicToken;

/// <summary>
/// Endpoint for exchanging a Dynamic JWT token for Axon identity
/// </summary>
public sealed class ExchangeDynamicTokenEndpoint : Endpoint<ExchangeDynamicTokenRequest, ExchangeDynamicTokenResponse>
{
    private readonly IDynamicAuthOrchestrator _orchestrator;
    private readonly Axon.Modules.Identity.Application.Services.IRateLimitService _rateLimitService;
    private readonly ILogger<ExchangeDynamicTokenEndpoint> _logger;

    public ExchangeDynamicTokenEndpoint(
        IDynamicAuthOrchestrator orchestrator,
        Axon.Modules.Identity.Application.Services.IRateLimitService rateLimitService,
        ILogger<ExchangeDynamicTokenEndpoint> logger)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _rateLimitService = rateLimitService ?? throw new ArgumentNullException(nameof(rateLimitService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override void Configure()
    {
        Post("/api/v1/auth/exchange");
        AllowAnonymous(); // JWT validation happens inside the orchestrator
        
        Description(d => d
            .WithTags("Authentication")
            .Accepts<ExchangeDynamicTokenRequest>("application/json")
            .Produces<ExchangeDynamicTokenResponse>(200, "application/json")
            .ProducesProblemFE(400)
            .ProducesProblemFE(401)
            .ProducesProblemFE(409)
            .ProducesProblemFE(422)
            .ProducesProblemFE(429)
            .ProducesProblemFE(500));

        Summary(s =>
        {
            s.Summary = "Exchange Dynamic JWT for Axon identity";
            s.Description = """
                Validates a Dynamic.xyz JWT token and creates/updates the corresponding Axon principal with all associated wallets.
                
                **Authentication Flow:**
                1. Extract Bearer token from Authorization header
                2. Validate JWT signature against Dynamic.xyz JWKS
                3. Create or update Axon principal and credential
                4. Process all wallets: activity tracking, ownership linking, default assignment
                5. Return detailed exchange metrics
                
                **Features:**
                - Idempotent operations - safe to retry
                - Soft-failure handling for individual wallets
                - Automatic default wallet assignment (first per chain)
                - Comprehensive audit logging and metrics
                - JWT replay attack protection (if jti claim present)
                
                **Rate Limiting:** 10 requests per minute per IP
                """;
            s.Responses[200] = "Successfully exchanged JWT token for Axon identity. Returns principal ID and detailed wallet processing metrics.";
            s.Responses[400] = "Invalid request format, malformed JWT, or JWT size exceeds limits (8KB max)";
            s.Responses[401] = "Invalid, expired, malformed JWT token, or replay attempt detected";
            s.Responses[409] = "Wallet ownership conflict - wallet already owned by another principal";
            s.Responses[422] = "Business rule violation during exchange (e.g., principal creation constraints)";
            s.Responses[429] = "Rate limit exceeded - too many requests";
            s.Responses[500] = "Internal server error or external service unavailable";
            
            // Add request example
            s.ExampleRequest = new ExchangeDynamicTokenRequest
            {
                // Request body is typically empty since JWT comes in Authorization header
            };
            
            // Add response examples
            s.ResponseExamples[200] = new ExchangeDynamicTokenResponse(
                AxonId: "01HKQR8X9N2Y3Z4A5B6C7D8E9F",
                Created: true,
                    WalletsProcessed: 3,
                    WalletsLinked: 2,
                    DefaultsApplied: 2,
                    Skipped: 1,
                    Conflicts: 0
                );
            
            s.ResponseExamples[401] = new { error = "AUTH.TOKEN_EXPIRED: Token has expired" };
            
            s.ResponseExamples[409] = new { error = "Wallet ownership conflict: 0x123...abc already owned by another principal" };
        });
    }

    public override async Task HandleAsync(ExchangeDynamicTokenRequest request, CancellationToken ct)
    {
        _logger.LogDebug("Processing Dynamic JWT exchange request");

        // Check rate limit first
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var rateLimitResult = await _rateLimitService.CheckRateLimitAsync(clientIp, "jwt_exchange", ct);
        if (rateLimitResult.IsFailure)
        {
            var rateLimitError = rateLimitResult.Error;
            _logger.LogWarning("Rate limit exceeded for IP {ClientIp}: {Message}", clientIp, rateLimitError.Message);
            
            HttpContext.Response.Headers.RetryAfter = rateLimitError.RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
            HttpContext.Response.Headers["X-RateLimit-Remaining"] = rateLimitError.RequestsRemaining.ToString(CultureInfo.InvariantCulture);
            HttpContext.Response.Headers["X-RateLimit-Reset"] = rateLimitError.WindowResetAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
            
            ThrowError(rateLimitError.Message, statusCode: 429);
        }

        // Extract JWT from Authorization header
        var jwt = ExtractJwtFromAuthorizationHeader();
        if (string.IsNullOrWhiteSpace(jwt))
        {
            _logger.LogWarning("Exchange request missing Authorization header or Bearer token");
            ThrowError("Authorization header with Bearer token is required", statusCode: 401);
        }

        // Validate JWT format and size before processing
        if (!IsValidJwtFormat(jwt))
        {
            _logger.LogWarning("Invalid JWT format received");
            ThrowError("Invalid JWT token format", statusCode: 400);
        }

        // Delegate to orchestrator for the complete flow
        var result = await _orchestrator.ExchangeAsync(jwt, ct);
        
        if (result.IsFailure)
        {
            HandleError(result.Error);
            return;
        }

        // Map domain result to API response
        var outcome = result.Value;
        
        // Add AxonId to logging context for structured observability
        using var axonScope = _logger.BeginScope("AxonId:{AxonId}", outcome.AxonId);
        
        var response = new ExchangeDynamicTokenResponse(
            AxonId: outcome.AxonId,
            Created: outcome.Created,
            WalletsProcessed: outcome.WalletsProcessed,
            WalletsLinked: outcome.WalletsLinked,
            DefaultsApplied: outcome.DefaultsApplied,
            Skipped: outcome.Skipped,
            Conflicts: outcome.Conflicts
        );

        _logger.LogInformation("JWT exchange completed successfully: Created={Created}, WalletsProcessed={WalletsProcessed}, WalletsLinked={WalletsLinked}", 
            outcome.Created, outcome.WalletsProcessed, outcome.WalletsLinked);
        Response = response;
    }

    /// <summary>
    /// Extracts JWT token from Authorization: Bearer header
    /// </summary>
    private string? ExtractJwtFromAuthorizationHeader()
    {
        var authHeader = HttpContext.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader))
            return null;

        const string bearerPrefix = "Bearer ";
        if (!authHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
            return null;

        return authHeader[bearerPrefix.Length..].Trim();
    }

    /// <summary>
    /// Validates JWT token format and basic constraints
    /// </summary>
    private static bool IsValidJwtFormat(string jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt))
            return false;

        // JWT should not exceed reasonable size limits (8KB max)
        if (jwt.Length > 8192)
            return false;

        // JWT should have exactly 2 dots (header.payload.signature)
        var parts = jwt.Split('.');
        if (parts.Length != 3)
            return false;

        // Each part should not be empty and should be base64url encoded
        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part))
                return false;
                
            // Basic base64url validation - should only contain valid characters
            if (!IsValidBase64Url(part))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Validates if string contains only valid base64url characters
    /// </summary>
    private static bool IsValidBase64Url(string input)
    {
        if (string.IsNullOrEmpty(input))
            return false;

        // Base64url uses: A-Z, a-z, 0-9, -, _
        foreach (char c in input)
        {
            if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
                return false;
        }

        return true;
    }

    /// <summary>
    /// Maps domain errors to appropriate HTTP responses
    /// </summary>
    private void HandleError(Error error)
    {
        _logger.LogWarning("JWT exchange failed: ErrorType={ErrorType}, ErrorCode={ErrorCode}, ErrorMessage={ErrorMessage}", 
            error.Type, error.Code, error.Message);

        var statusCode = error.Type switch
        {
            ErrorType.Validation => 400,
            ErrorType.Unauthorized => 401,
            ErrorType.Conflict => 409,
            ErrorType.BusinessRule => 422,
            ErrorType.External => 500,
            _ => 500
        };

        // Map specific auth error codes
        if (error.Code?.StartsWith("AUTH.", StringComparison.Ordinal) == true)
        {
            statusCode = error.Code switch
            {
                DynamicAuthConstants.ErrorCodes.TokenRequired or DynamicAuthConstants.ErrorCodes.InvalidTokenFormat => 400,
                DynamicAuthConstants.ErrorCodes.TokenExpired or DynamicAuthConstants.ErrorCodes.InvalidSignature or DynamicAuthConstants.ErrorCodes.ValidationFailed => 401,
                DynamicAuthConstants.ErrorCodes.PrincipalConflict => 409,
                _ => 500
            };
        }

        ThrowError($"{error.Code}: {error.Message}", statusCode: statusCode);
    }
}