using System.Net;
using System.Text.Json;
using Shouldly;

namespace Axon.Modules.Identity.E2E.Infrastructure;

/// <summary>
/// Validator for /auth/me endpoint responses.
/// Provides structured validation of response format, ETag behavior, and data completeness.
/// </summary>
public static class AuthMeResponseValidator
{
    #region Response Structure Validation

    /// <summary>
    /// Validates the complete structure of an /auth/me response.
    /// </summary>
    public static void ValidateAuthMeResponse(
        HttpResponseMessage response,
        string expectedPrincipalId,
        int expectedWalletCount = 0,
        int expectedDefaultsCount = 0)
    {
        ArgumentNullException.ThrowIfNull(response);
        // Validate HTTP status
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Validate content type
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");

        // Validate ETag presence
        response.Headers.ETag.ShouldNotBeNull("Response should include ETag header");

        // Validate response body structure
        var content = response.Content.ReadAsStringAsync().Result;
        var responseData = JsonSerializer.Deserialize<AuthMeResponseData>(content, JsonOptions);

        responseData.ShouldNotBeNull("Response should be valid JSON");
        ValidateUserData(responseData, expectedPrincipalId, expectedWalletCount, expectedDefaultsCount);
    }

    /// <summary>
    /// Validates a 304 Not Modified response.
    /// </summary>
    public static void ValidateNotModifiedResponse(HttpResponseMessage response, string expectedETag)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.StatusCode.ShouldBe(HttpStatusCode.NotModified);

        // 304 responses should have no content
        var content = response.Content.ReadAsStringAsync().Result;
        content.ShouldBeEmpty("304 responses should have no content");

