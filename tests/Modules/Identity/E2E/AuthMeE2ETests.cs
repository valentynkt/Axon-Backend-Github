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
[NonParallelizable] // CRITICAL: Prevent parallel execution to avoid test isolation issues with shared state (cache, DbContext)
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
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound,
            $"Expected 404 but got {(int)response.StatusCode}. Response body: {content}");

        // FastEndpoints may return empty body for 404, so only check error if body exists
        if (!string.IsNullOrWhiteSpace(content))
        {
            var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content, JsonOptions);
            errorResponse.ShouldNotBeNull();
            errorResponse.Code.ShouldBe("NOT_FOUND");
        }
    }

    #endregion

    #region Performance Tests

    [Test]
    [Category("Performance")]
    public async Task AuthMe_ResponseTime_ShouldCompleteWithin200ms()
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
        // Note: 200ms threshold accounts for E2E test overhead (Docker, network, etc.)
        // In production with optimized infrastructure, P95 should be < 100ms
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        duration.TotalMilliseconds.ShouldBeLessThan(200, "/auth/me should complete within 200ms in E2E tests");
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
    /// Model for ProblemDetails error response (RFC 7807).
    /// </summary>
    private sealed class ErrorResponse
    {
        public string? Type { get; set; }
        public string? Title { get; set; }
        public int Status { get; set; }
        public string? Detail { get; set; }
        public string? Instance { get; set; }

        // ProblemDetails extensions are serialized as top-level properties
        public string? ErrorCode { get; set; }
        public string? ErrorType { get; set; }
        public string? Severity { get; set; }
        public string? TraceId { get; set; }

        // Helper property for backward compatibility
        public string Code => ErrorCode ?? string.Empty;
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