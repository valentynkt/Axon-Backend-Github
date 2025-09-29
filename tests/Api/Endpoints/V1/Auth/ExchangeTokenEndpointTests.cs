using Axon.Api.Tests.Common;
using Axon.Modules.Identity.Application.Contracts.ExternalServices;
using Axon.Modules.Identity.Application.DTOs.Exchange;
using Axon.Modules.Identity.Infrastructure.Services;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using BuildingBlocks.Core.Abstractions.Authentication;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using NSubstitute;
using NUnit.Framework;
using Shouldly;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Globalization;

namespace Axon.Api.Tests.Endpoints.V1.Auth;

[TestFixture]
public class ExchangeTokenEndpointTests
{
    private TestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private ICurrentUserService _mockCurrentUserService = null!;
    private IDynamicAuthService _mockDynamicAuthService = null!;

    [SetUp]
    public void Setup()
    {
        _mockCurrentUserService = Substitute.For<ICurrentUserService>();
        _mockDynamicAuthService = Substitute.For<IDynamicAuthService>();

        // Mock DynamicAuthService to return successful validation
        var mockValidationResult = new DynamicUserData(
            AxonUserId: "test-user-id",
            Email: "test@example.com",
            EnvironmentId: "test-env",
            Wallets: new List<WalletData>
            {
                new(
                    Id: "test-wallet-id",
                    Address: "Sol1234567890",
                    Chain: "solana",
                    WalletName: "Test Wallet",
                    Provider: "phantom",
                    ConnectedAtUtc: DateTimeOffset.UtcNow
                )
            },
            FirstVisitUtc: DateTimeOffset.UtcNow,
            LastVisitUtc: DateTimeOffset.UtcNow,
            IsNewUser: false
        );

        _mockDynamicAuthService.ValidateTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<DynamicUserData, Error>(mockValidationResult));

        // Mock GetRawClaimsAsync to return claims principal with issuer
        var mockClaims = new[]
        {
            new Claim("sub", "test-subject-123"),
            new Claim("iss", "https://test.dynamic.xyz"),
            new Claim("aud", "test-audience")
        };
        var mockIdentity = new ClaimsIdentity(mockClaims, "Test");
        var mockPrincipal = new ClaimsPrincipal(mockIdentity);

