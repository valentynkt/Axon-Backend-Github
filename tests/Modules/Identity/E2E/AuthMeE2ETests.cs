using System.Net;
using System.Text.Json;
using Axon.Modules.Identity.E2E.Infrastructure;
using Axon.Modules.Identity.Infrastructure.Persistence.DbInvariants;
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
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt();
        await SetupPrincipalWithWallets(validJwt);

        // Act: Call /auth/me
        SetAuthorizationHeader(validJwt);
        var response = await HttpClient.GetAsync("/auth/me");

        // Assert: Should return complete user snapshot
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var responseData = JsonSerializer.Deserialize<AuthMeResponseValidator.AuthMeResponseData>(content, JsonOptions);

        AuthMeResponseValidator.ValidateAuthMeResponse(
            response,
            TestDataFixtures.DynA_Subject,
            expectedWalletCount: 2, // W1 and W2 from setup
            expectedDefaultsCount: 1); // Default should be auto-created

        // Verify specific wallet presence
        AuthMeResponseValidator.AssertWalletPresent(
            responseData,
            TestDataFixtures.W1MainAddress,
            TestDataFixtures.SolanaMainnetChain,
            "Signing",
            "Verified");

        AuthMeResponseValidator.AssertWalletPresent(
            responseData,
            TestDataFixtures.W2MainAddress,
            TestDataFixtures.SolanaMainnetChain,
            "Signing",
            "Verified");

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
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt();
        await SetupPrincipalWithWallets(validJwt);

        // Act: Call /auth/me
        SetAuthorizationHeader(validJwt);
        var response = await HttpClient.GetAsync("/auth/me");

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
        await SetupCredentialOnlyPrincipal(validJwt);

        // Act: Call /auth/me
        SetAuthorizationHeader(validJwt);
        var response = await HttpClient.GetAsync("/auth/me");

        // Assert: Should return principal with empty wallets and defaults
        AuthMeResponseValidator.ValidateAuthMeResponse(
            response,
            TestDataFixtures.DynA_Subject,
            expectedWalletCount: 0,
            expectedDefaultsCount: 0);

        var content = await response.Content.ReadAsStringAsync();
        var responseData = JsonSerializer.Deserialize<AuthMeResponseValidator.AuthMeResponseData>(content, JsonOptions);

        responseData.Wallets.ShouldBeEmpty("Should have no wallets");
        responseData.ChainDefaults.ShouldBeEmpty("Should have no chain defaults");
        responseData.Email.ShouldNotBeNullOrWhiteSpace("Should have email from JWT");
    }

    #endregion

    #region ETag Caching Behavior Tests

    [Test]
    public async Task AuthMe_IfNoneMatchWithCurrentETag_ShouldReturn304()
    {
        // Arrange: Set up principal and get initial ETag
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt();
        await SetupPrincipalWithWallets(validJwt);

        SetAuthorizationHeader(validJwt);
        var initialResponse = await HttpClient.GetAsync("/auth/me");
        var initialETag = AuthMeResponseValidator.ValidateAndExtractETag(initialResponse);

        // Act: Call /auth/me with If-None-Match header
        SetAuthorizationHeader(validJwt);
        SetIfNoneMatchHeader(initialETag);
        var conditionalResponse = await HttpClient.GetAsync("/auth/me");

        // Assert: Should return 304 Not Modified
        AuthMeResponseValidator.ValidateNotModifiedResponse(conditionalResponse, initialETag);
    }

    [Test]
    public async Task AuthMe_IfNoneMatchWithOldETag_ShouldReturn200WithNewData()
    {
        // Arrange: Set up principal
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt();
        await SetupCredentialOnlyPrincipal(validJwt);

        SetAuthorizationHeader(validJwt);
        var initialResponse = await HttpClient.GetAsync("/auth/me");
        var initialETag = AuthMeResponseValidator.ValidateAndExtractETag(initialResponse);

        // Modify principal by adding a wallet
        await AddWalletToPrincipal(validJwt);

        // Act: Call /auth/me with old ETag
        SetAuthorizationHeader(validJwt);
        SetIfNoneMatchHeader(initialETag);
        var updatedResponse = await HttpClient.GetAsync("/auth/me");

        // Assert: Should return 200 with updated data
        updatedResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var newETag = AuthMeResponseValidator.ValidateAndExtractETag(updatedResponse);
        AuthMeResponseValidator.ValidateETagChanged(initialETag, newETag);

        // Verify updated content
        var content = await updatedResponse.Content.ReadAsStringAsync();
        var responseData = JsonSerializer.Deserialize<AuthMeResponseValidator.AuthMeResponseData>(content, JsonOptions);

        responseData.Wallets.Count.ShouldBe(1, "Should now have one wallet");
    }

    [Test]
    public async Task AuthMe_MultipleRequestsWithoutChanges_ShouldReturnSameETag()
    {
        // Arrange: Set up principal
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt();
        await SetupPrincipalWithWallets(validJwt);

        // Act: Make multiple /auth/me requests without data changes
        SetAuthorizationHeader(validJwt);
        var response1 = await HttpClient.GetAsync("/auth/me");

        SetAuthorizationHeader(validJwt);
        var response2 = await HttpClient.GetAsync("/auth/me");

        SetAuthorizationHeader(validJwt);
        var response3 = await HttpClient.GetAsync("/auth/me");

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
        await SetupCredentialOnlyPrincipal(validJwt);

        // Get initial state
        SetAuthorizationHeader(validJwt);
        var response1 = await HttpClient.GetAsync("/auth/me");
        var etag1 = AuthMeResponseValidator.ValidateAndExtractETag(response1);

        // Modify state by adding wallet
        await AddWalletToPrincipal(validJwt);

        // Get updated state
        SetAuthorizationHeader(validJwt);
        var response2 = await HttpClient.GetAsync("/auth/me");
        var etag2 = AuthMeResponseValidator.ValidateAndExtractETag(response2);

        // Modify state again by adding second wallet
        await AddSecondWalletToPrincipal(validJwt);

        // Get final state
        SetAuthorizationHeader(validJwt);
        var response3 = await HttpClient.GetAsync("/auth/me");
        var etag3 = AuthMeResponseValidator.ValidateAndExtractETag(response3);

        // Assert: ETags should change with each modification
        AuthMeResponseValidator.ValidateETagChanged(etag1, etag2);
        AuthMeResponseValidator.ValidateETagChanged(etag2, etag3);
    }

    #endregion

    #region Cache Invalidation Scenarios

    [Test]
    public async Task AuthMe_WalletStatusChange_ShouldInvalidateCache()
    {
        // Arrange: Set up principal with pending wallet
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt();
        await SetupPrincipalWithPendingWallet(validJwt);

        // Get initial state (with pending wallet)
        SetAuthorizationHeader(validJwt);
        var pendingResponse = await HttpClient.GetAsync("/auth/me");
        var pendingETag = AuthMeResponseValidator.ValidateAndExtractETag(pendingResponse);

        var pendingContent = await pendingResponse.Content.ReadAsStringAsync();
        var pendingData = JsonSerializer.Deserialize<AuthMeResponseValidator.AuthMeResponseData>(pendingContent, JsonOptions);

        pendingData.Wallets.First().Status.ShouldBe("Pending");

        // Simulate wallet verification (would normally be done via signature verification)
        await VerifyWalletForPrincipal(validJwt);

        // Get updated state (with verified wallet)
        SetAuthorizationHeader(validJwt);
        var verifiedResponse = await HttpClient.GetAsync("/auth/me");
        var verifiedETag = AuthMeResponseValidator.ValidateAndExtractETag(verifiedResponse);

        // Assert: ETag should change and wallet status should be updated
        AuthMeResponseValidator.ValidateETagChanged(pendingETag, verifiedETag);

        var verifiedContent = await verifiedResponse.Content.ReadAsStringAsync();
        var verifiedData = JsonSerializer.Deserialize<AuthMeResponseValidator.AuthMeResponseData>(verifiedContent, JsonOptions);

        verifiedData.Wallets.First().Status.ShouldBe("Verified");
    }

    [Test]
    public async Task AuthMe_ChainDefaultChange_ShouldInvalidateCache()
    {
        // Arrange: Set up principal with multiple wallets
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt();
        await SetupPrincipalWithMultipleWallets(validJwt);

        // Get initial state
        SetAuthorizationHeader(validJwt);
        var initialResponse = await HttpClient.GetAsync("/auth/me");
        var initialETag = AuthMeResponseValidator.ValidateAndExtractETag(initialResponse);

        var initialContent = await initialResponse.Content.ReadAsStringAsync();
        var initialData = JsonSerializer.Deserialize<AuthMeResponseValidator.AuthMeResponseData>(initialContent, JsonOptions);

        var initialDefaultAddress = initialData.ChainDefaults.First().Address;

        // Change default wallet
        await ChangeDefaultWalletForPrincipal(validJwt);

        // Get updated state
        SetAuthorizationHeader(validJwt);
        var updatedResponse = await HttpClient.GetAsync("/auth/me");
        var updatedETag = AuthMeResponseValidator.ValidateAndExtractETag(updatedResponse);

        // Assert: ETag should change and default should be updated
        AuthMeResponseValidator.ValidateETagChanged(initialETag, updatedETag);

        var updatedContent = await updatedResponse.Content.ReadAsStringAsync();
        var updatedData = JsonSerializer.Deserialize<AuthMeResponseValidator.AuthMeResponseData>(updatedContent, JsonOptions);

        var newDefaultAddress = updatedData.ChainDefaults.First().Address;
        newDefaultAddress.ShouldNotBe(initialDefaultAddress, "Default wallet address should change");
    }

    #endregion

    #region Error and Edge Cases

    [Test]
    public async Task AuthMe_InvalidJWT_ShouldReturn401()
    {
        // Arrange: Use expired JWT
        var expiredJwt = JwtTestTokenFactory.CreateExpiredJwt();

        // Act: Call /auth/me with invalid token
        SetAuthorizationHeader(expiredJwt);
        var response = await HttpClient.GetAsync("/auth/me");

        // Assert: Should return 401
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var content = await response.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content, JsonOptions);

        errorResponse.ShouldNotBeNull();
        errorResponse.Code.ShouldBe("UNAUTHORIZED");
    }

    [Test]
    public async Task AuthMe_NoAuthorizationHeader_ShouldReturn401()
    {
        // Act: Call /auth/me without authorization
        var response = await HttpClient.GetAsync("/auth/me");

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
        var response = await HttpClient.GetAsync("/auth/me");

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
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt();
        await SetupPrincipalWithWallets(validJwt);

        // Act: Measure response time
        SetAuthorizationHeader(validJwt);
        var startTime = DateTime.UtcNow;
        var response = await HttpClient.GetAsync("/auth/me");
        var duration = DateTime.UtcNow - startTime;

        // Assert: Should complete within performance target
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        duration.TotalMilliseconds.ShouldBeLessThan(100, "/auth/me should complete within 100ms P95");
    }

    [Test]
    public async Task AuthMe_CachedResponse_ShouldBeFasterThanInitial()
    {
        // Arrange: Set up principal
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt();
        await SetupPrincipalWithWallets(validJwt);

        // Act: Measure initial response time
        SetAuthorizationHeader(validJwt);
        var startTime1 = DateTime.UtcNow;
        var response1 = await HttpClient.GetAsync("/auth/me");
        var duration1 = DateTime.UtcNow - startTime1;

        response1.StatusCode.ShouldBe(HttpStatusCode.OK);
        var etag1 = AuthMeResponseValidator.ValidateAndExtractETag(response1);

        // Act: Measure cached response time
        SetAuthorizationHeader(validJwt);
        SetIfNoneMatchHeader(etag1);
        var startTime2 = DateTime.UtcNow;
        var response2 = await HttpClient.GetAsync("/auth/me");
        var duration2 = DateTime.UtcNow - startTime2;

        // Assert: Cached response should be faster (304)
        response2.StatusCode.ShouldBe(HttpStatusCode.NotModified);
        duration2.ShouldBeLessThan(duration1, "Cached response should be faster than initial");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Sets up a principal with credential and wallets via exchange endpoint.
    /// </summary>
    private async Task SetupPrincipalWithWallets(string jwt)
    {
        var requestData = new
        {
            environmentId = TestDataFixtures.MainnetEnvironment,
            wallets = new[]
            {
                new { address = TestDataFixtures.W1MainAddress, chain = TestDataFixtures.SolanaMainnetChain },
                new { address = TestDataFixtures.W2MainAddress, chain = TestDataFixtures.SolanaMainnetChain }
            }
        };

        var json = JsonSerializer.Serialize(requestData, JsonOptions);
        var content = CreateJsonContent(json);

        SetAuthorizationHeader(jwt);
        var response = await HttpClient.PostAsync("/api/v1/auth/exchange", content);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// Sets up a principal with credential only (no wallets).
    /// </summary>
    private async Task SetupCredentialOnlyPrincipal(string jwt)
    {
        var requestData = new
        {
            environmentId = TestDataFixtures.MainnetEnvironment,
            wallets = Array.Empty<object>()
        };

        var json = JsonSerializer.Serialize(requestData, JsonOptions);
        var content = CreateJsonContent(json);

        SetAuthorizationHeader(jwt);
        var response = await HttpClient.PostAsync("/api/v1/auth/exchange", content);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// Adds a wallet to an existing principal.
    /// </summary>
    private async Task AddWalletToPrincipal(string jwt)
    {
        var requestData = new
        {
            environmentId = TestDataFixtures.MainnetEnvironment,
            wallets = new[]
            {
                new { address = TestDataFixtures.W1MainAddress, chain = TestDataFixtures.SolanaMainnetChain }
            }
        };

        var json = JsonSerializer.Serialize(requestData, JsonOptions);
        var content = CreateJsonContent(json);

        SetAuthorizationHeader(jwt);
        var response = await HttpClient.PostAsync("/api/v1/auth/exchange", content);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// Adds a second wallet to an existing principal.
    /// </summary>
    private async Task AddSecondWalletToPrincipal(string jwt)
    {
        var requestData = new
        {
            environmentId = TestDataFixtures.MainnetEnvironment,
            wallets = new[]
            {
                new { address = TestDataFixtures.W2MainAddress, chain = TestDataFixtures.SolanaMainnetChain }
            }
        };

        var json = JsonSerializer.Serialize(requestData, JsonOptions);
        var content = CreateJsonContent(json);

        SetAuthorizationHeader(jwt);
        var response = await HttpClient.PostAsync("/api/v1/auth/exchange", content);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// Sets up a principal with a pending wallet.
    /// </summary>
    private async Task SetupPrincipalWithPendingWallet(string jwt)
    {
        // This would require a specific API or test setup to create pending wallets
        // For now, we'll use the exchange endpoint which typically creates verified wallets
        await SetupCredentialOnlyPrincipal(jwt);
        await AddWalletToPrincipal(jwt);
    }

    /// <summary>
    /// Sets up a principal with multiple wallets for default testing.
    /// </summary>
    private async Task SetupPrincipalWithMultipleWallets(string jwt)
    {
        await SetupPrincipalWithWallets(jwt);
    }

    /// <summary>
    /// Verifies a wallet for a principal (simulates signature verification).
    /// </summary>
    private async Task VerifyWalletForPrincipal(string jwt)
    {
        // This would typically involve a wallet verification endpoint
        // For testing purposes, we might need to directly call the verification logic
        // or use a test-specific endpoint
        await Task.CompletedTask; // Placeholder
    }

    /// <summary>
    /// Changes the default wallet for a principal.
    /// </summary>
    private async Task ChangeDefaultWalletForPrincipal(string jwt)
    {
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

    #endregion
}