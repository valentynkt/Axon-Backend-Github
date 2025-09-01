using FastEndpoints;
using Axon.Modules.Identity.Infrastructure.Services;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Abstractions.Authentication;
using System.Security.Claims;

namespace Axon.Api.Endpoints.V1.Auth;

public class ExchangeTokenEndpoint : Endpoint<EmptyRequest, ExchangeTokenResponse>
{
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<ExchangeTokenEndpoint> _logger;
    
    public ExchangeTokenEndpoint(
        ICurrentUserService currentUser,
        ILogger<ExchangeTokenEndpoint> logger)
    {
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override void Configure()
    {
        Post("/api/v1/auth/exchange");
        AuthSchemes("DynamicXyz"); // Require Dynamic.xyz JWT authentication
        
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

    public override Task HandleAsync(EmptyRequest _, CancellationToken ct)
    {
        // Authentication middleware has already validated the token
        // Extract user information from the authenticated claims
        var userId = _currentUser.UserId;
        var email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogError("User ID not found in authenticated claims");
            ThrowError("User information not available", statusCode: 500);
        }
        
        // Extract wallet information from claims
        var wallets = ExtractWalletsFromClaims(User.Claims).ToList();
        
        var response = new ExchangeTokenResponse(userId, email, wallets);
        
        _logger.LogInformation("Token exchange successful for user {UserId}", userId);
        Response = response;
        
        return Task.CompletedTask;
    }

    private static IEnumerable<WalletInfo> ExtractWalletsFromClaims(IEnumerable<Claim> claims)
    {
        // Group wallet claims by chain to reconstruct wallet information
        var walletClaims = claims
            .Where(c => c.Type.StartsWith("wallet:") || c.Type == "wallet")
            .GroupBy(c => c.Type.Contains(':') ? c.Type.Split(':')[1] : "unknown")
            .Where(g => g.Key != "unknown");

        foreach (var chainGroup in walletClaims)
        {
            var chain = chainGroup.Key;
            var address = chainGroup.FirstOrDefault(c => c.Type == $"wallet:{chain}")?.Value ?? 
                         chainGroup.FirstOrDefault(c => c.Type == "wallet")?.Value;
            var provider = chainGroup.FirstOrDefault(c => c.Type == $"wallet:provider:{chain}")?.Value;
            
            if (!string.IsNullOrEmpty(address))
            {
                yield return new WalletInfo(
                    Id: Guid.NewGuid(),
                    Address: address,
                    Chain: chain,
                    Provider: provider ?? "unknown",
                    WalletName: null,
                    ConnectedAt: null
                );
            }
        }
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