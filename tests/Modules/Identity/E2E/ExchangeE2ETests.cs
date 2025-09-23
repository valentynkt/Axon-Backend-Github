using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Axon.Modules.Identity.Application.Contracts.ExternalServices;
using Axon.Modules.Identity.Application.DTOs.Exchange;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Infrastructure.Services;
using Axon.Modules.Identity.E2E.Infrastructure;
using Axon.Modules.Identity.Infrastructure.Persistence.DbInvariants;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.E2E.Tests;

/// <summary>
/// End-to-End tests for the complete Auth Story 1 exchange flow.
/// Tests the full pipeline from Dynamic JWT → Exchange → Axon Access Token
/// with real database, authentication service, and HTTP pipeline.
/// </summary>
[TestFixture]
public sealed class ExchangeE2ETests : E2ETestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private IDynamicAuthService _mockDynamicAuthService = null!;
    private AuthenticationService _authService = null!;

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        _mockDynamicAuthService = Substitute.For<IDynamicAuthService>();

        // Replace IDynamicAuthService with mock
        services.Remove(services.Single(d => d.ServiceType == typeof(IDynamicAuthService)));
        services.AddSingleton(_mockDynamicAuthService);
    }

    protected override async Task SetUpDerivedAsync()
    {
        // Get authentication service for token operations
        using var scope = Factory.Services.CreateScope();
        _authService = scope.ServiceProvider.GetRequiredService<AuthenticationService>();

        await base.SetUpDerivedAsync();
    }

    #region Complete Exchange Flow Tests

    [Test]
    public async Task CompleteExchangeFlow_NewUserWithDynamicJWT_ReturnsAccessTokens()
    {
        // Arrange - Mock Dynamic JWT validation
        var dynamicUserId = "new-e2e-user-001";
        var mockDynamicUserData = CreateMockDynamicUserData(dynamicUserId, withWallets: true);
        var mockClaimsPrincipal = CreateMockClaimsPrincipal(dynamicUserId, "https://app.dynamic.xyz/test-env");

        ConfigureDynamicAuthServiceMocks(mockDynamicUserData, mockClaimsPrincipal);

        // Create Dynamic JWT token (mock)
        var dynamicJwt = CreateMockDynamicJwt(dynamicUserId);
        SetAuthorizationHeader(dynamicJwt);

        // Act - Exchange Dynamic JWT for Axon tokens
        using var content = CreateJsonContent("{}");
        var exchangeResponse = await HttpClient.PostAsync("/api/v1/auth/exchange", content);

        // Assert Exchange Response
        exchangeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var exchangeContent = await exchangeResponse.Content.ReadAsStringAsync();
        var exchangeResult = JsonSerializer.Deserialize<ExchangeTokenResponseDto>(exchangeContent, JsonOptions);

        exchangeResult.ShouldNotBeNull();
        exchangeResult.AccessToken.ShouldNotBeNullOrEmpty();
        exchangeResult.RefreshToken.ShouldNotBeNullOrEmpty();
        exchangeResult.TokenType.ShouldBe("Bearer");
        exchangeResult.Created.ShouldBeTrue();
        exchangeResult.WalletsProcessed.ShouldBe(2); // Ethereum + Solana from mock
        exchangeResult.WalletsLinked.ShouldBe(2);
        exchangeResult.DefaultsApplied.ShouldBe(2); // One per chain

        // Verify token structure and validity
        var tokenValidationResult = await _authService.ValidateTokenAsync(exchangeResult.AccessToken);
        tokenValidationResult.IsSuccess.ShouldBeTrue();
        tokenValidationResult.Value.TokenType.ShouldBe(TokenType.AxonAccessToken);
        tokenValidationResult.Value.AxonUserId.Value.ShouldBe(Guid.Parse(exchangeResult.AxonUserId));

        // Test using the access token for authenticated requests
        await VerifyTokenWorksForAuthenticatedRequests(exchangeResult.AccessToken);
    }

    [Test]
    public async Task CompleteExchangeFlow_ExistingUserResubmission_ReturnsConsistentResults()
    {
        // Arrange - First exchange to create user
        var dynamicUserId = "existing-e2e-user-002";
        var mockDynamicUserData = CreateMockDynamicUserData(dynamicUserId, withWallets: false);
        var mockClaimsPrincipal = CreateMockClaimsPrincipal(dynamicUserId, "https://app.dynamic.xyz/test-env");

        ConfigureDynamicAuthServiceMocks(mockDynamicUserData, mockClaimsPrincipal);

        var dynamicJwt = CreateMockDynamicJwt(dynamicUserId);
        SetAuthorizationHeader(dynamicJwt);

        // Act - First exchange
        using var firstContent = CreateJsonContent("{}");
        var firstResponse = await HttpClient.PostAsync("/api/v1/auth/exchange", firstContent);

        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var firstResponseContent = await firstResponse.Content.ReadAsStringAsync();
        var firstResult = JsonSerializer.Deserialize<ExchangeTokenResponseDto>(firstResponseContent, JsonOptions);

        firstResult.ShouldNotBeNull();
        firstResult.Created.ShouldBeTrue();

        // Act - Second exchange (resubmission)
        using var secondContent = CreateJsonContent("{}");
        var secondResponse = await HttpClient.PostAsync("/api/v1/auth/exchange", secondContent);

        // Assert
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var secondResponseContent = await secondResponse.Content.ReadAsStringAsync();
        var secondResult = JsonSerializer.Deserialize<ExchangeTokenResponseDto>(secondResponseContent, JsonOptions);

        secondResult.ShouldNotBeNull();
        secondResult.Created.ShouldBeFalse(); // Should find existing
        secondResult.AxonUserId.ShouldBe(firstResult.AxonUserId); // Same user ID
        secondResult.AccessToken.ShouldNotBe(firstResult.AccessToken); // Different tokens (new issue time)
        secondResult.RefreshToken.ShouldNotBe(firstResult.RefreshToken); // Different refresh tokens

        // Both tokens should work
        var firstTokenValidation = await _authService.ValidateTokenAsync(firstResult.AccessToken);
        var secondTokenValidation = await _authService.ValidateTokenAsync(secondResult.AccessToken);

        firstTokenValidation.IsSuccess.ShouldBeTrue();
        secondTokenValidation.IsSuccess.ShouldBeTrue();
        firstTokenValidation.Value.AxonUserId.ShouldBe(secondTokenValidation.Value.AxonUserId);
    }

    [Test]
    public async Task CompleteExchangeFlow_RefreshTokenFlow_GeneratesNewAccessToken()
    {
        // Arrange - Complete initial exchange
        var dynamicUserId = "refresh-test-user-003";
        var mockDynamicUserData = CreateMockDynamicUserData(dynamicUserId, withWallets: false);
        var mockClaimsPrincipal = CreateMockClaimsPrincipal(dynamicUserId, "https://app.dynamic.xyz/test-env");

        ConfigureDynamicAuthServiceMocks(mockDynamicUserData, mockClaimsPrincipal);

        var dynamicJwt = CreateMockDynamicJwt(dynamicUserId);
        SetAuthorizationHeader(dynamicJwt);

        using var content = CreateJsonContent("{}");
        var exchangeResponse = await HttpClient.PostAsync("/api/v1/auth/exchange", content);

        exchangeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var exchangeContent = await exchangeResponse.Content.ReadAsStringAsync();
        var exchangeResult = JsonSerializer.Deserialize<ExchangeTokenResponseDto>(exchangeContent, JsonOptions);

        exchangeResult.ShouldNotBeNull();
        var originalRefreshToken = exchangeResult.RefreshToken;

        // Act - Use refresh token to get new access token
        var refreshResult = await _authService.RefreshAccessTokenAsync(originalRefreshToken);

        // Assert
        refreshResult.IsSuccess.ShouldBeTrue();
        refreshResult.Value.AccessToken.ShouldNotBe(exchangeResult.AccessToken); // New access token
        refreshResult.Value.RefreshToken.ShouldNotBe(originalRefreshToken); // New refresh token

        // Verify new tokens work
        var newTokenValidation = await _authService.ValidateTokenAsync(refreshResult.Value.AccessToken);
        newTokenValidation.IsSuccess.ShouldBeTrue();
        newTokenValidation.Value.AxonUserId.Value.ShouldBe(Guid.Parse(exchangeResult.AxonUserId));

        // Old refresh token should be invalidated
        var oldRefreshAttempt = await _authService.RefreshAccessTokenAsync(originalRefreshToken);
        oldRefreshAttempt.IsFailure.ShouldBeTrue();
        oldRefreshAttempt.Error.Type.ShouldBe(ErrorType.Unauthorized);
    }

    [Test]
    public async Task CompleteExchangeFlow_MultipleWalletsAndChains_HandlesCorrectly()
    {
        // Arrange - User with multiple wallets across different chains
        var dynamicUserId = "multi-wallet-user-004";
        var mockDynamicUserData = new DynamicUserData(
            AxonUserId: dynamicUserId,
            Email: "multiuser@example.com",
            EnvironmentId: "test-env-456",
            Wallets: new List<WalletData>
            {
                new(
                    Id: "eth-wallet-1",
                    Address: "0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e41",
                    Chain: "ethereum",
                    WalletName: "MetaMask",
                    Provider: "metamask",
                    ConnectedAtUtc: DateTimeOffset.UtcNow
                ),
                new(
                    Id: "sol-wallet-1",
                    Address: "9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWS",
                    Chain: "solana",
                    WalletName: "Phantom",
                    Provider: "phantom",
                    ConnectedAtUtc: DateTimeOffset.UtcNow
                ),
                new(
                    Id: "pol-wallet-1",
                    Address: "0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e42",
                    Chain: "polygon",
                    WalletName: "MetaMask",
                    Provider: "metamask",
                    ConnectedAtUtc: DateTimeOffset.UtcNow
                )
            },
            FirstVisitUtc: DateTimeOffset.UtcNow,
            LastVisitUtc: DateTimeOffset.UtcNow,
            IsNewUser: true
        );

        var mockClaimsPrincipal = CreateMockClaimsPrincipal(dynamicUserId, "https://app.dynamic.xyz/test-env");
        ConfigureDynamicAuthServiceMocks(mockDynamicUserData, mockClaimsPrincipal);

        var dynamicJwt = CreateMockDynamicJwt(dynamicUserId);
        SetAuthorizationHeader(dynamicJwt);

        // Act
        using var content = CreateJsonContent("{}");
        var response = await HttpClient.PostAsync("/api/v1/auth/exchange", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ExchangeTokenResponseDto>(responseContent, JsonOptions);

        result.ShouldNotBeNull();
        result.Created.ShouldBeTrue();
        result.WalletsProcessed.ShouldBe(3);
        result.WalletsLinked.ShouldBe(3);
        result.DefaultsApplied.ShouldBe(3); // One default per chain
        result.Conflicts.ShouldBe(0);
        result.Skipped.ShouldBe(0);

        // Verify token validity
        var tokenValidation = await _authService.ValidateTokenAsync(result.AccessToken);
        tokenValidation.IsSuccess.ShouldBeTrue();
    }

    [Test]
    public async Task CompleteExchangeFlow_InvalidDynamicJWT_Returns401()
    {
        // Arrange - Configure Dynamic service to reject token
        _mockDynamicAuthService.ValidateTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<DynamicUserData, Error>(
                Error.Unauthorized("Invalid Dynamic JWT", "DYNAMIC.INVALID_TOKEN")));

        var invalidJwt = "invalid.dynamic.jwt";
        SetAuthorizationHeader(invalidJwt);

        // Act
        using var content = CreateJsonContent("{}");
        var response = await HttpClient.PostAsync("/api/v1/auth/exchange", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task CompleteExchangeFlow_WalletOwnershipConflict_Returns409()
    {
        // Arrange - Create first user with wallet
        var firstUserId = "first-user-005";
        var conflictingWalletAddress = "0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e41";

        // First user claims the wallet
        var firstUserData = CreateMockDynamicUserDataWithSpecificWallet(firstUserId, conflictingWalletAddress, "ethereum");
        var firstUserClaims = CreateMockClaimsPrincipal(firstUserId, "https://app.dynamic.xyz/test-env");
        ConfigureDynamicAuthServiceMocks(firstUserData, firstUserClaims);

        var firstJwt = CreateMockDynamicJwt(firstUserId);
        SetAuthorizationHeader(firstJwt);

        using var firstContent = CreateJsonContent("{}");
        var firstResponse = await HttpClient.PostAsync("/api/v1/auth/exchange", firstContent);
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Now second user tries to claim same wallet
        var secondUserId = "second-user-006";
        var secondUserData = CreateMockDynamicUserDataWithSpecificWallet(secondUserId, conflictingWalletAddress, "ethereum");
        var secondUserClaims = CreateMockClaimsPrincipal(secondUserId, "https://app.dynamic.xyz/test-env");
        ConfigureDynamicAuthServiceMocks(secondUserData, secondUserClaims);

        var secondJwt = CreateMockDynamicJwt(secondUserId);
        SetAuthorizationHeader(secondJwt);

        // Act
        using var secondContent = CreateJsonContent("{}");
        var secondResponse = await HttpClient.PostAsync("/api/v1/auth/exchange", secondContent);

        // Assert
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    #endregion

    #region Rate Limiting Tests

    [Test]
    public async Task CompleteExchangeFlow_RateLimitExceeded_Returns429()
    {
        // Arrange
        var dynamicUserId = "rate-limit-user-007";
        var mockDynamicUserData = CreateMockDynamicUserData(dynamicUserId, withWallets: false);
        var mockClaimsPrincipal = CreateMockClaimsPrincipal(dynamicUserId, "https://app.dynamic.xyz/test-env");

        ConfigureDynamicAuthServiceMocks(mockDynamicUserData, mockClaimsPrincipal);

        var dynamicJwt = CreateMockDynamicJwt(dynamicUserId);
        SetAuthorizationHeader(dynamicJwt);

        // Act - Make multiple rapid requests to trigger rate limiting
        var tasks = new List<Task<HttpResponseMessage>>();
        var contentObjects = new List<StringContent>();
        for (int i = 0; i < 10; i++) // Exceed typical rate limit
        {
            var content = CreateJsonContent("{}");
            contentObjects.Add(content);
            tasks.Add(_client.PostAsync("/api/v1/auth/exchange", content));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert - At least some should be rate limited
        var rateLimitedResponses = responses.Where(r => r.StatusCode == HttpStatusCode.TooManyRequests).ToList();
        rateLimitedResponses.Count.ShouldBeGreaterThan(0, "Some requests should be rate limited");

        // Cleanup
        foreach (var response in responses)
        {
            response.Dispose();
        }

        foreach (var content in contentObjects)
        {
            content.Dispose();
        }
    }

    #endregion

    #region Helper Methods

    private async Task VerifyTokenWorksForAuthenticatedRequests(string accessToken)
    {
        // Create a new client with the access token
        using var authenticatedClient = Factory.CreateClient();
        authenticatedClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        // Try to access a protected endpoint (assuming there's an endpoint that requires authentication)
        var response = await authenticatedClient.GetAsync("/api/v1/auth/me");

        // Should not be 401 Unauthorized (exact response depends on endpoint implementation)
        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }

    private void ConfigureDynamicAuthServiceMocks(DynamicUserData userData, ClaimsPrincipal claimsPrincipal)
    {
        _mockDynamicAuthService.ValidateTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<DynamicUserData, Error>(userData));

        _mockDynamicAuthService.GetRawClaimsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<ClaimsPrincipal, Error>(claimsPrincipal));
    }

    private static DynamicUserData CreateMockDynamicUserData(string userId, bool withWallets = false)
    {
        var wallets = withWallets ? new List<WalletData>
        {
            new(
                Id: "eth-wallet",
                Address: "0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e41",
                Chain: "ethereum",
                WalletName: "MetaMask",
                Provider: "metamask",
                ConnectedAtUtc: DateTimeOffset.UtcNow
            ),
            new(
                Id: "sol-wallet",
                Address: "9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWS",
                Chain: "solana",
                WalletName: "Phantom",
                Provider: "phantom",
                ConnectedAtUtc: DateTimeOffset.UtcNow
            )
        } : new List<WalletData>();

        return new DynamicUserData(
            AxonUserId: userId,
            Email: $"{userId}@example.com",
            EnvironmentId: "test-env-123",
            Wallets: wallets,
            FirstVisitUtc: DateTimeOffset.UtcNow,
            LastVisitUtc: DateTimeOffset.UtcNow,
            IsNewUser: true
        );
    }

    private static DynamicUserData CreateMockDynamicUserDataWithSpecificWallet(string userId, string walletAddress, string chain)
    {
        return new DynamicUserData(
            AxonUserId: userId,
            Email: $"{userId}@example.com",
            EnvironmentId: "test-env-123",
            Wallets: new List<WalletData>
            {
                new(
                    Id: $"{chain}-wallet",
                    Address: walletAddress,
                    Chain: chain,
                    WalletName: "TestWallet",
                    Provider: "test",
                    ConnectedAtUtc: DateTimeOffset.UtcNow
                )
            },
            FirstVisitUtc: DateTimeOffset.UtcNow,
            LastVisitUtc: DateTimeOffset.UtcNow,
            IsNewUser: true
        );
    }

    private static ClaimsPrincipal CreateMockClaimsPrincipal(string userId, string issuer)
    {
        var claims = new[]
        {
            new Claim("sub", userId),
            new Claim("iss", issuer),
            new Claim("aud", "test-audience"),
            new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
            new Claim("exp", DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString())
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Dynamic"));
    }

    private static string CreateMockDynamicJwt(string userId)
    {
        // In a real scenario, this would be a valid JWT from Dynamic
        // For testing purposes, we return a mock token that will be handled by our mock service
        return $"dynamic.jwt.{userId}";
    }

    private async Task CleanDatabase()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Microsoft.EntityFrameworkCore.DbContext>();

        // Clean in dependency order
        await dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE identity.wallet_ownerships CASCADE");
        await dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE identity.principal_chain_defaults CASCADE");
        await dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE identity.credentials CASCADE");
        await dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE identity.wallets CASCADE");
        await dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE identity.axon_principals CASCADE");
    }

    private sealed record ExchangeTokenResponseDto(
        string AccessToken,
        string RefreshToken,
        string TokenType,
        int ExpiresIn,
        string AxonUserId,
        bool Created,
        int WalletsProcessed,
        int WalletsLinked,
        int DefaultsApplied,
        int Skipped,
        int Conflicts,
        DateTimeOffset IssuedAt,
        DateTimeOffset AccessTokenExpiresAt,
        DateTimeOffset RefreshTokenExpiresAt
    );

    #endregion
}