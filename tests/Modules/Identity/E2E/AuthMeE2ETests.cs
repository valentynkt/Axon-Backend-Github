using System.Net;
using System.Text.Json;
using Axon.Modules.Identity.E2E.Infrastructure;
using Axon.Modules.Identity.Infrastructure.Tests.Persistence.DbInvariants;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.E2E;

/// <summary>
/// E2E tests for /auth/me Snapshot & Caching (Section G of TDD document).
/// Tests TDD requirement 23: Complete /auth/me response validation with ETag caching.
/// Validates principal data, wallets, chain defaults, and HTTP caching behavior.
/// </summary>
[TestFixture]
public class AuthMeE2ETests : E2ETestBase
{
    #region Test 23: ME_snapshot_contains_defaults_and_wallets

    [Test]
    public async Task AuthMe_NewPrincipalWithWallets_ShouldReturnCompleteSnapshot()
    {
        // Arrange: Create and exchange to set up principal with wallets
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwtWithWallets();
        var (axonToken, principalId) = await SetupPrincipalWithWallets(validJwt);

        // Act: Call /auth/me using the Axon Access Token
        SetAuthorizationHeader(axonToken);
        var response = await HttpClient.GetAsync("/api/v1/auth/me");

        // Assert: Should return complete user snapshot
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var responseData = JsonSerializer.Deserialize<AuthMeResponseValidator.AuthMeResponseData>(content, JsonOptions)!;

        AuthMeResponseValidator.ValidateAuthMeResponse(
            response,
            principalId, // Use the actual Principal GUID returned from exchange
            expectedWalletCount: 2, // W1 and W2 from setup
            expectedDefaultsCount: 1); // Default should be auto-created

        // Verify specific wallet presence (API returns lowercase enum values)
        AuthMeResponseValidator.AssertWalletPresent(
            responseData,
            TestDataFixtures.W1MainAddress,
            TestDataFixtures.SolanaMainnetChain,
            "signing",
            "verified");

        AuthMeResponseValidator.AssertWalletPresent(
            responseData,
            TestDataFixtures.W2MainAddress,
            TestDataFixtures.SolanaMainnetChain,
            "signing",
            "verified");

        // Verify chain default presence
        AuthMeResponseValidator.AssertChainDefaultPresent(
            responseData,
            TestDataFixtures.SolanaMainnetChain,
            TestDataFixtures.W1MainAddress); // First wallet should be default
    }

    [Test]
    public async Task AuthMe_WithETagHeader_ShouldReturnETagForCaching()
    {
        // Arrange: Set up principal
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwtWithWallets();
        var (axonToken, _) = await SetupPrincipalWithWallets(validJwt);

        // Act: Call /auth/me using the Axon Access Token
        SetAuthorizationHeader(axonToken);
        var response = await HttpClient.GetAsync("/api/v1/auth/me");

        // Assert: Should include ETag header
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var etag = AuthMeResponseValidator.ValidateAndExtractETag(response);
        etag.ShouldNotBeNullOrWhiteSpace("ETag should be present and non-empty");

        // Validate cache headers
        AuthMeResponseValidator.ValidateCacheHeaders(response);
    }

    [Test]
    public async Task AuthMe_CredentialOnlyPrincipal_ShouldReturnMinimalSnapshot()
    {
        // Arrange: Create principal with credential only (no wallets)
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt();
        var (axonToken, principalId) = await SetupCredentialOnlyPrincipal(validJwt);

        // Act: Call /auth/me using the Axon Access Token
        SetAuthorizationHeader(axonToken);
        var response = await HttpClient.GetAsync("/api/v1/auth/me");

        // Assert: Should return principal with empty wallets and defaults
        AuthMeResponseValidator.ValidateAuthMeResponse(
            response,
            principalId,
            expectedWalletCount: 0,
            expectedDefaultsCount: 0);

        var content = await response.Content.ReadAsStringAsync();
        var responseData = JsonSerializer.Deserialize<AuthMeResponseValidator.AuthMeResponseData>(content, JsonOptions)!;

        responseData.Wallets.ShouldBeEmpty("Should have no wallets");
        responseData.Profile.RiskTier.ShouldNotBeNullOrWhiteSpace("Should have RiskTier");
    }

