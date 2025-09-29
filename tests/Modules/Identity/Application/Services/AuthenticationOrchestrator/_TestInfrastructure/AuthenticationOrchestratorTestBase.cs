using System.Security.Claims;
using Axon.Modules.Identity.Application.Common;
using Axon.Modules.Identity.Application.Common.Models;
using Axon.Modules.Identity.Application.Configuration;
using Axon.Modules.Identity.Application.Contracts.Providers;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Application.Services;
using Axon.Modules.Identity.Domain.Entities;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;

namespace Axon.Modules.Identity.Application.Tests.Services.AuthenticationOrchestrator._TestInfrastructure;

/// <summary>
/// Base class for AuthenticationOrchestrator tests.
/// Provides common mock setup, test data builders, and helper assertions.
///
/// TESTING PHILOSOPHY (from Sprint 2):
/// - Minimal infrastructure mocking
/// - Focus on observable behavior (authentication succeeds/fails)
/// - Integration-style tests at the orchestrator layer
/// - Avoid brittle .Received() assertions on infrastructure
/// </summary>
public abstract class AuthenticationOrchestratorTestBase
{
    // Core mocks (8 dependencies)
    protected IEnumerable<IAuthenticationProvider> Providers { get; set; } = null!;
    protected IAuthenticationProvider DynamicProvider { get; set; } = null!;
    protected IAuthenticationProvider WalletProvider { get; set; } = null!;
    protected IJwtTokenService TokenService { get; set; } = null!;
    protected UserManager<AxonUserAuth> UserManager { get; set; } = null!;
    protected SignInManager<AxonUserAuth> SignInManager { get; set; } = null!;
    protected IDistributedCache Cache { get; set; } = null!;
    protected ILogger<Application.Services.AuthenticationOrchestrator> Logger { get; set; } = null!;
    protected IChallengeService ChallengeService { get; set; } = null!;
    protected IRefreshTokenProvider RefreshTokenProvider { get; set; } = null!;
    protected IOptions<AuthenticationOptions> AuthOptions { get; set; } = null!;

    // System under test
    protected Application.Services.AuthenticationOrchestrator Orchestrator { get; set; } = null!;

    [SetUp]
    public virtual void SetUp()
    {
        // Initialize all mocks
        DynamicProvider = Substitute.For<IAuthenticationProvider>();
        DynamicProvider.ProviderType.Returns("dynamic");

        WalletProvider = Substitute.For<IAuthenticationProvider>();
        WalletProvider.ProviderType.Returns("wallet");

        Providers = new[] { DynamicProvider, WalletProvider };

        TokenService = Substitute.For<IJwtTokenService>();
        UserManager = MockUserManager();
        SignInManager = MockSignInManager(UserManager);
        Cache = Substitute.For<IDistributedCache>();
        Logger = Substitute.For<ILogger<Application.Services.AuthenticationOrchestrator>>();
        ChallengeService = Substitute.For<IChallengeService>();
        RefreshTokenProvider = Substitute.For<IRefreshTokenProvider>();

        // Setup auth options with defaults
        var authOptions = new AuthenticationOptions
        {
            AccessTokenExpirySeconds = 1800, // 30 minutes
            RefreshTokenExpirySeconds = 2592000 // 30 days
        };
        AuthOptions = Options.Create(authOptions);

        // Configure default behaviors
        ConfigureDefaultMockBehaviors();

        // Create orchestrator instance
        Orchestrator = new Application.Services.AuthenticationOrchestrator(
            Providers,
            TokenService,
            UserManager,
            SignInManager,
            Cache,
            Logger,
            ChallengeService,
            RefreshTokenProvider,
            AuthOptions);
    }

    [TearDown]
    public virtual void TearDown()
    {
        // Cleanup if needed
    }

    #region Mock Setup Helpers

    protected virtual void ConfigureDefaultMockBehaviors()
    {
        // Default: TokenService generates valid tokens
        // Use ReturnsForAnyArgs to avoid Vogen ProviderType uninitialized exception
        TokenService.GenerateAccessTokenAsync(
            new AxonUserId(Guid.NewGuid()),
            Domain.ValueObjects.ProviderType.Dynamic,
            "",
            "",
            30,
            default)
            .ReturnsForAnyArgs(call =>
            {
                var accessToken = $"axon-jwt-{Guid.NewGuid()}";
                var expiresAt = DateTimeOffset.UtcNow.AddMinutes(30);
                var axonToken = new AxonToken(
                    AccessToken: accessToken,
                    TokenType: "Bearer",
                    ExpiresIn: 1800,
                    IssuedAt: DateTimeOffset.UtcNow,
                    ExpiresAt: expiresAt);
                return Task.FromResult(Result.Success<AxonToken, Error>(axonToken));
            });

        // Default: Cache operations succeed (no-op for most tests)
        Cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<byte[]?>(null)); // Cache miss by default

