using Axon.Api.Services.ExternalServices;
using Axon.Api.Services.ExternalServices.Models;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;

namespace Axon.Api.Tests.Endpoints.V1.Auth;

[TestFixture]
public class ExchangeTokenEndpointTests
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private DynamicAuthService _mockDynamicAuthService = null!;

    [SetUp]
    public void Setup()
    {
        _mockDynamicAuthService = Substitute.For<DynamicAuthService>(Substitute.For<HttpClient>());
        
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace DynamicAuthService with mock
                    services.Remove(services.Single(d => d.ServiceType == typeof(DynamicAuthService)));
                    services.AddSingleton(_mockDynamicAuthService);
                });
            });

        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    public async Task HandleAsync_WhenNoAuthorizationHeader_Returns401()
    {
        // Act
        var response = await _client.PostAsync("/api/v1/auth/exchange", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task HandleAsync_WhenEmptyAuthorizationHeader_Returns401()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "");

        // Act
        var response = await _client.PostAsync("/api/v1/auth/exchange", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task HandleAsync_WhenInvalidToken_Returns401()
    {
        // Arrange
        const string invalidToken = "invalid-jwt-token";
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", invalidToken);

        _mockDynamicAuthService.ValidateTokenAsync(invalidToken)
            .Returns(Result.Failure<DynamicUser>("Invalid token"));

        // Act
        var response = await _client.PostAsync("/api/v1/auth/exchange", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task HandleAsync_WhenDynamicServiceUnavailable_Returns503()
    {
        // Arrange
        const string validToken = "valid-jwt-token";
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", validToken);

        _mockDynamicAuthService.ValidateTokenAsync(validToken)
            .Returns(Result.Failure<DynamicUser>("Dynamic.xyz unavailable"));

        // Act
        var response = await _client.PostAsync("/api/v1/auth/exchange", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    [Test]
    public async Task HandleAsync_WhenValidToken_Returns200WithUserData()
    {
        // Arrange
        const string validToken = "valid-jwt-token";
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", validToken);

        var userId = Guid.NewGuid();
        var walletId = Guid.NewGuid();
        var dynamicUser = new DynamicUser
        {
            Id = userId,
            Email = "test@example.com",
            DisplayName = "Test User",
            Wallets = new List<DynamicWallet>
            {
                new()
                {
                    Id = walletId,
                    Address = "Sol1234567890",
                    Chain = "solana",
                    Provider = "phantom",
                    WalletName = "My Wallet",
                    ConnectedAt = DateTime.UtcNow
                }
            }
        };

        _mockDynamicAuthService.ValidateTokenAsync(validToken)
            .Returns(Result.Success(dynamicUser));

        // Act
        var response = await _client.PostAsync("/api/v1/auth/exchange", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var exchangeResponse = JsonSerializer.Deserialize<ExchangeTokenResponse>(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        exchangeResponse.ShouldNotBeNull();
        exchangeResponse.UserId.ShouldBe(userId.ToString());
        exchangeResponse.Email.ShouldBe("test@example.com");
        exchangeResponse.Wallets.ShouldHaveSingleItem();
        exchangeResponse.Wallets[0].Address.ShouldBe("Sol1234567890");
        exchangeResponse.Wallets[0].Chain.ShouldBe("solana");
    }

    private record ExchangeTokenResponse(
        string UserId,
        string Email,
        List<WalletInfo> Wallets
    );

    private record WalletInfo(
        Guid Id,
        string Address,
        string Chain,
        string Provider,
        string? WalletName,
        DateTime? ConnectedAt
    );
}