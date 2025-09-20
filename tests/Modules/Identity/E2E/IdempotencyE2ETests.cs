using System.Net;
using System.Text;
using System.Text.Json;
using Axon.Modules.Identity.E2E.Infrastructure;
using Axon.Modules.Identity.Infrastructure.Persistence.DbInvariants;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.E2E;

/// <summary>
/// E2E tests for Idempotency & Replays (Section F of TDD document).
/// Tests complete HTTP request idempotency for /auth/exchange endpoint.
/// Covers TDD tests 21-22 with real HTTP calls and response validation.
/// </summary>
[TestFixture]
public class IdempotencyE2ETests : E2ETestBase
{
    #region Test 21: EXCHANGE_idempotent_dynamic (E2E)

    [Test]
    public async Task ExchangeEndpoint_DynamicJWTReSubmission_ShouldReturnConsistentResponse()
    {
        // Arrange: Create valid Dynamic JWT
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt();
        using var requestPayload = CreateExchangeRequestPayload();

        // Act: Submit same request multiple times
        SetAuthorizationHeader(validJwt);
        var response1 = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);

        SetAuthorizationHeader(validJwt);
        var response2 = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);

        SetAuthorizationHeader(validJwt);
        var response3 = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);

        // Assert: All responses should be successful and consistent
        response1.StatusCode.ShouldBe(HttpStatusCode.OK);
        response2.StatusCode.ShouldBe(HttpStatusCode.OK);
        response3.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Parse response content
        var content1 = await response1.Content.ReadAsStringAsync();
        var content2 = await response2.Content.ReadAsStringAsync();
        var content3 = await response3.Content.ReadAsStringAsync();

        var exchangeResponse1 = JsonSerializer.Deserialize<ExchangeResponse>(content1, JsonOptions);
        var exchangeResponse2 = JsonSerializer.Deserialize<ExchangeResponse>(content2, JsonOptions);
        var exchangeResponse3 = JsonSerializer.Deserialize<ExchangeResponse>(content3, JsonOptions);

        // Validate response consistency
        exchangeResponse1.ShouldNotBeNull();
        exchangeResponse2.ShouldNotBeNull();
        exchangeResponse3.ShouldNotBeNull();

        // Same principal should be returned
        exchangeResponse1.AxonUserId.ShouldBe(exchangeResponse2.AxonUserId);
        exchangeResponse2.AxonUserId.ShouldBe(exchangeResponse3.AxonUserId);

        // First call creates, subsequent calls find existing
        exchangeResponse1.Created.ShouldBeTrue("First call should create principal");
        exchangeResponse2.Created.ShouldBeFalse("Second call should find existing");
        exchangeResponse3.Created.ShouldBeFalse("Third call should find existing");
    }

    [Test]
    public async Task ExchangeEndpoint_ConcurrentDynamicJWTRequests_ShouldHandleRaceConditions()
    {
        // Arrange: Create valid Dynamic JWT
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt();
        using var requestPayload = CreateExchangeRequestPayload();

        // Act: Submit concurrent requests
        var tasks = new List<Task<HttpResponseMessage>>();
        var requestContent = await requestPayload.ReadAsStringAsync();

        for (int i = 0; i < 5; i++)
        {
            var client = Factory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", validJwt);

            // HttpClient.PostAsync takes ownership of the content and disposes it
            #pragma warning disable CA2000 // Dispose objects before losing scope
            var content = new StringContent(requestContent, Encoding.UTF8, "application/json");
            tasks.Add(client.PostAsync("/api/v1/auth/exchange", content));
            #pragma warning restore CA2000 // Dispose objects before losing scope
        }

        var responses = await Task.WhenAll(tasks);

        // Assert: All responses should be successful
        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.OK,
            "All concurrent requests should succeed");

        // Parse all responses
        var exchangeResponses = new List<ExchangeResponse>();
        foreach (var response in responses)
        {
            var content = await response.Content.ReadAsStringAsync();
            var exchangeResponse = JsonSerializer.Deserialize<ExchangeResponse>(content, JsonOptions);
            exchangeResponse.ShouldNotBeNull();
            exchangeResponses.Add(exchangeResponse);
        }

        // All should resolve to same principal
        var firstPrincipalId = exchangeResponses[0].AxonUserId;
        exchangeResponses.ShouldAllBe(r => r.AxonUserId == firstPrincipalId,
            "All concurrent requests should resolve to same principal");

        // Only one should create, others should find existing
        var createdCount = exchangeResponses.Count(r => r.Created);
        createdCount.ShouldBe(1, "Exactly one request should create the principal");
    }

    [Test]
    public async Task ExchangeEndpoint_DifferentTimestamps_ShouldRemainIdempotent()
    {
        // Arrange: Create valid Dynamic JWT
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt();
        using var requestPayload = CreateExchangeRequestPayload();

        // Act: Submit requests at different times
        SetAuthorizationHeader(validJwt);
        var response1 = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);

        // Advance time
        AdvanceTime(TimeSpan.FromMinutes(10));
        SetAuthorizationHeader(validJwt);
        var response2 = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);

        // Advance time again
        AdvanceTime(TimeSpan.FromHours(1));
        SetAuthorizationHeader(validJwt);
        var response3 = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);

        // Assert: All should succeed with same principal
        response1.StatusCode.ShouldBe(HttpStatusCode.OK);
        response2.StatusCode.ShouldBe(HttpStatusCode.OK);
        response3.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content1 = await response1.Content.ReadAsStringAsync();
        var content2 = await response2.Content.ReadAsStringAsync();
        var content3 = await response3.Content.ReadAsStringAsync();

        var exchangeResponse1 = JsonSerializer.Deserialize<ExchangeResponse>(content1, JsonOptions);
        var exchangeResponse2 = JsonSerializer.Deserialize<ExchangeResponse>(content2, JsonOptions);
        var exchangeResponse3 = JsonSerializer.Deserialize<ExchangeResponse>(content3, JsonOptions);

        // Same principal across time
        exchangeResponse1.AxonUserId.ShouldBe(exchangeResponse2.AxonUserId);
        exchangeResponse2.AxonUserId.ShouldBe(exchangeResponse3.AxonUserId);

        // Only first creates
        exchangeResponse1.Created.ShouldBeTrue();
        exchangeResponse2.Created.ShouldBeFalse();
        exchangeResponse3.Created.ShouldBeFalse();
    }

    #endregion

    #region Test 22: EXCHANGE_idempotent_wallet (E2E)

    [Test]
    public async Task ExchangeEndpoint_WalletProofReSubmission_ShouldBeIdempotent()
    {
        // Arrange: Create request with wallet proof
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt("unknown_user_12345");
        using var requestPayload = CreateExchangeRequestWithWallet(
            TestDataFixtures.W1MainAddress,
            TestDataFixtures.SolanaMainnetChain);

        // Act: Submit same wallet proof multiple times
        SetAuthorizationHeader(validJwt);
        var response1 = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);

        SetAuthorizationHeader(validJwt);
        var response2 = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);

        SetAuthorizationHeader(validJwt);
        var response3 = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);

        // Assert: All should succeed with consistent results
        response1.StatusCode.ShouldBe(HttpStatusCode.OK);
        response2.StatusCode.ShouldBe(HttpStatusCode.OK);
        response3.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content1 = await response1.Content.ReadAsStringAsync();
        var content2 = await response2.Content.ReadAsStringAsync();
        var content3 = await response3.Content.ReadAsStringAsync();

        var exchangeResponse1 = JsonSerializer.Deserialize<ExchangeResponse>(content1, JsonOptions);
        var exchangeResponse2 = JsonSerializer.Deserialize<ExchangeResponse>(content2, JsonOptions);
        var exchangeResponse3 = JsonSerializer.Deserialize<ExchangeResponse>(content3, JsonOptions);

        // Same principal should be returned
        exchangeResponse1.AxonUserId.ShouldBe(exchangeResponse2.AxonUserId);
        exchangeResponse2.AxonUserId.ShouldBe(exchangeResponse3.AxonUserId);

        // First call creates principal and links wallet
        exchangeResponse1.Created.ShouldBeTrue("First call should create principal");
        exchangeResponse1.WalletsLinked.ShouldBe(1);

        // Subsequent calls should find existing without re-linking
        exchangeResponse2.Created.ShouldBeFalse("Second call should find existing");
        exchangeResponse2.WalletsLinked.ShouldBe(0, "Wallet should already be linked");

        exchangeResponse3.Created.ShouldBeFalse("Third call should find existing");
        exchangeResponse3.WalletsLinked.ShouldBe(0, "Wallet should already be linked");
    }

    [Test]
    public async Task ExchangeEndpoint_WalletProofWithTTLVariation_ShouldRemainIdempotent()
    {
        // Arrange: Create request with wallet proof
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt("test_user_ttl_67890");
        using var requestPayload = CreateExchangeRequestWithWallet(
            TestDataFixtures.W2MainAddress,
            TestDataFixtures.SolanaMainnetChain);

        // Act: Submit within TTL
        SetAuthorizationHeader(validJwt);
        var response1 = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);

        // Advance time but stay within reasonable TTL
        AdvanceTime(TimeSpan.FromMinutes(30));
        SetAuthorizationHeader(validJwt);
        var response2 = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);

        // Advance beyond typical TTL
        AdvanceTime(TimeSpan.FromHours(2));
        SetAuthorizationHeader(validJwt);
        var response3 = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);

        // Assert: All should succeed regardless of TTL
        response1.StatusCode.ShouldBe(HttpStatusCode.OK);
        response2.StatusCode.ShouldBe(HttpStatusCode.OK);
        response3.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content1 = await response1.Content.ReadAsStringAsync();
        var content2 = await response2.Content.ReadAsStringAsync();
        var content3 = await response3.Content.ReadAsStringAsync();

        var exchangeResponse1 = JsonSerializer.Deserialize<ExchangeResponse>(content1, JsonOptions);
        var exchangeResponse2 = JsonSerializer.Deserialize<ExchangeResponse>(content2, JsonOptions);
        var exchangeResponse3 = JsonSerializer.Deserialize<ExchangeResponse>(content3, JsonOptions);

        // Should resolve to same principal regardless of TTL expiry
        exchangeResponse1.AxonUserId.ShouldBe(exchangeResponse2.AxonUserId);
        exchangeResponse2.AxonUserId.ShouldBe(exchangeResponse3.AxonUserId);

        // Only first should create and link
        exchangeResponse1.Created.ShouldBeTrue();
        exchangeResponse2.Created.ShouldBeFalse();
        exchangeResponse3.Created.ShouldBeFalse();
    }

    [Test]
    public async Task ExchangeEndpoint_MixedCredentialAndWallet_ShouldBeIdempotentAcrossTypes()
    {
        // Arrange: Create request with both credential and wallet
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt();
        using var requestWithWallet = CreateExchangeRequestWithWallet(
            TestDataFixtures.W1MainAddress,
            TestDataFixtures.SolanaMainnetChain);

        // Act: First request with wallet
        SetAuthorizationHeader(validJwt);
        var response1 = await HttpClient.PostAsync("/api/v1/auth/exchange", requestWithWallet);

        // Second request with same credential but different wallet
        using var requestWithDifferentWallet = CreateExchangeRequestWithWallet(
            TestDataFixtures.W2MainAddress,
            TestDataFixtures.SolanaMainnetChain);

        SetAuthorizationHeader(validJwt);
        var response2 = await HttpClient.PostAsync("/api/v1/auth/exchange", requestWithDifferentWallet);

        // Third request with original wallet again
        SetAuthorizationHeader(validJwt);
        var response3 = await HttpClient.PostAsync("/api/v1/auth/exchange", requestWithWallet);

        // Assert: All should succeed and resolve to same principal
        response1.StatusCode.ShouldBe(HttpStatusCode.OK);
        response2.StatusCode.ShouldBe(HttpStatusCode.OK);
        response3.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content1 = await response1.Content.ReadAsStringAsync();
        var content2 = await response2.Content.ReadAsStringAsync();
        var content3 = await response3.Content.ReadAsStringAsync();

        var exchangeResponse1 = JsonSerializer.Deserialize<ExchangeResponse>(content1, JsonOptions);
        var exchangeResponse2 = JsonSerializer.Deserialize<ExchangeResponse>(content2, JsonOptions);
        var exchangeResponse3 = JsonSerializer.Deserialize<ExchangeResponse>(content3, JsonOptions);

        // All should resolve to same principal (credential takes precedence)
        exchangeResponse1.AxonUserId.ShouldBe(exchangeResponse2.AxonUserId);
        exchangeResponse2.AxonUserId.ShouldBe(exchangeResponse3.AxonUserId);

        // First creates principal, second links additional wallet, third is pure idempotent
        exchangeResponse1.Created.ShouldBeTrue("First call creates principal");
        exchangeResponse2.Created.ShouldBeFalse("Second call finds existing via credential");
        exchangeResponse3.Created.ShouldBeFalse("Third call finds existing");

        // Wallet linking behavior
        exchangeResponse1.WalletsLinked.ShouldBe(1, "First wallet linked");
        exchangeResponse2.WalletsLinked.ShouldBe(1, "Second wallet linked to same principal");
        exchangeResponse3.WalletsLinked.ShouldBe(0, "First wallet already linked");
    }

    #endregion

    #region Response Header Validation

    [Test]
    public async Task ExchangeEndpoint_IdempotentRequests_ShouldMaintainResponseHeaders()
    {
        // Arrange: Create valid request
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt();
        using var requestPayload = CreateExchangeRequestPayload();

        // Act: Submit multiple requests
        SetAuthorizationHeader(validJwt);
        var response1 = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);

        SetAuthorizationHeader(validJwt);
        var response2 = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);

        // Assert: Headers should be consistent
        response1.StatusCode.ShouldBe(HttpStatusCode.OK);
        response2.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Content-Type should be consistent
        response1.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
        response2.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");

        // Response structure should be identical
        var content1 = await response1.Content.ReadAsStringAsync();
        var content2 = await response2.Content.ReadAsStringAsync();

        var json1 = JsonSerializer.Deserialize<JsonElement>(content1);
        var json2 = JsonSerializer.Deserialize<JsonElement>(content2);

        // AxonUserId should be identical
        json1.GetProperty("axonUserId").GetString()
            .ShouldBe(json2.GetProperty("axonUserId").GetString());
    }

    #endregion

    #region Error Scenarios with Idempotency

    [Test]
    public async Task ExchangeEndpoint_IdempotentErrorScenarios_ShouldBeConsistent()
    {
        // Arrange: Create invalid JWT (expired)
        var expiredJwt = JwtTestTokenFactory.CreateExpiredJwt();
        using var requestPayload = CreateExchangeRequestPayload();

        // Act: Submit same invalid request multiple times
        SetAuthorizationHeader(expiredJwt);
        var response1 = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);

        SetAuthorizationHeader(expiredJwt);
        var response2 = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);

        SetAuthorizationHeader(expiredJwt);
        var response3 = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);

        // Assert: Error responses should be consistent
        response1.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response2.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response3.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var content1 = await response1.Content.ReadAsStringAsync();
        var content2 = await response2.Content.ReadAsStringAsync();
        var content3 = await response3.Content.ReadAsStringAsync();

        var error1 = JsonSerializer.Deserialize<ErrorResponse>(content1, JsonOptions);
        var error2 = JsonSerializer.Deserialize<ErrorResponse>(content2, JsonOptions);
        var error3 = JsonSerializer.Deserialize<ErrorResponse>(content3, JsonOptions);

        // Error codes and messages should be identical
        error1.Code.ShouldBe(error2.Code);
        error2.Code.ShouldBe(error3.Code);
        error1.Message.ShouldBe(error2.Message);
        error2.Message.ShouldBe(error3.Message);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a basic exchange request payload.
    /// </summary>
    private static StringContent CreateExchangeRequestPayload()
    {
        var requestData = new
        {
            environmentId = TestDataFixtures.MainnetEnvironment,
            wallets = Array.Empty<object>()
        };

        var json = JsonSerializer.Serialize(requestData, JsonOptions);
        return CreateJsonContent(json);
    }

    /// <summary>
    /// Creates an exchange request payload with wallet data.
    /// </summary>
    private static StringContent CreateExchangeRequestWithWallet(string address, string chainId)
    {
        var requestData = new
        {
            environmentId = TestDataFixtures.MainnetEnvironment,
            wallets = new[]
            {
                new
                {
                    address,
                    chain = chainId
                }
            }
        };

        var json = JsonSerializer.Serialize(requestData, JsonOptions);
        return CreateJsonContent(json);
    }

    #endregion

    #region JSON Serialization Options

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    #endregion

    #region Response Models

    /// <summary>
    /// Model for exchange response.
    /// </summary>
    private sealed class ExchangeResponse
    {
        public string AxonUserId { get; set; } = string.Empty;
        public bool Created { get; set; }
        public int WalletsLinked { get; set; }
        public int DefaultsApplied { get; set; }
    }

    /// <summary>
    /// Model for error response.
    /// </summary>
    private sealed class ErrorResponse
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public object? Details { get; set; }
    }

    #endregion
}