    #endregion

    #region ETag Caching Behavior Tests

    [Test]
    public async Task AuthMe_IfNoneMatchWithCurrentETag_ShouldReturn304()
    {
        // Arrange: Set up principal and get initial ETag
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwtWithWallets();
        var (axonToken, _) = await SetupPrincipalWithWallets(validJwt);

        ClearAllHeaders();
        SetAuthorizationHeader(axonToken);
        var initialResponse = await HttpClient.GetAsync("/api/v1/auth/me");
        var initialETag = AuthMeResponseValidator.ValidateAndExtractETag(initialResponse);

        // Act: Call /auth/me with If-None-Match header
        ClearAllHeaders();
        SetAuthorizationHeader(axonToken);
        SetIfNoneMatchHeader(initialETag);
        var conditionalResponse = await HttpClient.GetAsync("/api/v1/auth/me");

        // Assert: Should return 304 Not Modified
        AuthMeResponseValidator.ValidateNotModifiedResponse(conditionalResponse, initialETag);
    }

    [Test]
    public async Task AuthMe_IfNoneMatchWithOldETag_ShouldReturn200WithNewData()
    {
        // Arrange: Set up principal
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt();
        var (axonToken, _) = await SetupCredentialOnlyPrincipal(validJwt);

        ClearAllHeaders();
        SetAuthorizationHeader(axonToken);
        var initialResponse = await HttpClient.GetAsync("/api/v1/auth/me");
        var initialETag = AuthMeResponseValidator.ValidateAndExtractETag(initialResponse);

        // Modify principal by adding a wallet
        var (updatedAxonToken, _) = await AddWalletToPrincipal();

        // Act: Call /auth/me with old ETag using updated token
        ClearAllHeaders();
        SetAuthorizationHeader(updatedAxonToken);
        SetIfNoneMatchHeader(initialETag);
        var updatedResponse = await HttpClient.GetAsync("/api/v1/auth/me");

        // Assert: Should return 200 with updated data
        updatedResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var newETag = AuthMeResponseValidator.ValidateAndExtractETag(updatedResponse);
        AuthMeResponseValidator.ValidateETagChanged(initialETag, newETag);

        // Verify updated content
        var content = await updatedResponse.Content.ReadAsStringAsync();
        var responseData = JsonSerializer.Deserialize<AuthMeResponseValidator.AuthMeResponseData>(content, JsonOptions)!;

        responseData.Wallets.Count.ShouldBe(1, "Should now have one wallet");
    }

    [Test]
    public async Task AuthMe_MultipleRequestsWithoutChanges_ShouldReturnSameETag()
    {
        // Arrange: Set up principal
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwtWithWallets();
        var (axonToken, _) = await SetupPrincipalWithWallets(validJwt);

        // Act: Make multiple /auth/me requests without data changes
        SetAuthorizationHeader(axonToken);
        var response1 = await HttpClient.GetAsync("/api/v1/auth/me");

        SetAuthorizationHeader(axonToken);
        var response2 = await HttpClient.GetAsync("/api/v1/auth/me");

        SetAuthorizationHeader(axonToken);
        var response3 = await HttpClient.GetAsync("/api/v1/auth/me");

        // Assert: ETags should be identical
        var etag1 = AuthMeResponseValidator.ValidateAndExtractETag(response1);
        var etag2 = AuthMeResponseValidator.ValidateAndExtractETag(response2);
        var etag3 = AuthMeResponseValidator.ValidateAndExtractETag(response3);

        AuthMeResponseValidator.ValidateETagUnchanged(etag1, etag2);
        AuthMeResponseValidator.ValidateETagUnchanged(etag2, etag3);
    }