        Cache.SetAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        Cache.RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Default: ChallengeService operations succeed
        var challenge = new AuthenticationChallenge(
            ChainId: "solana",
            Address: "test-address",
            IssuedAt: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Exp: DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds(),
            Nonce: "test-nonce",
            Aud: "test-audience",
            Message: "test-message",
            Mac: "test-mac",
            Mkv: "test-mkv");
        ChallengeService.GenerateChallengeAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success<AuthenticationChallenge, Error>(challenge)));

        // Default: RefreshTokenProvider operations
        RefreshTokenProvider.GetJtiFromToken(Arg.Any<string>())
            .Returns(Guid.NewGuid().ToString());

        RefreshTokenProvider.GetUserIdFromToken(Arg.Any<string>())
            .Returns(Guid.NewGuid());

        RefreshTokenProvider.ValidateAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<UserManager<AxonUserAuth>>(),
            Arg.Any<AxonUserAuth>())
            .Returns(true);

        RefreshTokenProvider.GenerateAsync(
            Arg.Any<string>(),
            Arg.Any<UserManager<AxonUserAuth>>(),
            Arg.Any<AxonUserAuth>())
            .Returns($"refresh-token-{Guid.NewGuid()}");

        // Default: UserManager operations
        UserManager.FindByIdAsync(Arg.Any<string>())
            .Returns(call =>
            {
                var userId = call.ArgAt<string>(0);
                if (Guid.TryParse(userId, out var guid))
                {
                    return Task.FromResult<AxonUserAuth?>(CreateAxonUserAuth(new AxonUserId(guid)));
                }
                return Task.FromResult<AxonUserAuth?>(null);
            });

        UserManager.UpdateSecurityStampAsync(Arg.Any<AxonUserAuth>())
            .Returns(IdentityResult.Success);

        // Default: SignInManager operations
        SignInManager.SignOutAsync()
            .Returns(Task.CompletedTask);
    }

    protected static UserManager<AxonUserAuth> MockUserManager()
    {
        var store = Substitute.For<IUserStore<AxonUserAuth>>();
        var userManager = Substitute.For<UserManager<AxonUserAuth>>(
            store,
            null, null, null, null, null, null, null, null);
        return userManager;
    }

    protected static SignInManager<AxonUserAuth> MockSignInManager(UserManager<AxonUserAuth> userManager)
    {
        var contextAccessor = Substitute.For<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        var claimsFactory = Substitute.For<IUserClaimsPrincipalFactory<AxonUserAuth>>();

        var signInManager = Substitute.For<SignInManager<AxonUserAuth>>(
            userManager,
            contextAccessor,
            claimsFactory,
            null, null, null, null);

        return signInManager;
    }

    #endregion

    #region Test Data Builders

    /// <summary>
    /// Creates an AxonUserAuth (Identity user) for testing
    /// </summary>
    protected static AxonUserAuth CreateAxonUserAuth(
        AxonUserId? principalId = null,
        string? dynamicUserId = null,
        string? email = null,
        string? providerType = null)
    {
        var user = AxonUserAuth.Create(
            principalId: principalId ?? new AxonUserId(Guid.NewGuid()),
            providerType: providerType ?? "dynamic",
            issuer: "https://app.dynamic.xyz",
            subject: dynamicUserId ?? Guid.NewGuid().ToString(),
            dynamicEnvironmentId: "test-env-123",
            dynamicUserId: dynamicUserId ?? Guid.NewGuid().ToString());

        if (email != null)
        {
            user.Email = email;
            user.NormalizedEmail = email.ToUpperInvariant();
        }

        return user;
    }

    /// <summary>
    /// Creates a mock AuthenticationData response from provider
    /// </summary>
    protected static AuthenticationData CreateAuthenticationData(
        AxonUserAuth? user = null,
        string? providerType = null,
        DateTime? tokenExpiresAt = null,
        Dictionary<string, object>? additionalClaims = null)
    {
        return new AuthenticationData(
            User: user ?? CreateAxonUserAuth(),
            ProviderType: providerType ?? "dynamic",
            AdditionalClaims: additionalClaims ?? new Dictionary<string, object>(),
            TokenExpiresAt: tokenExpiresAt ?? DateTime.UtcNow.AddMinutes(30));
    }

    /// <summary>
    /// Creates a ClaimsPrincipal for testing
    /// </summary>
    protected static ClaimsPrincipal CreateClaimsPrincipal(
        string? userId = null,
        string? subject = null,
        string? issuer = null)
    {
        var claims = new List<Claim>
        {
            new("axon_user_id", userId ?? Guid.NewGuid().ToString()),
            new("sub", subject ?? Guid.NewGuid().ToString()),
            new("iss", issuer ?? "https://app.dynamic.xyz")
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    #endregion
}