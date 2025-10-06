using System.Globalization;
using System.Net;
using System.Text;
using Axon.Modules.Identity.E2E.Infrastructure;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.E2E;

/// <summary>
/// E2E tests for rate limiting functionality on authentication endpoints.
/// Tests rate limit enforcement, headers, IP-based counters, and time windows.
/// </summary>
[TestFixture]
[NonParallelizable] // CRITICAL: Prevent parallel execution due to shared rate limit state
public class RateLimitingE2ETests : E2ETestBase
{
    [Test]
    public async Task ExchangeEndpoint_WithinRateLimit_Returns200WithCorrectHeaders()
    {
        // Arrange: Create valid JWT
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt();

        // Act - Make 5 requests within limit (10 per minute)
        var responses = new List<HttpResponseMessage>();
        for (int i = 0; i < 5; i++)
        {
            SetAuthorizationHeader(validJwt);
            using var content = CreateJsonContent("{}");
            var response = await HttpClient.PostAsync("/api/v1/auth/exchange", content);
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
        // Arrange: Create valid JWT
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt("rate_limit_test_user");

        // Act - Make 11 requests to exceed the limit of 10 per minute
        var responses = new List<HttpResponseMessage>();

        for (int i = 0; i < 11; i++)
        {
            SetAuthorizationHeader(validJwt);
            using var content = CreateJsonContent("{}");
            var response = await HttpClient.PostAsync("/api/v1/auth/exchange", content);
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
        var client1 = Factory.CreateClient();
        var validJwt1 = JwtTestTokenFactory.CreateValidDynamicJwt("ip_test_user_1");
        client1.DefaultRequestHeaders.Add("Authorization", $"Bearer {validJwt1}");
        client1.DefaultRequestHeaders.Add("X-Forwarded-For", "192.168.1.1");

        var client2 = Factory.CreateClient();
        var validJwt2 = JwtTestTokenFactory.CreateValidDynamicJwt("ip_test_user_2");
        client2.DefaultRequestHeaders.Add("Authorization", $"Bearer {validJwt2}");
        client2.DefaultRequestHeaders.Add("X-Forwarded-For", "192.168.1.2");

        try
        {
            // Act - Make 10 requests from first client (IP: 192.168.1.1)
            for (int i = 0; i < 10; i++)
            {
                using var content = new StringContent("{}", Encoding.UTF8, "application/json");
                var response = await client1.PostAsync("/api/v1/auth/exchange", content);
                response.StatusCode.ShouldBe(HttpStatusCode.OK);
                response.Dispose();
            }

            // Verify first client is at limit
            using (var limitContent = new StringContent("{}", Encoding.UTF8, "application/json"))
            {
                var limitResponse1 = await client1.PostAsync("/api/v1/auth/exchange", limitContent);
                limitResponse1.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
                limitResponse1.Dispose();
            }

            // Act - Make request from second client (IP: 192.168.1.2, should still work)
            using (var content2 = new StringContent("{}", Encoding.UTF8, "application/json"))
            {
                var response2 = await client2.PostAsync("/api/v1/auth/exchange", content2);

                // Assert - Second client should not be rate limited
                response2.StatusCode.ShouldBe(HttpStatusCode.OK);
                response2.Headers.GetValues("X-RateLimit-Remaining").First().ShouldBe("9"); // Fresh counter
                response2.Dispose();
            }
        }
        finally
        {
            client1.Dispose();
            client2.Dispose();
        }
    }

    [Test]
    public async Task AuthMeEndpoint_HasNoRateLimiting()
    {
        // Arrange: Create and exchange to set up principal
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt("me_rate_test");
        SetAuthorizationHeader(validJwt);
        using var exchangeContent = CreateJsonContent("{}");
        var exchangeResponse = await HttpClient.PostAsync("/api/v1/auth/exchange", exchangeContent);
        exchangeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Extract Axon token from response
        var content = await exchangeResponse.Content.ReadAsStringAsync();
        var exchangeData = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(content);
        var axonToken = exchangeData.GetProperty("accessToken").GetString();
        exchangeResponse.Dispose();

        // Act - Make multiple /auth/me requests
        SetAuthorizationHeader(axonToken!);
        var meResponse = await HttpClient.GetAsync("/api/v1/auth/me");

        // Assert - /auth/me should not have rate limiting headers
        meResponse.Headers.Contains("X-RateLimit-Limit").ShouldBeFalse();
        meResponse.Headers.Contains("X-RateLimit-Remaining").ShouldBeFalse();
        meResponse.Headers.Contains("X-RateLimit-Reset").ShouldBeFalse();

        // The endpoint should not return rate limiting status
        meResponse.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);

        meResponse.Dispose();
    }

    [Test]
    public async Task RateLimitWindow_ResetsAfter60Seconds()
    {
        // This test verifies reset header logic

        // Arrange: Create valid JWT
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt("window_test_user");
        SetAuthorizationHeader(validJwt);

        // Act
        using var content = CreateJsonContent("{}");
        var response = await HttpClient.PostAsync("/api/v1/auth/exchange", content);

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
        // Arrange: Create valid JWT
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt("header_accuracy_test");

        // Act - Make sequential requests and verify header consistency
        SetAuthorizationHeader(validJwt);
        using var firstContent = CreateJsonContent("{}");
        var firstResponse = await HttpClient.PostAsync("/api/v1/auth/exchange", firstContent);
        var firstRemaining = int.Parse(firstResponse.Headers.GetValues("X-RateLimit-Remaining").First(), CultureInfo.InvariantCulture);
        var firstLimit = firstResponse.Headers.GetValues("X-RateLimit-Limit").First();
        firstResponse.Dispose();

        SetAuthorizationHeader(validJwt);
        using var secondContent = CreateJsonContent("{}");
        var secondResponse = await HttpClient.PostAsync("/api/v1/auth/exchange", secondContent);
        var secondRemaining = int.Parse(secondResponse.Headers.GetValues("X-RateLimit-Remaining").First(), CultureInfo.InvariantCulture);
        var secondLimit = secondResponse.Headers.GetValues("X-RateLimit-Limit").First();
        secondResponse.Dispose();

        // Assert - Remaining count should decrease by exactly 1
        firstRemaining.ShouldBe(secondRemaining + 1);

        // Both should have same limit
        firstLimit.ShouldBe(secondLimit);
    }

    [Test]
    public async Task RateLimiting_BasicTest_Returns429()
    {
        // Arrange: Create valid JWT
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt("basic_rate_limit");

        // Act - Make multiple requests quickly to trigger rate limit
        var tasks = new List<Task<HttpResponseMessage>>();

        // Send 12 requests (exceeds 10 req/min limit for /auth/exchange)
        for (int i = 0; i < 12; i++)
        {
            var client = Factory.CreateClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {validJwt}");
            #pragma warning disable CA2000 // Dispose objects before losing scope - HttpClient takes ownership
            var content = new StringContent("", Encoding.UTF8, "application/json");
            var task = client.PostAsync("/api/v1/auth/exchange", content);
            #pragma warning restore CA2000
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

        // Cleanup
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }
}