    [Test]
    public async Task AuthMe_DataChangesBetweenRequests_ShouldUpdateETag()
    {
        // Arrange: Set up initial principal
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt();
        var (axonToken, _) = await SetupCredentialOnlyPrincipal(validJwt);

        // Get initial state
        SetAuthorizationHeader(axonToken);
        var response1 = await HttpClient.GetAsync("/api/v1/auth/me");
        var etag1 = AuthMeResponseValidator.ValidateAndExtractETag(response1);

        // Modify state by adding wallet
        var (axonToken2, _) = await AddWalletToPrincipal();

        // Get updated state
        SetAuthorizationHeader(axonToken2);
        var response2 = await HttpClient.GetAsync("/api/v1/auth/me");
        var etag2 = AuthMeResponseValidator.ValidateAndExtractETag(response2);

        // Modify state again by adding second wallet
        var (axonToken3, _) = await AddSecondWalletToPrincipal();

        // Get final state
        SetAuthorizationHeader(axonToken3);
        var response3 = await HttpClient.GetAsync("/api/v1/auth/me");
        var etag3 = AuthMeResponseValidator.ValidateAndExtractETag(response3);

        // Assert: ETags should change with each modification
        AuthMeResponseValidator.ValidateETagChanged(etag1, etag2);
        AuthMeResponseValidator.ValidateETagChanged(etag2, etag3);
    }

    #endregion

    #region Cache Invalidation Scenarios

    // NOTE: Test removed because Dynamic-attested wallets are immediately verified (by design).
    // The exchange endpoint creates wallets with VerificationSource.DynamicAttested,
    // which means they are verified immediately and never in "pending" state.
    // This test was written aspirationally before the Dynamic integration was fully implemented.
    // If we need to test pending→verified transitions, we would need a different endpoint
    // that creates wallets without Dynamic attestation (e.g., user-initiated wallet adds).

    [Test]
    public async Task AuthMe_ChainDefaultChange_ShouldInvalidateCache()
    {
        // Arrange: Set up principal with multiple wallets
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt();
        var (axonToken, _) = await SetupPrincipalWithMultipleWallets(validJwt);

        // Get initial state
        ClearAllHeaders();
        SetAuthorizationHeader(axonToken);
        var initialResponse = await HttpClient.GetAsync("/api/v1/auth/me");
        var initialETag = AuthMeResponseValidator.ValidateAndExtractETag(initialResponse);

        var initialContent = await initialResponse.Content.ReadAsStringAsync();
        var initialData = JsonSerializer.Deserialize<AuthMeResponseValidator.AuthMeResponseData>(initialContent, JsonOptions)!;

        var initialDefaultWallet = initialData.Wallets.First(w => w.IsDefault);
        var initialDefaultAddress = initialDefaultWallet.Address;

        // Change default wallet
        await ChangeDefaultWalletForPrincipal(validJwt);

        // Get updated state - use same token as default change doesn't affect token
        ClearAllHeaders();
        SetAuthorizationHeader(axonToken);
        var updatedResponse = await HttpClient.GetAsync("/api/v1/auth/me");
        var updatedETag = AuthMeResponseValidator.ValidateAndExtractETag(updatedResponse);

        // Assert: ETag should change and default should be updated
        AuthMeResponseValidator.ValidateETagChanged(initialETag, updatedETag);

        var updatedContent = await updatedResponse.Content.ReadAsStringAsync();
        var updatedData = JsonSerializer.Deserialize<AuthMeResponseValidator.AuthMeResponseData>(updatedContent, JsonOptions)!;

        var newDefaultWallet = updatedData.Wallets.First(w => w.IsDefault);
        var newDefaultAddress = newDefaultWallet.Address;
        newDefaultAddress.ShouldNotBe(initialDefaultAddress, "Default wallet address should change");
    }

    #endregion

    #region Error and Edge Cases

