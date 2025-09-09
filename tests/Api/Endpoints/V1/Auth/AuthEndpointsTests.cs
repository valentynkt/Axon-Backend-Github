using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace Axon.Api.Tests.Endpoints.V1.Auth;

[TestFixture]
public class AuthEndpointsTests
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services =>
                {
                    // Override Dynamic config for testing
                    services.Configure<Microsoft.Extensions.Configuration.IConfiguration>(config =>
                    {
                        config["Dynamic:JwksUri"] = "https://test.dynamic.xyz/.well-known/jwks";
                        config["Dynamic:Issuer"] = "https://test.dynamic.xyz";
                        config["Dynamic:Audience"] = "test-audience";
                    });
                });
            });
        
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    public async Task ExchangeEndpoint_WithoutAuthHeader_Returns400()
    {
        // Act
        var response = await _client.PostAsync("/auth/exchange", new StringContent("", Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task ExchangeEndpoint_WithInvalidBearerToken_Returns400()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");

        // Act
        var response = await _client.PostAsync("/auth/exchange", new StringContent("", Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task ExchangeEndpoint_WithValidMockJwt_Returns401()
    {
        // Arrange - Create a mock JWT (won't be signature validated in test)
        var mockJwt = CreateMockJwt();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mockJwt);

        // Act
        var response = await _client.PostAsync("/auth/exchange", new StringContent("{}", Encoding.UTF8, "application/json"));

        // Assert - Should return 401 due to signature validation failure, which is expected
        // The new architecture should properly validate JWT signatures
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task MeEndpoint_WithoutAuthHeader_Returns401()
    {
        // Act
        var response = await _client.GetAsync("/auth/me");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task MeEndpoint_WithInvalidToken_Returns401()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");

        // Act
        var response = await _client.GetAsync("/auth/me");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task RateLimiting_ExceedsLimit_Returns429()
    {
        // Arrange - Make multiple requests quickly to trigger rate limit
        var tasks = new List<Task<HttpResponseMessage>>();

        // Act - Send 12 requests (exceeds 10 req/min limit for /auth/exchange)
        for (int i = 0; i < 12; i++)
        {
            var task = _client.PostAsync("/auth/exchange", new StringContent("", Encoding.UTF8, "application/json"));
            tasks.Add(task);
        }

        var responses = await Task.WhenAll(tasks);

        // Assert - At least one should be rate limited
        var rateLimitedResponses = responses.Where(r => r.StatusCode == HttpStatusCode.TooManyRequests).ToArray();
        rateLimitedResponses.Length.ShouldBeGreaterThan(0);

        // Check rate limit headers on 429 response
        var rateLimitedResponse = rateLimitedResponses.First();
        rateLimitedResponse.Headers.ShouldContain(h => h.Key == "X-RateLimit-Limit");
        rateLimitedResponse.Headers.ShouldContain(h => h.Key == "X-RateLimit-Remaining");
    }

    [Test]
    public async Task SecurityHeaders_ArePresent()
    {
        // Act
        var response = await _client.GetAsync("/");

        // Assert
        response.Headers.ShouldContain(h => h.Key == "X-Content-Type-Options");
        response.Headers.ShouldContain(h => h.Key == "X-Frame-Options");
        response.Headers.ShouldContain(h => h.Key == "X-XSS-Protection");
        response.Headers.ShouldContain(h => h.Key == "Referrer-Policy");
        response.Headers.ShouldContain(h => h.Key == "X-Correlation-ID");
    }

    private static string CreateMockJwt()
    {
        // Create a mock JWT for testing (signature won't be valid)
        var handler = new JwtSecurityTokenHandler();
        
        var claims = new[]
        {
            new Claim("sub", "test-subject-123"),
            new Claim("iss", "https://test.dynamic.xyz"),
            new Claim("aud", "test-audience"),
            new Claim("exp", new DateTimeOffset(DateTime.UtcNow.AddHours(1)).ToUnixTimeSeconds().ToString()),
            new Claim("iat", new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds().ToString())
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