        _mockDynamicAuthService.GetRawClaimsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<ClaimsPrincipal, Error>(mockPrincipal));

        _factory = new TestWebApplicationFactory()
            .WithServices(services =>
            {
                // Replace authentication with test authentication
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", options => { });

                // Replace ICurrentUserService with mock
                services.Remove(services.Single(d => d.ServiceType == typeof(ICurrentUserService)));
                services.AddSingleton(_mockCurrentUserService);

                // Replace IDynamicAuthService with mock
                services.Remove(services.Single(d => d.ServiceType == typeof(IDynamicAuthService)));
                services.AddSingleton(_mockDynamicAuthService);
            });

        _client = _factory.CreateClient();

        // Add mock Authorization header for all requests
        var mockJwt = CreateMockJwt();
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {mockJwt}");
    }

    [TearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    public async Task HandleAsync_WhenAuthenticated_Returns200WithUserData()
    {
        // Arrange
        _mockCurrentUserService.AxonUserId.Returns("test-user-id");

        // Act
        var response = await _client.PostAsync("/api/v1/auth/exchange", new StringContent("{}", Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var exchangeResponse = JsonSerializer.Deserialize<ExchangeTokenResponse>(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        exchangeResponse.ShouldNotBeNull();
        exchangeResponse.AxonUserId.ShouldNotBeNullOrEmpty();
        Guid.TryParse(exchangeResponse.AxonUserId, out _).ShouldBeTrue("AxonUserId should be a valid GUID");
        exchangeResponse.Created.ShouldBeTrue();
        exchangeResponse.WalletsProcessed.ShouldBe(1);
        exchangeResponse.WalletsLinked.ShouldBe(1);
        exchangeResponse.DefaultsApplied.ShouldBe(1);
        exchangeResponse.Skipped.ShouldBe(0);
        exchangeResponse.Conflicts.ShouldBe(0);
    }

    [Test]
    public async Task HandleAsync_WhenJwtValidationFails_Returns401()
    {
        // Arrange - Add invalid JWT token and mock validation to fail
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid-jwt-token");
        _mockDynamicAuthService.ValidateTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<DynamicUserData, Error>(Error.Validation("Invalid JWT token")));

        // Act
        var response = await _client.PostAsync("/api/v1/auth/exchange", new StringContent("{}", Encoding.UTF8, "application/json"));

        // Assert - JWT validation failure returns 401 Unauthorized
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task HandleAsync_WhenMissingAuthorizationHeader_Returns401()
    {
        // Arrange - Remove authorization header
        _client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _client.PostAsync("/api/v1/auth/exchange", new StringContent("{}", Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task HandleAsync_WhenInvalidAuthorizationHeader_Returns401()
    {
        // Arrange - Set invalid authorization header format
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", "invalid-format");

        // Act
        var response = await _client.PostAsync("/api/v1/auth/exchange", new StringContent("{}", Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task HandleAsync_WhenDynamicServiceUnavailable_Returns500()
    {
        // Arrange - Mock Dynamic service to return internal error
        _mockDynamicAuthService.ValidateTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<DynamicUserData, Error>(Error.Internal("Dynamic service unavailable", "DYNAMIC.SERVICE_ERROR")));

        // Act
        var response = await _client.PostAsync("/api/v1/auth/exchange", new StringContent("{}", Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Test]
    public async Task HandleAsync_WhenWalletConflict_Returns409()
    {
        // Arrange - Mock scenario where wallet is owned by another principal
        var conflictError = Error.Conflict("Wallet is already owned by another principal", "WALLET.OWNERSHIP_CONFLICT");
        _mockDynamicAuthService.ValidateTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<DynamicUserData, Error>(conflictError));

        // Act
        var response = await _client.PostAsync("/api/v1/auth/exchange", new StringContent("{}", Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task HandleAsync_WhenValidRequest_SetsNoCacheHeaders()
    {
        // Arrange
        _mockCurrentUserService.AxonUserId.Returns("test-user-id");

        // Act
        var response = await _client.PostAsync("/api/v1/auth/exchange", new StringContent("{}", Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl?.NoStore.ShouldBeTrue();
        response.Headers.Pragma?.FirstOrDefault()?.Name.ShouldBe("no-cache");
    }

    [Test]
    public async Task HandleAsync_WhenMultipleWallets_ProcessesAllWallets()
    {
        // Arrange - Mock validation result with multiple wallets
        var multiWalletResult = new DynamicUserData(
            AxonUserId: "test-user-id",
            Email: "test@example.com",
            EnvironmentId: "test-env",
            Wallets: new List<WalletData>
            {
                new(
                    Id: "wallet-1",
                    Address: "0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e41",
                    Chain: "ethereum",
                    WalletName: "MetaMask",
                    Provider: "metamask",
                    ConnectedAtUtc: DateTimeOffset.UtcNow
                ),
                new(
                    Id: "wallet-2",
                    Address: "9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWS",
                    Chain: "solana",
                    WalletName: "Phantom",
                    Provider: "phantom",
                    ConnectedAtUtc: DateTimeOffset.UtcNow
                )
            },
            FirstVisitUtc: DateTimeOffset.UtcNow,
            LastVisitUtc: DateTimeOffset.UtcNow,
            IsNewUser: false
        );

        _mockDynamicAuthService.ValidateTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<DynamicUserData, Error>(multiWalletResult));

        _mockCurrentUserService.AxonUserId.Returns("test-user-id");

        // Act
        var response = await _client.PostAsync("/api/v1/auth/exchange", new StringContent("{}", Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var exchangeResponse = JsonSerializer.Deserialize<ExchangeTokenResponse>(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        exchangeResponse.ShouldNotBeNull();
        exchangeResponse.WalletsProcessed.ShouldBe(2);
        exchangeResponse.WalletsLinked.ShouldBe(2);
        exchangeResponse.DefaultsApplied.ShouldBe(2); // One default per chain
    }

    [Test]
    public async Task HandleAsync_WhenTokenGenerationFails_Returns500()
    {
        // Arrange - Mock GetRawClaimsAsync to fail
        _mockDynamicAuthService.GetRawClaimsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<ClaimsPrincipal, Error>(Error.Internal("Token generation failed", "TOKEN.GENERATION_ERROR")));

        _mockCurrentUserService.AxonUserId.Returns("test-user-id");

        // Act
        var response = await _client.PostAsync("/api/v1/auth/exchange", new StringContent("{}", Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    private record ExchangeTokenResponse(
        string AxonUserId,
        bool Created,
        int WalletsProcessed,
        int WalletsLinked,
        int DefaultsApplied,
        int Skipped,
        int Conflicts
    );

    private static string CreateMockJwt()
    {
        // Create a mock JWT for testing (signature won't be valid)
        var handler = new JwtSecurityTokenHandler();

        var claims = new[]
        {
            new Claim("sub", "test-subject-123"),
            new Claim("iss", "https://test.dynamic.xyz"),
            new Claim("aud", "test-audience"),
            new Claim("exp", new DateTimeOffset(DateTime.UtcNow.AddHours(1)).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
            new Claim("iat", new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture))
        };

        var token = new JwtSecurityToken(
            issuer: "https://test.dynamic.xyz",
            audience: "test-audience",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: null // No signing for mock
        );

        // Return just the header.payload part (no signature)
        var tokenString = handler.WriteToken(token);
        var parts = tokenString.Split('.');
        return $"{parts[0]}.{parts[1]}.mock-signature";
    }
}