    [Test]
    public async Task AuthMe_InvalidJWT_ShouldReturn401()
    {
        // Arrange: Use JWT with invalid issuer (test cannot use expired JWT because ValidateLifetime=false in test setup)
        var invalidIssuerJwt = JwtTestTokenFactory.CreateInvalidIssuerJwt();

        // Act: Call /auth/me with invalid token
        ClearAllHeaders();
        SetAuthorizationHeader(invalidIssuerJwt);
        var response = await HttpClient.GetAsync("/api/v1/auth/me");

        // Assert: Should return 401 Unauthorized
        // Note: JWT middleware rejects invalid tokens at middleware level (before endpoint),
        // so the response may not have a JSON body like endpoint-level errors.
        // The important validation is the 401 status code.
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task AuthMe_NoAuthorizationHeader_ShouldReturn401()
    {
        // Act: Call /auth/me without authorization
        var response = await HttpClient.GetAsync("/api/v1/auth/me");

        // Assert: Should return 401
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task AuthMe_NonexistentPrincipal_ShouldReturn404()
    {
        // Arrange: Create JWT for user that doesn't exist in system
        var unknownUserJwt = JwtTestTokenFactory.CreateValidDynamicJwt("unknown_user_999999");

        // Act: Call /auth/me
        SetAuthorizationHeader(unknownUserJwt);
        var response = await HttpClient.GetAsync("/api/v1/auth/me");

        // Assert: Should return 404 (principal not found)
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var content = await response.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content, JsonOptions);

        errorResponse.ShouldNotBeNull();
        errorResponse.Code.ShouldBe("NOT_FOUND");
    }

    #endregion

    #region Performance Tests

    [Test]
    public async Task AuthMe_ResponseTime_ShouldCompleteWithin100ms()
    {
        // Arrange: Set up principal with data
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwtWithWallets();
        var (axonToken, _) = await SetupPrincipalWithWallets(validJwt);

        // Act: Measure response time
        SetAuthorizationHeader(axonToken);
        var startTime = DateTime.UtcNow;
        var response = await HttpClient.GetAsync("/api/v1/auth/me");
        var duration = DateTime.UtcNow - startTime;

        // Assert: Should complete within performance target
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        duration.TotalMilliseconds.ShouldBeLessThan(100, "/auth/me should complete within 100ms P95");
    }

    [Test]
    public async Task AuthMe_CachedResponse_ShouldBeFasterThanInitial()
    {
        // Arrange: Set up principal
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwtWithWallets();
        var (axonToken, _) = await SetupPrincipalWithWallets(validJwt);

        // Act: Measure initial response time
        ClearAllHeaders();
        SetAuthorizationHeader(axonToken);
        var startTime1 = DateTime.UtcNow;
        var response1 = await HttpClient.GetAsync("/api/v1/auth/me");
        var duration1 = DateTime.UtcNow - startTime1;

        response1.StatusCode.ShouldBe(HttpStatusCode.OK);
        var etag1 = AuthMeResponseValidator.ValidateAndExtractETag(response1);

        // Act: Measure cached response time
        ClearAllHeaders();
        SetAuthorizationHeader(axonToken);
        SetIfNoneMatchHeader(etag1);
        var startTime2 = DateTime.UtcNow;
        var response2 = await HttpClient.GetAsync("/api/v1/auth/me");
        var duration2 = DateTime.UtcNow - startTime2;

        // Assert: Cached response should be faster (304)
        response2.StatusCode.ShouldBe(HttpStatusCode.NotModified);
        duration2.ShouldBeLessThan(duration1, "Cached response should be faster than initial");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Sets up a principal with credential and wallets via exchange endpoint.
    /// Note: Exchange endpoint accepts empty body - JWT is extracted from Authorization header.
    /// Returns tuple of (Axon Access Token, Principal ID) for use in subsequent requests.
    /// </summary>
    private async Task<(string AccessToken, string PrincipalId)> SetupPrincipalWithWallets(string jwt)
    {
        // Exchange endpoint accepts empty body - JWT extracted from Authorization header
        using var content = CreateJsonContent("{}");

        ClearAllHeaders();
        SetAuthorizationHeader(jwt);
        var response = await HttpClient.PostAsync("/api/v1/auth/exchange", content);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Extract and return the Axon Access Token and Principal ID from the response
        var responseContent = await response.Content.ReadAsStringAsync();
        var exchangeResponse = JsonSerializer.Deserialize<ExchangeResponse>(responseContent, JsonOptions);
        exchangeResponse.ShouldNotBeNull();
        exchangeResponse.AccessToken.ShouldNotBeNullOrWhiteSpace();
        exchangeResponse.AxonUserId.ShouldNotBeNullOrWhiteSpace();

        // Allow database transaction to commit before subsequent reads
        // Read context might not immediately see writes from write context due to transaction isolation
        await Task.Delay(100);

        // Clear headers after setup to prevent pollution
        ClearAllHeaders();

        return (exchangeResponse.AccessToken, exchangeResponse.AxonUserId);
    }

    /// <summary>
    /// Sets up a principal with credential only (no wallets).
    /// Note: Exchange endpoint accepts empty body - JWT is extracted from Authorization header.
    /// Returns tuple of (Axon Access Token, Principal ID) for use in subsequent requests.
    /// </summary>
    private async Task<(string AccessToken, string PrincipalId)> SetupCredentialOnlyPrincipal(string jwt)
    {
        // Exchange endpoint accepts empty body - JWT extracted from Authorization header
        using var content = CreateJsonContent("{}");

        ClearAllHeaders();
        SetAuthorizationHeader(jwt);
        var response = await HttpClient.PostAsync("/api/v1/auth/exchange", content);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Extract and return the Axon Access Token and Principal ID from the response
        var responseContent = await response.Content.ReadAsStringAsync();
        var exchangeResponse = JsonSerializer.Deserialize<ExchangeResponse>(responseContent, JsonOptions);
        exchangeResponse.ShouldNotBeNull();
        exchangeResponse.AccessToken.ShouldNotBeNullOrWhiteSpace();
        exchangeResponse.AxonUserId.ShouldNotBeNullOrWhiteSpace();

        // Allow database transaction to commit before subsequent reads
        await Task.Delay(100);

        // Clear headers after setup to prevent pollution
        ClearAllHeaders();

        return (exchangeResponse.AccessToken, exchangeResponse.AxonUserId);
    }

    /// <summary>
    /// Adds a wallet to an existing principal by creating a NEW JWT with one wallet FOR THE SAME SUBJECT.
    /// Note: Exchange endpoint accepts empty body - JWT is extracted from Authorization header.
    /// Returns tuple of (Axon Access Token, Principal ID) for use in subsequent requests.
    /// </summary>
    /// <param name="subject">The Dynamic subject ID to use (must match the existing principal)</param>
    private async Task<(string AccessToken, string PrincipalId)> AddWalletToPrincipal(string subject = TestDataFixtures.DynA_Subject)
    {
        // Create NEW JWT with ONE wallet (W1) to add to the principal
        // This is necessary because exchange endpoint processes wallets from the JWT
        // CRITICAL: Must use the SAME subject as the existing principal!
        var jwtWithOneWallet = JwtTestTokenFactory.CreateValidDynamicJwtWithWallets(
            subject: subject,
            wallets: new List<(string, string, string?, string?)>
            {
                (TestDataFixtures.W1MainAddress, TestDataFixtures.SolanaMainnetChain, "Phantom", "phantom")
            });

        // Exchange endpoint accepts empty body - JWT extracted from Authorization header
        using var content = CreateJsonContent("{}");

        ClearAllHeaders();
        SetAuthorizationHeader(jwtWithOneWallet);
        var response = await HttpClient.PostAsync("/api/v1/auth/exchange", content);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Extract and return the Axon Access Token and Principal ID from the response
        var responseContent = await response.Content.ReadAsStringAsync();
        var exchangeResponse = JsonSerializer.Deserialize<ExchangeResponse>(responseContent, JsonOptions);
        exchangeResponse.ShouldNotBeNull();
        exchangeResponse.AccessToken.ShouldNotBeNullOrWhiteSpace();
        exchangeResponse.AxonUserId.ShouldNotBeNullOrWhiteSpace();

        // Allow database transaction to commit before subsequent reads
        await Task.Delay(100);

        // Clear headers after setup to prevent pollution
        ClearAllHeaders();

        return (exchangeResponse.AccessToken, exchangeResponse.AxonUserId);
    }

    /// <summary>
    /// Adds a second wallet to an existing principal by creating a NEW JWT with two wallets FOR THE SAME SUBJECT.
    /// Note: Exchange endpoint accepts empty body - JWT is extracted from Authorization header.
    /// Returns tuple of (Axon Access Token, Principal ID) for use in subsequent requests.
    /// </summary>
    /// <param name="subject">The Dynamic subject ID to use (must match the existing principal)</param>
    private async Task<(string AccessToken, string PrincipalId)> AddSecondWalletToPrincipal(string subject = TestDataFixtures.DynA_Subject)
    {
        // Create NEW JWT with TWO wallets (W1 and W2) to add to the principal
        // This is necessary because exchange endpoint processes wallets from the JWT
        // CRITICAL: Must use the SAME subject as the existing principal!
        var jwtWithTwoWallets = JwtTestTokenFactory.CreateValidDynamicJwtWithWallets(
            subject: subject,
            wallets: new List<(string, string, string?, string?)>
            {
                (TestDataFixtures.W1MainAddress, TestDataFixtures.SolanaMainnetChain, "Phantom", "phantom"),
                (TestDataFixtures.W2MainAddress, TestDataFixtures.SolanaMainnetChain, "Phantom", "phantom")
            });

        // Exchange endpoint accepts empty body - JWT extracted from Authorization header
        using var content = CreateJsonContent("{}");

        ClearAllHeaders();
        SetAuthorizationHeader(jwtWithTwoWallets);
        var response = await HttpClient.PostAsync("/api/v1/auth/exchange", content);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Extract and return the Axon Access Token and Principal ID from the response
        var responseContent = await response.Content.ReadAsStringAsync();
        var exchangeResponse = JsonSerializer.Deserialize<ExchangeResponse>(responseContent, JsonOptions);
        exchangeResponse.ShouldNotBeNull();
        exchangeResponse.AccessToken.ShouldNotBeNullOrWhiteSpace();
        exchangeResponse.AxonUserId.ShouldNotBeNullOrWhiteSpace();

        // Allow database transaction to commit before subsequent reads
        await Task.Delay(100);

        // Clear headers after setup to prevent pollution
        ClearAllHeaders();

        return (exchangeResponse.AccessToken, exchangeResponse.AxonUserId);
    }

    /// <summary>
    /// Sets up a principal with multiple wallets for default testing.
    /// Returns tuple of (Axon Access Token, Principal ID) for use in subsequent requests.
    /// </summary>
    private async Task<(string AccessToken, string PrincipalId)> SetupPrincipalWithMultipleWallets(string jwt)
    {
        return await SetupPrincipalWithWallets(jwt);
    }

    /// <summary>
    /// Changes the default wallet for a principal.
    /// </summary>
    private static async Task ChangeDefaultWalletForPrincipal(string jwt)
    {
        _ = jwt; // Unused parameter placeholder
        // This would involve calling an endpoint to change chain defaults
        // For testing purposes, this might be a separate API call
        await Task.CompletedTask; // Placeholder
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
    /// Model for error response.
    /// </summary>
    private sealed class ErrorResponse
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public object? Details { get; set; }
    }

    /// <summary>
    /// Model for exchange response containing the Axon Access Token.
    /// </summary>
    private sealed class ExchangeResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string TokenType { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
        public string AxonUserId { get; set; } = string.Empty;
        public bool Created { get; set; }
        public int WalletsLinked { get; set; }
        public int Conflicts { get; set; }
    }

    #endregion
}