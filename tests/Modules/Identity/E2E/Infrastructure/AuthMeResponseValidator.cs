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
        // Validate principal information
        responseData.AxonUserId.ShouldBe(expectedPrincipalId);
        responseData.Email.ShouldNotBeNullOrWhiteSpace("Email should be present");

        // Validate wallets array
        responseData.Wallets.ShouldNotBeNull("Wallets array should be present");
        responseData.Wallets.Count.ShouldBe(expectedWalletCount, "Wallet count should match expected");

        // Validate chain defaults
        responseData.ChainDefaults.ShouldNotBeNull("ChainDefaults should be present");
        responseData.ChainDefaults.Count.ShouldBe(expectedDefaultsCount, "Chain defaults count should match expected");

        // Validate wallet structure if wallets exist
        foreach (var wallet in responseData.Wallets)
        {
            ValidateWalletData(wallet);
        }

        // Validate chain defaults structure if defaults exist
        foreach (var chainDefault in responseData.ChainDefaults)
        {
            ValidateChainDefaultData(chainDefault);
        }
    }

    /// <summary>
    /// Validates wallet data structure.
    /// </summary>
    private static void ValidateWalletData(WalletData wallet)
    {
        wallet.Id.ShouldNotBeNullOrWhiteSpace("Wallet ID should be present");
        wallet.ChainId.ShouldNotBeNullOrWhiteSpace("Chain ID should be present");
        wallet.Address.ShouldNotBeNullOrWhiteSpace("Wallet address should be present");
        wallet.AccessMode.ShouldNotBeNullOrWhiteSpace("Access mode should be present");
        wallet.Status.ShouldNotBeNullOrWhiteSpace("Wallet status should be present");

        // Validate enum values
        wallet.AccessMode.ShouldBeOneOf("Signing", "WatchOnly");
        wallet.Status.ShouldBeOneOf("Pending", "Verified", "Revoked");
    }

    /// <summary>
    /// Validates chain default data structure.
    /// </summary>
    private static void ValidateChainDefaultData(ChainDefaultData chainDefault)
    {
        chainDefault.ChainId.ShouldNotBeNullOrWhiteSpace("Chain ID should be present");
        chainDefault.WalletId.ShouldNotBeNullOrWhiteSpace("Wallet ID should be present");
        chainDefault.Address.ShouldNotBeNullOrWhiteSpace("Default wallet address should be present");
    }

    #endregion

    #region ETag Validation

    /// <summary>
    /// Validates ETag format and ensures it's not empty.
    /// </summary>
    public static string ValidateAndExtractETag(HttpResponseMessage response)
    {
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
    /// </summary>
    public class AuthMeResponseData
    {
        public string AxonUserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string EnvironmentId { get; set; } = string.Empty;
        public List<WalletData> Wallets { get; set; } = new();
        public List<ChainDefaultData> ChainDefaults { get; set; } = new();
        public DateTime? FirstVisitUtc { get; set; }
        public DateTime? LastVisitUtc { get; set; }
        public bool IsNewUser { get; set; }
    }

    /// <summary>
    /// Data model for wallet information in /auth/me response.
    /// </summary>
    public class WalletData
    {
        public string Id { get; set; } = string.Empty;
        public string ChainId { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string AccessMode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? VerifiedAtUtc { get; set; }
    }

    /// <summary>
    /// Data model for chain default information in /auth/me response.
    /// </summary>
    public class ChainDefaultData
    {
        public string ChainId { get; set; } = string.Empty;
        public string WalletId { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
    }

    #endregion

    #region Test Assertion Helpers

    /// <summary>
    /// Asserts that the response contains specific wallet information.
    /// </summary>
    public static void AssertWalletPresent(
        AuthMeResponseData responseData,
        string expectedAddress,
        string expectedChainId,
        string expectedAccessMode = "Signing",
        string expectedStatus = "Verified")
    {
        var wallet = responseData.Wallets.FirstOrDefault(w => w.Address == expectedAddress);
        wallet.ShouldNotBeNull($"Wallet with address {expectedAddress} should be present");

        wallet.ChainId.ShouldBe(expectedChainId);
        wallet.AccessMode.ShouldBe(expectedAccessMode);
        wallet.Status.ShouldBe(expectedStatus);
    }

    /// <summary>
    /// Asserts that the response contains specific chain default information.
    /// </summary>
    public static void AssertChainDefaultPresent(
        AuthMeResponseData responseData,
        string expectedChainId,
        string expectedAddress)
    {
        var chainDefault = responseData.ChainDefaults.FirstOrDefault(cd => cd.ChainId == expectedChainId);
        chainDefault.ShouldNotBeNull($"Chain default for {expectedChainId} should be present");

        chainDefault.Address.ShouldBe(expectedAddress);
    }

    /// <summary>
    /// Asserts that the response does not contain a specific wallet.
    /// </summary>
    public static void AssertWalletNotPresent(
        AuthMeResponseData responseData,
        string excludedAddress)
    {
        var wallet = responseData.Wallets.FirstOrDefault(w => w.Address == excludedAddress);
        wallet.ShouldBeNull($"Wallet with address {excludedAddress} should not be present");
    }

    #endregion
}