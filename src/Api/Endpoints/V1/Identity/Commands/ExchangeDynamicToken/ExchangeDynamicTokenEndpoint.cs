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
    private readonly ILogger<ExchangeDynamicTokenEndpoint> _logger;

    public ExchangeDynamicTokenEndpoint(
        IDynamicAuthOrchestrator orchestrator,
        ILogger<ExchangeDynamicTokenEndpoint> logger)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
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
            .ProducesProblemFE(422)
            .ProducesProblemFE(500));

        Summary(s =>
        {
            s.Summary = "Exchange Dynamic JWT for Axon identity";
            s.Description = "Validates a Dynamic.xyz JWT token and creates/updates the corresponding Axon principal with all associated wallets";
            s.Responses[200] = "Successfully exchanged JWT token for Axon identity";
            s.Responses[400] = "Invalid request format";
            s.Responses[401] = "Invalid, expired, or malformed JWT token";
            s.Responses[422] = "Business rule violation during exchange";
            s.Responses[500] = "Internal server error";
        });
    }

    public override async Task HandleAsync(ExchangeDynamicTokenRequest request, CancellationToken ct)
    {
        _logger.LogDebug("Processing Dynamic JWT exchange request");

        // Extract JWT from Authorization header
        var jwt = ExtractJwtFromAuthorizationHeader();
        if (string.IsNullOrWhiteSpace(jwt))
        {
            _logger.LogWarning("Exchange request missing Authorization header or Bearer token");
            ThrowError("Authorization header with Bearer token is required", statusCode: 401);
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