using FastEndpoints;
using Axon.Modules.Identity.Infrastructure.Services;
using BuildingBlocks.Core.Diagnostics.Errors;

namespace Axon.Api.Endpoints.V1.Auth;

public class ExchangeTokenEndpoint : Endpoint<EmptyRequest, ExchangeTokenResponse>
{
    private readonly IDynamicAuthService _dynamicAuth;
    private readonly ILogger<ExchangeTokenEndpoint> _logger;
    
    public ExchangeTokenEndpoint(
        IDynamicAuthService dynamicAuth,
        ILogger<ExchangeTokenEndpoint> logger)
    {
        _dynamicAuth = dynamicAuth ?? throw new ArgumentNullException(nameof(dynamicAuth));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override void Configure()
    {
        Post("/api/v1/auth/exchange");
        AllowAnonymous();
        
        Summary(s =>
        {
            s.Summary = "Exchange Dynamic.xyz JWT for user information";
            s.Description = "Validates a Dynamic.xyz JWT token and returns user profile with wallet information";
            s.Responses[200] = "Token validated successfully";
            s.Responses[401] = "Invalid or expired token";
            s.Responses[503] = "Dynamic.xyz service unavailable";
        });
        
        Tags("Authentication");
    }

    public override async Task HandleAsync(EmptyRequest _, CancellationToken ct)
    {
        var authHeader = HttpContext.Request.Headers.Authorization.FirstOrDefault();
        var token = authHeader?.Replace("Bearer ", "", StringComparison.Ordinal);
            
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("Token exchange attempted without token");
            HttpContext.Response.StatusCode = 401;
            return;
        }
        
        var result = await _dynamicAuth.ValidateTokenAsync(token, ct);
        
        if (result.IsFailure)
        {
            _logger.LogWarning("Token validation failed: {ErrorCode}", result.Error.Code);
            
            var statusCode = result.Error.Type switch
            {
                ErrorType.Unavailable => 503,
                ErrorType.Timeout => 503,
                ErrorType.Network => 503,
                _ => 401
            };
            
            HttpContext.Response.StatusCode = statusCode;
            return;
        }
        
        var response = MapToResponse(result.Value);
        Response = response;
    }

    private static ExchangeTokenResponse MapToResponse(DynamicUserData user)
    {
        var wallets = user.Wallets.Select(w => new WalletInfo(
            Guid.TryParse(w.Id, out var walletId) ? walletId : Guid.NewGuid(),
            w.Address,
            w.Chain,
            w.Provider,
            w.WalletName,
            w.ConnectedAt
        )).ToList();

        return new ExchangeTokenResponse(
            user.UserId,
            user.Email,
            wallets
        );
    }
}

public record ExchangeTokenResponse(
    string UserId,
    string Email,
    List<WalletInfo> Wallets
);

public record WalletInfo(
    Guid Id,
    string Address,
    string Chain,
    string Provider,
    string? WalletName,
    DateTime? ConnectedAt
);