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
    public async Task HandleAsync_WhenJwtValidationFails_Returns400()
    {
        // Arrange - Mock JWT validation to fail
        _mockDynamicAuthService.ValidateTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<DynamicUserData, Error>(Error.Validation("Invalid JWT token")));

        // Act
        var response = await _client.PostAsync("/api/v1/auth/exchange", new StringContent("{}", Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
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

