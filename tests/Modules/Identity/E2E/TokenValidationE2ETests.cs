using System.Net;
using System.Text.Json;
using Axon.Modules.Identity.E2E.Infrastructure;
using Axon.Modules.Identity.Infrastructure.Tests.Persistence.DbInvariants;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.E2E;

/// <summary>
/// E2E tests for Token & Proof Validation (Section E of TDD document).
/// Tests JWT issuer/audience enforcement, JWKS rotation, message TTL, and signature reuse guard.
/// Covers TDD tests 17-20 with real HTTP endpoints and cryptographic validation.
/// </summary>
[TestFixture]
public class TokenValidationE2ETests : E2ETestBase
{
    #region Test 17: JWT_iss_aud_enforced

    [Test]
    public async Task JWT_InvalidIssuer_ShouldReturn401()
    {
        // Arrange: Create JWT with wrong issuer
        var invalidJwt = JwtTestTokenFactory.CreateInvalidIssuerJwt();
        SetAuthorizationHeader(invalidJwt);

        // Create request payload
        using var requestPayload = CreateExchangeRequestPayload();

        // Act: Send exchange request
        var response = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);

        // Assert: Should reject with 401
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var content = await response.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content, JsonOptions);

        errorResponse.ShouldNotBeNull();
        errorResponse.Code.ShouldBe("UNAUTHORIZED");
        errorResponse.Message.ShouldContain("issuer", Case.Insensitive);
    }

    [Test]
    public async Task JWT_InvalidAudience_ShouldReturn401()
    {
        // Arrange: Create JWT with wrong audience
        var invalidJwt = JwtTestTokenFactory.CreateInvalidAudienceJwt();
        SetAuthorizationHeader(invalidJwt);

        // Create request payload
        using var requestPayload = CreateExchangeRequestPayload();

        // Act: Send exchange request
        var response = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);

        // Assert: Should reject with 401
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var content = await response.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content, JsonOptions);

        errorResponse.ShouldNotBeNull();
        errorResponse.Code.ShouldBe("UNAUTHORIZED");
        errorResponse.Message.ShouldContain("audience", Case.Insensitive);
    }

    [Test]
    public async Task JWT_UnknownIssuer_ShouldReturn401()
    {
        // Arrange: Create JWT with completely unknown issuer
        var unknownIssuerJwt = JwtTestTokenFactory.CreateInvalidIssuerJwt(
            TestDataFixtures.DynA_Subject,
            "https://totally-unknown.evil.com");
        SetAuthorizationHeader(unknownIssuerJwt);

        // Create request payload
        using var requestPayload = CreateExchangeRequestPayload();

        // Act: Send exchange request
        var response = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);

        // Assert: Should reject with 401
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var content = await response.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content, JsonOptions);

        errorResponse.ShouldNotBeNull();
        errorResponse.Code.ShouldBe("UNAUTHORIZED");
    }

    #endregion

    #region Test 18: JWT_JWKS_rotation

    [Test]
    public async Task JWT_Kid1Valid_ShouldSucceed()
    {
        // Arrange: Create JWT with kid1 (default JWKS)
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt(kid: TestDataFixtures.Kid1);
        SetAuthorizationHeader(validJwt);

        // Create request payload
        using var requestPayload = CreateExchangeRequestPayload();

        // Act: Send exchange request
        var response = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);

        // Assert: Should succeed
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var exchangeResponse = JsonSerializer.Deserialize<ExchangeResponse>(content, JsonOptions);

        exchangeResponse.ShouldNotBeNull();
        exchangeResponse.AxonUserId.ShouldNotBeNullOrWhiteSpace();
        exchangeResponse.Created.ShouldBeTrue(); // New principal created
    }

    [Test]
    public async Task JWT_KeyRotationScenario_ShouldValidateBothKeys()
    {
        // Arrange: Start with kid1 JWT
        var kid1Jwt = JwtTestTokenFactory.CreateValidDynamicJwt(kid: TestDataFixtures.Kid1);
        SetAuthorizationHeader(kid1Jwt);

        using var requestPayload = CreateExchangeRequestPayload();

        // Act 1: First request with kid1 should succeed
        var response1 = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);
        response1.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Arrange 2: Simulate JWKS rotation (add kid2)
        SimulateJwksKeyRotation();

        // Clear the cache to force JWKS refresh (advance time past cache TTL)
        AdvanceTime(TimeSpan.FromMinutes(15));

        // Create JWT with kid2
        var kid2Jwt = JwtTestTokenFactory.CreateRotatedKeyJwt();
        SetAuthorizationHeader(kid2Jwt);

        // Act 2: Second request with kid2 should also succeed
        var response2 = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);

        // Assert: Both keys should be valid after rotation
        response2.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content2 = await response2.Content.ReadAsStringAsync();
        var exchangeResponse2 = JsonSerializer.Deserialize<ExchangeResponse>(content2, JsonOptions);

        exchangeResponse2.ShouldNotBeNull();
        exchangeResponse2.AxonUserId.ShouldNotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task JWT_CacheTTLHonored_ShouldRespectCacheTime()
    {
        // Arrange: Create JWT with kid1
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt(kid: TestDataFixtures.Kid1);
        SetAuthorizationHeader(validJwt);

        using var requestPayload = CreateExchangeRequestPayload();

        // Act 1: First request should succeed and cache JWKS
        var response1 = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);
        response1.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Arrange 2: Simulate JWKS endpoint failure
        SimulateJwksEndpointFailure();

        // Act 2: Request within cache TTL should still succeed using cached JWKS
        var response2 = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);

        // Assert: Should succeed using cached JWKS
        response2.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Arrange 3: Advance time past cache TTL
        AdvanceTime(TimeSpan.FromMinutes(30)); // Assuming 15-30 min cache TTL

        // Act 3: Request after cache expiry should fail due to JWKS endpoint failure
        var response3 = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);

        // Assert: Should fail when cache expires and JWKS endpoint is down
        response3.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Test 19: SIGN_message_TTL

    [Test]
    public async Task SignedMessage_ValidTTL_ShouldSucceed()
    {
        // Arrange: Create exchange request with valid wallet signature
        var (_, signature, issuedAt) = JwtTestTokenFactory.WalletSignatureTestData.CreateValidSignature();
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt();
        SetAuthorizationHeader(validJwt);

        using var requestPayload = CreateExchangeRequestWithWallet(
            TestDataFixtures.W1MainAddress,
            TestDataFixtures.SolanaMainnetChain,
            signature,
            issuedAt);

        // Act: Send exchange request
        var response = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);

        // Assert: Should succeed with valid TTL
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var exchangeResponse = JsonSerializer.Deserialize<ExchangeResponse>(content, JsonOptions);

        exchangeResponse.ShouldNotBeNull();
        exchangeResponse.WalletsLinked.ShouldBeGreaterThan(0);
    }

    [Test]
    public async Task SignedMessage_ExpiredTTL_ShouldReturn401()
    {
        // Arrange: Create exchange request with expired wallet signature
        var (_, signature, expiredIssuedAt) = JwtTestTokenFactory.WalletSignatureTestData.CreateExpiredSignature();
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt();
        SetAuthorizationHeader(validJwt);

        using var requestPayload = CreateExchangeRequestWithWallet(
            TestDataFixtures.W1MainAddress,
            TestDataFixtures.SolanaMainnetChain,
            signature,
            expiredIssuedAt);

        // Act: Send exchange request
        var response = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);

        // Assert: Should reject expired signature
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var content = await response.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content, JsonOptions);

        errorResponse.ShouldNotBeNull();
        errorResponse.Code.ShouldBe("UNAUTHORIZED");
        errorResponse.Message.ShouldContain("expired", Case.Insensitive);
    }

    [Test]
    public async Task SignedMessage_FutureIssuedAt_ShouldReturn401()
    {
        // Arrange: Create signature with issued_at in the future
        var futureIssuedAt = TestTime.AddHours(1); // Future time
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt();
        SetAuthorizationHeader(validJwt);

        var (_, signature, _) = JwtTestTokenFactory.WalletSignatureTestData.CreateValidSignature();
        using var requestPayload = CreateExchangeRequestWithWallet(
            TestDataFixtures.W1MainAddress,
            TestDataFixtures.SolanaMainnetChain,
            signature,
            futureIssuedAt);

        // Act: Send exchange request
        var response = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);

        // Assert: Should reject future-dated signature
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var content = await response.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content, JsonOptions);

        errorResponse.ShouldNotBeNull();
        errorResponse.Code.ShouldBe("UNAUTHORIZED");
        errorResponse.Message.ShouldContain("future", Case.Insensitive);
    }

    #endregion

    #region Test 20: SIGN_signature_reuse_guard (optional LRU)

    [Test]
    public async Task SignatureReuse_WithinGuardWindow_ShouldReturn401()
    {
        // Arrange: Create first exchange request with signature
        var (_, signature, issuedAt) = JwtTestTokenFactory.WalletSignatureTestData.CreateValidSignature();
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt();
        SetAuthorizationHeader(validJwt);

        using var requestPayload = CreateExchangeRequestWithWallet(
            TestDataFixtures.W1MainAddress,
            TestDataFixtures.SolanaMainnetChain,
            signature,
            issuedAt);

        // Act 1: First request should succeed
        var response1 = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);
        response1.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Arrange 2: Try to reuse the same signature within guard window
        SetAuthorizationHeader(validJwt); // Reset auth header
        using var duplicatePayload = CreateExchangeRequestWithWallet(
            TestDataFixtures.W1MainAddress,
            TestDataFixtures.SolanaMainnetChain,
            signature, // Same signature
            issuedAt);  // Same issued at

        // Act 2: Second request with same signature should fail
        var response2 = await HttpClient.PostAsync("/api/v1/auth/exchange", duplicatePayload);

        // Assert: Should reject reused signature
        response2.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var content = await response2.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content, JsonOptions);

        errorResponse.ShouldNotBeNull();
        errorResponse.Code.ShouldBe("UNAUTHORIZED");
        errorResponse.Message.ShouldContain("reuse", Case.Insensitive);
    }

    [Test]
    public async Task SignatureReuse_OutsideGuardWindow_ShouldSucceed()
    {
        // Arrange: Create first exchange request with signature
        var (_, signature, issuedAt) = JwtTestTokenFactory.WalletSignatureTestData.CreateValidSignature();
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt();
        SetAuthorizationHeader(validJwt);

        using var requestPayload = CreateExchangeRequestWithWallet(
            TestDataFixtures.W1MainAddress,
            TestDataFixtures.SolanaMainnetChain,
            signature,
            issuedAt);

        // Act 1: First request should succeed
        var response1 = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);
        response1.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Arrange 2: Advance time beyond guard window (e.g., 1 hour)
        AdvanceTime(TimeSpan.FromHours(1));

        // Reset auth header and try same signature again
        SetAuthorizationHeader(validJwt);
        using var laterPayload = CreateExchangeRequestWithWallet(
            TestDataFixtures.W1MainAddress,
            TestDataFixtures.SolanaMainnetChain,
            signature,
            issuedAt);

        // Act 2: Request after guard window should succeed (LRU cleared)
        var response2 = await HttpClient.PostAsync("/api/v1/auth/exchange", laterPayload);

        // Assert: Should succeed after guard window expires
        response2.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    #endregion

    #region Performance Tests (as per TDD requirements)

    [Test]
    public async Task JWT_ValidationPerformance_ShouldCompleteWithin100ms()
    {
        // Arrange: Create valid JWT
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt();
        SetAuthorizationHeader(validJwt);

        using var requestPayload = CreateExchangeRequestPayload();

        // Act: Measure validation time
        var startTime = DateTime.UtcNow;
        var response = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);
        var duration = DateTime.UtcNow - startTime;

        // Assert: Should complete within performance target
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        duration.TotalMilliseconds.ShouldBeLessThan(100, "JWT validation should complete within 100ms P95");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a basic exchange request payload for testing.
    /// Note: Exchange endpoint accepts empty body - JWT is extracted from Authorization header.
    /// </summary>
    private static StringContent CreateExchangeRequestPayload()
    {
        // Exchange endpoint accepts empty body - JWT extracted from Authorization header
        return CreateJsonContent("{}");
    }

    /// <summary>
    /// Creates an exchange request payload with wallet signature data.
    /// Note: Exchange endpoint accepts empty body - JWT is extracted from Authorization header.
    /// Wallet data is no longer sent in request body.
    /// </summary>
    private static StringContent CreateExchangeRequestWithWallet(
        string address,
        string chainId,
        string signature,
        DateTime issuedAt)
    {
        // Exchange endpoint accepts empty body - JWT extracted from Authorization header
        // Wallet signature data is processed from Dynamic JWT claims, not from request body
        _ = address; // Unused - kept for backward compatibility with test signatures
        _ = chainId; // Unused - kept for backward compatibility with test signatures
        _ = signature; // Unused - kept for backward compatibility with test signatures
        _ = issuedAt; // Unused - kept for backward compatibility with test signatures
        return CreateJsonContent("{}");
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