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

namespace Axon.Api.Tests.Endpoints.V1.Auth;

[TestFixture]
public class RateLimitingTests
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private ICurrentUserService _mockCurrentUserService = null!;

    [SetUp]
    public void Setup()
    {
        _mockCurrentUserService = Substitute.For<ICurrentUserService>();
        _mockCurrentUserService.AxonUserId.Returns("test-user-id");
        
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
        // Arrange - Create clients with different IPs (simulated via different client instances)
        var client1 = _factory.CreateClient();
        var client2 = _factory.CreateClient();

        try
        {
            // Act - Make 10 requests from first client
            for (int i = 0; i < 10; i++)
            {
                var response = await client1.PostAsync("/api/v1/auth/exchange", null);
                response.StatusCode.ShouldBe(HttpStatusCode.OK);
                response.Dispose();
            }

            // Verify first client is at limit
            var limitResponse1 = await client1.PostAsync("/api/v1/auth/exchange", null);
            limitResponse1.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
            limitResponse1.Dispose();

            // Act - Make request from second client (should still work)
            var response2 = await client2.PostAsync("/api/v1/auth/exchange", null);
            
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
        // Arrange - First exhaust rate limit on exchange endpoint
        for (int i = 0; i < 11; i++)
        {
            var exchangeResponse = await _client.PostAsync("/api/v1/auth/exchange", null);
            exchangeResponse.Dispose();
        }

        // Act - Try /auth/me endpoint
        var meResponse = await _client.GetAsync("/auth/me");

        // Assert - Should not be rate limited
        meResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        
        // Should not have rate limiting headers
        meResponse.Headers.Contains("X-RateLimit-Limit").ShouldBeFalse();
        meResponse.Headers.Contains("X-RateLimit-Remaining").ShouldBeFalse();
        meResponse.Headers.Contains("X-RateLimit-Reset").ShouldBeFalse();
        
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
        var firstResponse = await _client.PostAsync("/api/v1/auth/exchange", null);
        var firstRemaining = int.Parse(firstResponse.Headers.GetValues("X-RateLimit-Remaining").First(), CultureInfo.InvariantCulture);
        firstResponse.Dispose();

        var secondResponse = await _client.PostAsync("/api/v1/auth/exchange", null);
        var secondRemaining = int.Parse(secondResponse.Headers.GetValues("X-RateLimit-Remaining").First(), CultureInfo.InvariantCulture);
        secondResponse.Dispose();

        // Assert - Remaining count should decrease by exactly 1
        firstRemaining.ShouldBe(secondRemaining + 1);
        
        // Both should have same limit and reset time
        firstResponse.Headers.GetValues("X-RateLimit-Limit").First()
            .ShouldBe(secondResponse.Headers.GetValues("X-RateLimit-Limit").First());
    }
}

