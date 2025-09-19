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

namespace Axon.Api.Tests.Endpoints.V1.Auth;

[TestFixture]
public class ExchangeTokenEndpointTests
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private ICurrentUserService _mockCurrentUserService = null!;

    [SetUp]
    public void Setup()
    {
        _mockCurrentUserService = Substitute.For<ICurrentUserService>();
        
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace authentication with test authentication
                    services.AddAuthentication("Test")
                        .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", options => { });
                    
                    // Replace ICurrentUserService with mock
                    services.Remove(services.Single(d => d.ServiceType == typeof(ICurrentUserService)));
                    services.AddSingleton(_mockCurrentUserService);
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
    public async Task HandleAsync_WhenAuthenticated_Returns200WithUserData()
    {
        // Arrange
        _mockCurrentUserService.AxonUserId.Returns("test-user-id");

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
        exchangeResponse.AxonUserId.ShouldBe("test-user-id");
        exchangeResponse.Email.ShouldBe("test@example.com");
        exchangeResponse.Wallets.ShouldHaveSingleItem();
        exchangeResponse.Wallets[0].Address.ShouldBe("Sol1234567890");
        exchangeResponse.Wallets[0].Chain.ShouldBe("solana");
        exchangeResponse.Wallets[0].Provider.ShouldBe("phantom");
    }

    [Test]
    public async Task HandleAsync_WhenAxonUserIdMissing_Returns500()
    {
        // Arrange
        _mockCurrentUserService.AxonUserId.Returns((string?)null);

        // Act
        var response = await _client.PostAsync("/api/v1/auth/exchange", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    private record ExchangeTokenResponse(
        string AxonUserId,
        string Email,
        List<WalletInfo> Wallets
    );

    private record WalletInfo(
        string Id,
        string Address,
        string Chain,
        string Provider,
        string? WalletName,
        DateTime? ConnectedAt
    );
}

/// <summary>
/// Test authentication handler that always succeeds for testing
/// </summary>
public class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
            new Claim(ClaimTypes.Email, "test@example.com"),
            new Claim("environment_id", "test-env"),
            new Claim("wallet:solana", "Sol1234567890"),
            new Claim("wallet:provider:solana", "phantom")
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}