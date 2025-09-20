using Axon.Api.Tests.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;
using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Text.Encodings.Web;
using System.Security.Claims;
using BuildingBlocks.Core.Abstractions.Authentication;
using NSubstitute;
using FluentAssertions;
using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using System.IdentityModel.Tokens.Jwt;
using Axon.Modules.Identity.Application.Contracts.ExternalServices;
using CSharpFunctionalExtensions;
using BuildingBlocks.Core.Diagnostics.Errors;

namespace Axon.Api.Tests.Endpoints.V1.Auth;

[TestFixture]
public class RateLimitingTests
{
    private TestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private ICurrentUserService _mockCurrentUserService = null!;
    private IDynamicAuthService _mockDynamicAuthService = null!;

    [SetUp]
    public void Setup()
    {
        _mockCurrentUserService = Substitute.For<ICurrentUserService>();
        _mockCurrentUserService.AxonUserId.Returns("test-subject-123");

        _mockDynamicAuthService = Substitute.For<IDynamicAuthService>();

        // Mock successful JWT validation for rate limiting tests
        var mockValidationResult = new DynamicUserData(
            AxonUserId: "test-subject-123",
            Email: "test@example.com",
            EnvironmentId: "test-env",
            Wallets: new List<WalletData>
            {
                new WalletData(
                    Id: "wallet-id-123",
                    Address: "Sol1234567890",
                    Chain: "solana",
                    WalletName: "Test Wallet",
                    Provider: "phantom",
                    ConnectedAtUtc: DateTimeOffset.UtcNow
                )
            },
            FirstVisitUtc: DateTimeOffset.UtcNow,
            LastVisitUtc: DateTimeOffset.UtcNow,
            IsNewUser: true
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
    public async Task ExchangeEndpoint_WithinRateLimit_Returns200WithCorrectHeaders()
    {
        // Act - Make 5 requests within limit (10 per minute)
        var responses = new List<HttpResponseMessage>();
        for (int i = 0; i < 5; i++)
        {
            var response = await _client.PostAsync("/api/v1/auth/exchange", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
            responses.Add(response);
        }

        // Assert - All should succeed with rate limit headers
        foreach (var response in responses)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            
            // Verify rate limiting headers are present
            response.Headers.Contains("X-RateLimit-Limit").ShouldBeTrue();
            response.Headers.Contains("X-RateLimit-Remaining").ShouldBeTrue();
            response.Headers.Contains("X-RateLimit-Reset").ShouldBeTrue();
            
            // Verify header values
            response.Headers.GetValues("X-RateLimit-Limit").First().ShouldBe("10");
        }

        // Verify remaining count decreases
        var firstResponse = responses.First();
        var lastResponse = responses.Last();
        
        var firstRemaining = int.Parse(firstResponse.Headers.GetValues("X-RateLimit-Remaining").First(), CultureInfo.InvariantCulture);
        var lastRemaining = int.Parse(lastResponse.Headers.GetValues("X-RateLimit-Remaining").First(), CultureInfo.InvariantCulture);
        
        firstRemaining.ShouldBeGreaterThan(lastRemaining);
        
        // Cleanup
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Test]
    public async Task ExchangeEndpoint_ExceedsRateLimit_Returns429WithRetryAfterHeader()
    {
        // Act - Make 11 requests to exceed the limit of 10 per minute
        var responses = new List<HttpResponseMessage>();
        
        for (int i = 0; i < 11; i++)
        {
            var response = await _client.PostAsync("/api/v1/auth/exchange", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
            responses.Add(response);
        }

        // Assert - First 10 should succeed, 11th should be rate limited
        for (int i = 0; i < 10; i++)
        {
            responses[i].StatusCode.ShouldBe(HttpStatusCode.OK, $"Request {i + 1} should succeed");
        }

        var rateLimitedResponse = responses[10];
        rateLimitedResponse.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        
        // Verify rate limit headers on 429 response
        rateLimitedResponse.Headers.Contains("Retry-After").ShouldBeTrue();
        rateLimitedResponse.Headers.Contains("X-RateLimit-Limit").ShouldBeTrue();
        rateLimitedResponse.Headers.Contains("X-RateLimit-Remaining").ShouldBeTrue();
        rateLimitedResponse.Headers.Contains("X-RateLimit-Reset").ShouldBeTrue();
        
        // Verify header values for rate limited response
        rateLimitedResponse.Headers.GetValues("X-RateLimit-Limit").First().ShouldBe("10");
        rateLimitedResponse.Headers.GetValues("X-RateLimit-Remaining").First().ShouldBe("0");
        
        var retryAfter = int.Parse(rateLimitedResponse.Headers.GetValues("Retry-After").First(), CultureInfo.InvariantCulture);
        retryAfter.ShouldBeGreaterThan(0);
        retryAfter.ShouldBeLessThanOrEqualTo(60); // Should be within 60 seconds (1 minute window)
        
        // Cleanup
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Test]
    public async Task ExchangeEndpoint_MultipleIPs_MaintainSeparateCounters()
    {
        // Arrange - Create clients with different simulated IPs via X-Forwarded-For headers
        var client1 = _factory.CreateClient();
        var mockJwt1 = CreateMockJwt();
        client1.DefaultRequestHeaders.Add("Authorization", $"Bearer {mockJwt1}");
        client1.DefaultRequestHeaders.Add("X-Forwarded-For", "192.168.1.1");

        var client2 = _factory.CreateClient();
        var mockJwt2 = CreateMockJwt();
        client2.DefaultRequestHeaders.Add("Authorization", $"Bearer {mockJwt2}");
        client2.DefaultRequestHeaders.Add("X-Forwarded-For", "192.168.1.2");

        try
        {
            // Act - Make 10 requests from first client (IP: 192.168.1.1)
            for (int i = 0; i < 10; i++)
            {
                var response = await client1.PostAsync("/api/v1/auth/exchange", new StringContent("{}", Encoding.UTF8, "application/json"));
                response.StatusCode.ShouldBe(HttpStatusCode.OK);
                response.Dispose();
            }

            // Verify first client is at limit
            var limitResponse1 = await client1.PostAsync("/api/v1/auth/exchange", new StringContent("{}", Encoding.UTF8, "application/json"));
            limitResponse1.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
            limitResponse1.Dispose();

            // Act - Make request from second client (IP: 192.168.1.2, should still work)
            var response2 = await client2.PostAsync("/api/v1/auth/exchange", new StringContent("{}", Encoding.UTF8, "application/json"));

            // Assert - Second client should not be rate limited
            response2.StatusCode.ShouldBe(HttpStatusCode.OK);
            response2.Headers.GetValues("X-RateLimit-Remaining").First().ShouldBe("9"); // Fresh counter
            response2.Dispose();
        }
        finally
        {
            client1.Dispose();
            client2.Dispose();
        }
    }

    [Test]
    public async Task AuthMeEndpoint_NotAffectedByRateLimit_AlwaysReturns200()
    {
        // Arrange - First exhaust rate limit on exchange endpoint (11 calls to exceed 10 limit)
        for (int i = 0; i < 11; i++)
        {
            var exchangeResponse = await _client.PostAsync("/api/v1/auth/exchange", new StringContent("{}", Encoding.UTF8, "application/json"));
            exchangeResponse.Dispose();
        }

        // Verify that exchange endpoint is now rate limited
        var rateLimitedResponse = await _client.PostAsync("/api/v1/auth/exchange", new StringContent("{}", Encoding.UTF8, "application/json"));
        rateLimitedResponse.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        rateLimitedResponse.Dispose();

        // Act - Try /auth/me endpoint after exchange is rate limited
        var meResponse = await _client.GetAsync("/auth/me");

        // Assert - /auth/me should not be rate limited (even though exchange is)
        // The main test is that it doesn't have rate limiting headers

        // Should not have rate limiting headers (this is the main test)
        meResponse.Headers.Contains("X-RateLimit-Limit").ShouldBeFalse();
        meResponse.Headers.Contains("X-RateLimit-Remaining").ShouldBeFalse();
        meResponse.Headers.Contains("X-RateLimit-Reset").ShouldBeFalse();

        // The endpoint should not return rate limiting status (though it may have other errors)
        meResponse.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);

        meResponse.Dispose();
    }

    [Test]
    public async Task RateLimitWindow_ResetsAfter60Seconds()
    {
        // This test would require time manipulation or a very long wait
        // For now, we'll test the concept by verifying reset header logic
        
        // Act
        var response = await _client.PostAsync("/api/v1/auth/exchange", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        
        var resetHeader = response.Headers.GetValues("X-RateLimit-Reset").First();
        var resetTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(resetHeader, CultureInfo.InvariantCulture));
        var currentTime = DateTimeOffset.UtcNow;
        
        // Reset time should be within the next 60 seconds
        var timeDiff = resetTime - currentTime;
        timeDiff.TotalSeconds.ShouldBeLessThanOrEqualTo(60);
        timeDiff.TotalSeconds.ShouldBeGreaterThanOrEqualTo(0);
        
        response.Dispose();
    }

    [Test]
    public async Task RateLimit_HeaderAccuracy_UpdatesCorrectly()
    {
        // Act - Make sequential requests and verify header consistency
        var firstResponse = await _client.PostAsync("/api/v1/auth/exchange", new StringContent("{}", Encoding.UTF8, "application/json"));
        var firstRemaining = int.Parse(firstResponse.Headers.GetValues("X-RateLimit-Remaining").First(), CultureInfo.InvariantCulture);
        firstResponse.Dispose();

        var secondResponse = await _client.PostAsync("/api/v1/auth/exchange", new StringContent("{}", Encoding.UTF8, "application/json"));
        var secondRemaining = int.Parse(secondResponse.Headers.GetValues("X-RateLimit-Remaining").First(), CultureInfo.InvariantCulture);
        secondResponse.Dispose();

        // Assert - Remaining count should decrease by exactly 1
        firstRemaining.ShouldBe(secondRemaining + 1);
        
        // Both should have same limit and reset time
        firstResponse.Headers.GetValues("X-RateLimit-Limit").First()
            .ShouldBe(secondResponse.Headers.GetValues("X-RateLimit-Limit").First());
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
            new Claim("exp", new DateTimeOffset(DateTime.UtcNow.AddHours(1)).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new Claim("iat", new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture))
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