        // ETag should still be present
        response.Headers.ETag.ShouldNotBeNull("304 response should include ETag header");
        response.Headers.ETag.Tag.ShouldBe($"\"{expectedETag}\"");
    }

    /// <summary>
    /// Validates user data structure within the response.
    /// </summary>
    private static void ValidateUserData(
        AuthMeResponseData responseData,
        string expectedPrincipalId,
        int expectedWalletCount,
        int expectedDefaultsCount)
    {
        // Validate profile information
        responseData.Profile.ShouldNotBeNull("Profile should be present");
        responseData.Profile.AxonId.ShouldBe(expectedPrincipalId, "AxonId should match expected principal ID");
        responseData.Profile.RiskTier.ShouldNotBeNullOrWhiteSpace("RiskTier should be present");

        // Validate ETag in response body (in addition to header)
        responseData.ETag.ShouldNotBeNullOrWhiteSpace("ETag should be present in response body");

        // Validate wallets array
        responseData.Wallets.ShouldNotBeNull("Wallets array should be present");
        responseData.Wallets.Count.ShouldBe(expectedWalletCount, "Wallet count should match expected");

        // Validate default wallet count (wallets with IsDefault=true)
        var defaultWalletCount = responseData.Wallets.Count(w => w.IsDefault);
        defaultWalletCount.ShouldBe(expectedDefaultsCount, "Default wallet count should match expected");

        // Validate wallet structure if wallets exist
        foreach (var wallet in responseData.Wallets)
        {
            ValidateWalletData(wallet);
        }
    }

    /// <summary>
    /// Validates wallet data structure.
    /// </summary>
    private static void ValidateWalletData(WalletData wallet)
    {
        wallet.Chain.ShouldNotBeNullOrWhiteSpace("Chain should be present");
        wallet.Address.ShouldNotBeNullOrWhiteSpace("Wallet address should be present");
        wallet.State.ShouldNotBeNullOrWhiteSpace("Wallet state should be present");
        wallet.Access.ShouldNotBeNullOrWhiteSpace("Access should be present");

        // Validate enum values (API returns lowercase)
        wallet.State.ShouldBeOneOf("pending", "verified", "revoked");
        wallet.Access.ShouldBeOneOf("signing", "watch_only");
    }

    #endregion

    #region ETag Validation

    /// <summary>
    /// Validates ETag format and ensures it's not empty.
    /// </summary>
    public static string ValidateAndExtractETag(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Headers.ETag.ShouldNotBeNull("Response should include ETag header");

        var etagValue = response.Headers.ETag.Tag;
        etagValue.ShouldNotBeNullOrWhiteSpace("ETag value should not be empty");

        // ETags should be quoted
        etagValue.ShouldStartWith("\"");
        etagValue.ShouldEndWith("\"");

        // Extract unquoted value
        var unquotedEtag = etagValue.Trim('"');
        unquotedEtag.ShouldNotBeNullOrWhiteSpace("ETag content should not be empty");

        return unquotedEtag;
    }

    /// <summary>
    /// Validates that ETags are different (indicating data changed).
    /// </summary>
    public static void ValidateETagChanged(string originalETag, string newETag)
    {
        newETag.ShouldNotBe(originalETag, "ETag should change when data is modified");
    }

    /// <summary>
    /// Validates that ETags are the same (indicating data unchanged).
    /// </summary>
    public static void ValidateETagUnchanged(string originalETag, string newETag)
    {
        newETag.ShouldBe(originalETag, "ETag should remain the same when data is unchanged");
    }

    #endregion

    #region Cache Validation

    /// <summary>
    /// Validates cache headers in the response.
    /// </summary>
    public static void ValidateCacheHeaders(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);
        // Check for cache-related headers
        response.Headers.ETag.ShouldNotBeNull("Response should include ETag for caching");

        // Validate that cache-control headers are appropriate
        var cacheControl = response.Headers.CacheControl;
        if (cacheControl != null)
        {
            // For /auth/me, we typically want private caching
            cacheControl.Private.ShouldBeTrue("User data should be private cached");
        }
    }

    /// <summary>
    /// Validates behavior of conditional requests with If-None-Match.
    /// </summary>
    public static void ValidateConditionalRequestBehavior(
        HttpResponseMessage originalResponse,
        HttpResponseMessage conditionalResponse,
        bool shouldBeNotModified = true)
    {
        ArgumentNullException.ThrowIfNull(conditionalResponse);
        if (shouldBeNotModified)
        {
            conditionalResponse.StatusCode.ShouldBe(HttpStatusCode.NotModified);

            var originalETag = ValidateAndExtractETag(originalResponse);
            var conditionalETag = ValidateAndExtractETag(conditionalResponse);

            ValidateETagUnchanged(originalETag, conditionalETag);
        }
        else
        {
            conditionalResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

            var originalETag = ValidateAndExtractETag(originalResponse);
            var conditionalETag = ValidateAndExtractETag(conditionalResponse);

            ValidateETagChanged(originalETag, conditionalETag);
        }
    }

    #endregion

    #region JSON Serialization Options

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    #endregion

    #region Response Data Models

    /// <summary>
    /// Data model for /auth/me response structure.
    /// Matches GetCurrentUserResponseDto from API.
    /// </summary>
    public class AuthMeResponseData
    {
        public ProfileData Profile { get; set; } = new();
        public List<WalletData> Wallets { get; set; } = new();
        public string ETag { get; set; } = string.Empty;
    }

    /// <summary>
    /// Type alias for backward compatibility.
    /// </summary>
    public class AuthMeResponse : AuthMeResponseData
    {
    }

    /// <summary>
    /// User profile information nested in response.
    /// </summary>
    public class ProfileData
    {
        public string AxonId { get; set; } = string.Empty;
        public string RiskTier { get; set; } = string.Empty;
    }

    /// <summary>
    /// Data model for wallet information in /auth/me response.
    /// Matches WalletInfoDto from API.
    /// </summary>
    public class WalletData
    {
        public string Chain { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Access { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
    }

    #endregion

    #region Test Assertion Helpers

    /// <summary>
    /// Asserts that the response contains specific wallet information.
    /// </summary>
    public static void AssertWalletPresent(
        AuthMeResponseData responseData,
        string expectedAddress,
        string expectedChain,
        string expectedAccess = "signing",
        string expectedState = "verified")
    {
        ArgumentNullException.ThrowIfNull(responseData);
        var wallet = responseData.Wallets.FirstOrDefault(w => w.Address == expectedAddress);
        wallet.ShouldNotBeNull($"Wallet with address {expectedAddress} should be present");

        wallet.Chain.ShouldBe(expectedChain);
        wallet.Access.ShouldBe(expectedAccess);
        wallet.State.ShouldBe(expectedState);
    }

    /// <summary>
    /// Asserts that the response contains a default wallet for the specified chain.
    /// Checks that a wallet with IsDefault=true exists for the chain and address.
    /// </summary>
    public static void AssertChainDefaultPresent(
        AuthMeResponseData responseData,
        string expectedChain,
        string expectedAddress)
    {
        ArgumentNullException.ThrowIfNull(responseData);
        var defaultWallet = responseData.Wallets
            .FirstOrDefault(w => w.Chain == expectedChain && w.Address == expectedAddress && w.IsDefault);
        defaultWallet.ShouldNotBeNull($"Default wallet for chain {expectedChain} with address {expectedAddress} should be present");
    }

    /// <summary>
    /// Asserts that the response does not contain a specific wallet.
    /// </summary>
    public static void AssertWalletNotPresent(
        AuthMeResponseData responseData,
        string excludedAddress)
    {
        ArgumentNullException.ThrowIfNull(responseData);
        var wallet = responseData.Wallets.FirstOrDefault(w => w.Address == excludedAddress);
        wallet.ShouldBeNull($"Wallet with address {excludedAddress} should not be present");
    }

    #endregion
}