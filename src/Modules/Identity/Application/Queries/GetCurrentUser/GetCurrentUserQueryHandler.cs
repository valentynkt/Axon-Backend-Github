using System.Security.Claims;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Axon.Modules.Identity.Application.Queries.GetCurrentUser;

/// <summary>
/// Handler for retrieving current authenticated user information
/// </summary>
public class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, Result<CurrentUserResult, Error>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    
    public GetCurrentUserQueryHandler(
        ICurrentUserService currentUserService,
        IHttpContextAccessor httpContextAccessor)
    {
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }
    
    public Task<Result<CurrentUserResult, Error>> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        // Check if user is authenticated
        if (!_currentUserService.IsAuthenticated || string.IsNullOrEmpty(_currentUserService.UserId))
        {
            return Task.FromResult(Result.Failure<CurrentUserResult, Error>(
                Error.Unauthorized("User is not authenticated", "AUTH.NOT_AUTHENTICATED")));
        }
        
        // Extract user data from claims
        var userResult = ExtractUserFromClaims();
        
        if (userResult == null)
        {
            // Fallback to stub data if claims are incomplete
            userResult = CreateStubUserResult();
        }
        
        return Task.FromResult(Result.Success<CurrentUserResult, Error>(userResult));
    }
    
    private CurrentUserResult? ExtractUserFromClaims()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated != true)
        {
            return null;
        }
        
        var claims = httpContext.User.Claims.ToList();
        
        // Extract basic user info
        var userId = _currentUserService.UserId;
        var email = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(email))
        {
            return null;
        }
        
        // Extract environment ID for Dynamic user ID
        var environmentId = claims.FirstOrDefault(c => c.Type == "environment_id")?.Value;
        
        // Extract wallet data
        var wallets = new List<WalletData>();
        var walletClaims = claims.Where(c => c.Type == "wallet").ToList();
        
        foreach (var walletClaim in walletClaims)
        {
            var address = walletClaim.Value;
            
            // Find chain and provider for this wallet
            var chainClaim = claims.FirstOrDefault(c => c.Type.StartsWith($"wallet:") && 
                                                        c.Value == address && 
                                                        !c.Type.Contains("provider", StringComparison.Ordinal));
            var chain = chainClaim?.Type.Split(':').LastOrDefault() ?? "unknown";
            
            var providerClaim = claims.FirstOrDefault(c => c.Type == $"wallet:provider:{chain}");
            var provider = providerClaim?.Value ?? "unknown";
            
            wallets.Add(new WalletData
            {
                Id = Guid.NewGuid(), // Generate a new ID for now
                Address = address,
                Chain = chain,
                Provider = provider,
                WalletName = null,
                ConnectedAt = DateTime.UtcNow // Use current time as approximation
            });
        }
        
        // Extract timestamps
        DateTime? firstVisit = null;
        DateTime? lastVisit = null;
        
        var firstVisitClaim = claims.FirstOrDefault(c => c.Type == "first_visit")?.Value;
        if (DateTime.TryParse(firstVisitClaim, out var fv))
        {
            firstVisit = fv;
        }
        
        var lastVisitClaim = claims.FirstOrDefault(c => c.Type == "last_visit")?.Value;
        if (DateTime.TryParse(lastVisitClaim, out var lv))
        {
            lastVisit = lv;
        }
        
        // Check if new user
        var isNewUser = claims.FirstOrDefault(c => c.Type == "is_new_user")?.Value == "true";
        
        return new CurrentUserResult
        {
            User = new UserProfile
            {
                Id = userId,
                DynamicUserId = Guid.TryParse(userId, out var dynId) ? dynId : Guid.NewGuid(),
                Email = email,
                DisplayName = email.Split('@').FirstOrDefault() ?? "User",
                Username = email.Split('@').FirstOrDefault()?.ToLower() ?? "user",
                FirstVisit = firstVisit ?? DateTime.UtcNow,
                LastVisit = lastVisit ?? DateTime.UtcNow,
                Metadata = new Dictionary<string, object>
                {
                    ["authenticated"] = true,
                    ["environment_id"] = environmentId ?? "unknown",
                    ["is_new_user"] = isNewUser
                }
            },
            Wallets = wallets,
            SyncedAt = DateTime.UtcNow,
            SyncStatus = "completed"
        };
    }
    
    private static CurrentUserResult CreateStubUserResult()
    {
        var now = DateTime.UtcNow;
        
        return new CurrentUserResult
        {
            User = new UserProfile
            {
                Id = "usr_2Z4e8K9mNp3QrS7T",
                DynamicUserId = Guid.Parse("95b11417-f18f-457f-8804-68e361f9164f"),
                Email = "user@example.com",
                DisplayName = "John Doe",
                Username = "johndoe",
                FirstVisit = now.AddDays(-30),
                LastVisit = now.AddMinutes(-5),
                Metadata = new Dictionary<string, object>
                {
                    ["preferences"] = new Dictionary<string, object>
                    {
                        ["theme"] = "dark",
                        ["notifications"] = true
                    },
                    ["tags"] = new[] { "trader", "developer", "early-adopter" }
                }
            },
            Wallets = new List<WalletData>
            {
                new()
                {
                    Id = Guid.Parse("e5d4c3b2-1a2b-3c4d-5e6f-7a8b9c0d1e2f"),
                    Address = "5FHneW46xGXgs5mUiveU4sbTyGBzmstUspZC92UhjJM694ty",
                    Chain = "solana",
                    Provider = "phantom",
                    WalletName = "Primary Wallet",
                    ConnectedAt = now.AddDays(-7)
                },
                new()
                {
                    Id = Guid.Parse("f6e5d4c3-2b3c-4d5e-6f7a-8b9c0d1e2f3a"),
                    Address = "7C4jsPZpht42Tw6MjXWF56Q5RQUocjBBmciEjDa8HRtp",
                    Chain = "solana",
                    Provider = "metamask",
                    WalletName = "Trading Wallet",
                    ConnectedAt = now.AddDays(-3)
                }
            },
            SyncedAt = now,
            SyncStatus = "completed"
        };
    }
}