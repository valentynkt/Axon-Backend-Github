using System.Net;
using System.Text;
using System.Text.Json;
using Axon.Modules.Identity.E2E.Infrastructure;
using Axon.Modules.Identity.Infrastructure.Tests.Persistence.DbInvariants;
using NSec.Cryptography;
using NUnit.Framework;
using Shouldly;
using SimpleBase;

namespace Axon.Modules.Identity.E2E;

/// <summary>
/// E2E tests for wallet signature verification in the authentication flow.
/// Tests the complete integration from HTTP request through to signature verification service.
/// Covers real-world scenarios with actual Ed25519 signatures and various encoding formats.
/// </summary>
[TestFixture]
public class WalletSignatureE2ETests : E2ETestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    #region Challenge/Exchange Flow Integration Tests

    [Test]
    public async Task ExchangeEndpoint_WithValidWalletSignature_ShouldSucceed()
    {
        // Arrange - Create a valid Dynamic JWT
        var dynamicJwt = JwtTestTokenFactory.CreateValidDynamicJwt(
            subject: TestDataFixtures.DynA_Subject,
            issuer: TestDataFixtures.DynamicIssuer);

        SetAuthorizationHeader(dynamicJwt);

        // Create a valid Ed25519 signature for a test wallet
        using var key = Key.Create(SignatureAlgorithm.Ed25519);
        var walletAddress = GetSolanaAddressFromKey(key);
        var message = CreateChallengeMessage(walletAddress);
        var signature = CreateSignatureForMessage(key, message);

        // Create exchange request with wallet signature
        using var requestPayload = CreateExchangeRequestWithWalletSignature(
            walletAddress, message, signature);

        // Act - Send exchange request
        var response = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);

        // Assert - Should succeed with wallet signature verification
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var exchangeResponse = JsonSerializer.Deserialize<ExchangeResponse>(content, JsonOptions);

        exchangeResponse.ShouldNotBeNull();
        exchangeResponse.Success.ShouldBeTrue();
        exchangeResponse.AxonUserId.ShouldNotBeNullOrEmpty();
    }

    [Test]
    public async Task ExchangeEndpoint_WithInvalidWalletSignature_ShouldReturn400()
    {
        // Arrange - Create a valid Dynamic JWT
        var dynamicJwt = JwtTestTokenFactory.CreateValidDynamicJwt(
            subject: TestDataFixtures.DynA_Subject,
            issuer: TestDataFixtures.DynamicIssuer);

        SetAuthorizationHeader(dynamicJwt);

        // Create an invalid signature (corrupted)
        using var key = Key.Create(SignatureAlgorithm.Ed25519);
        var walletAddress = GetSolanaAddressFromKey(key);
        var message = CreateChallengeMessage(walletAddress);
        var validSignature = CreateSignatureForMessage(key, message);

        // Corrupt the signature
        var signatureBytes = Base58.Bitcoin.Decode(validSignature);
        signatureBytes[0] ^= 0xFF;
        var corruptedSignature = Base58.Bitcoin.Encode(signatureBytes);

        using var requestPayload = CreateExchangeRequestWithWalletSignature(
            walletAddress, message, corruptedSignature);

        // Act
        var response = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);

        // Assert - Should fail with validation error
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content, JsonOptions);

        errorResponse.ShouldNotBeNull();
        errorResponse.Code.ShouldContain("SIGNATURE");
    }

    [Test]
    public async Task ExchangeEndpoint_WithBase64EncodedSignature_ShouldSucceed()
    {
        // Arrange - Test Base64 encoding auto-detection
        var dynamicJwt = JwtTestTokenFactory.CreateValidDynamicJwt(
            subject: TestDataFixtures.DynA_Subject,
            issuer: TestDataFixtures.DynamicIssuer);

        SetAuthorizationHeader(dynamicJwt);

        using var key = Key.Create(SignatureAlgorithm.Ed25519);
        var walletAddress = GetSolanaAddressFromKey(key);
        var message = CreateChallengeMessage(walletAddress);

        // Create Base64 encoded signature instead of Base58
        var messageBytes = Encoding.UTF8.GetBytes(message);
        var signatureBytes = SignatureAlgorithm.Ed25519.Sign(key, messageBytes);
        var base64Signature = Convert.ToBase64String(signatureBytes);

        using var requestPayload = CreateExchangeRequestWithWalletSignature(
            walletAddress, message, base64Signature);

        // Act
        var response = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);

        // Assert - Should succeed with Base64 encoding detection
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var exchangeResponse = JsonSerializer.Deserialize<ExchangeResponse>(content, JsonOptions);

        exchangeResponse.ShouldNotBeNull();
        exchangeResponse.Success.ShouldBeTrue();
    }

    [Test]
    public async Task ExchangeEndpoint_WithBase64UrlEncodedSignature_ShouldSucceed()
    {
        // Arrange - Test Base64Url encoding auto-detection
        var dynamicJwt = JwtTestTokenFactory.CreateValidDynamicJwt(
            subject: TestDataFixtures.DynA_Subject,
            issuer: TestDataFixtures.DynamicIssuer);

        SetAuthorizationHeader(dynamicJwt);

        using var key = Key.Create(SignatureAlgorithm.Ed25519);
        var walletAddress = GetSolanaAddressFromKey(key);
        var message = CreateChallengeMessage(walletAddress);

        // Create Base64Url encoded signature
        var messageBytes = Encoding.UTF8.GetBytes(message);
        var signatureBytes = SignatureAlgorithm.Ed25519.Sign(key, messageBytes);
        var base64UrlSignature = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(signatureBytes);

        using var requestPayload = CreateExchangeRequestWithWalletSignature(
            walletAddress, message, base64UrlSignature);

        // Act
        var response = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);

        // Assert - Should succeed with Base64Url encoding detection
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var exchangeResponse = JsonSerializer.Deserialize<ExchangeResponse>(content, JsonOptions);

        exchangeResponse.ShouldNotBeNull();
        exchangeResponse.Success.ShouldBeTrue();
    }

    #endregion

    #region Message Format Sensitivity Tests

    [Test]
    public async Task ExchangeEndpoint_MessageFormattingDifference_ShouldFail()
    {
        // Arrange - Test that message formatting must be exact
        var dynamicJwt = JwtTestTokenFactory.CreateValidDynamicJwt(
            subject: TestDataFixtures.DynA_Subject,
            issuer: TestDataFixtures.DynamicIssuer);

        SetAuthorizationHeader(dynamicJwt);

        using var key = Key.Create(SignatureAlgorithm.Ed25519);
        var walletAddress = GetSolanaAddressFromKey(key);

        // Sign the original message
        var originalMessage = "{\"wallet\":\"" + walletAddress + "\",\"action\":\"authenticate\"}";
        var signature = CreateSignatureForMessage(key, originalMessage);

        // Use slightly different message (added space)
        var modifiedMessage = "{\"wallet\": \"" + walletAddress + "\", \"action\": \"authenticate\"}";

        using var requestPayload = CreateExchangeRequestWithWalletSignature(
            walletAddress, modifiedMessage, signature);

        // Act
        var response = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);

        // Assert - Should fail due to message formatting difference
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content, JsonOptions);

        errorResponse.ShouldNotBeNull();
        errorResponse.Code.ShouldContain("SIGNATURE");
    }

    [Test]
    public async Task ExchangeEndpoint_UnicodeInMessage_ShouldSucceed()
    {
        // Arrange - Test Unicode character handling
        var dynamicJwt = JwtTestTokenFactory.CreateValidDynamicJwt(
            subject: TestDataFixtures.DynA_Subject,
            issuer: TestDataFixtures.DynamicIssuer);

        SetAuthorizationHeader(dynamicJwt);

        using var key = Key.Create(SignatureAlgorithm.Ed25519);
        var walletAddress = GetSolanaAddressFromKey(key);
        var message = "Hello 世界! 🌍 Authenticate wallet: " + walletAddress;
        var signature = CreateSignatureForMessage(key, message);

        using var requestPayload = CreateExchangeRequestWithWalletSignature(
            walletAddress, message, signature);

        // Act
        var response = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);

        // Assert - Should succeed with Unicode characters
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var exchangeResponse = JsonSerializer.Deserialize<ExchangeResponse>(content, JsonOptions);

        exchangeResponse.ShouldNotBeNull();
        exchangeResponse.Success.ShouldBeTrue();
    }

    #endregion

    #region Error Handling E2E Tests

    [Test]
    public async Task ExchangeEndpoint_WithUnsupportedChain_ShouldReturn400()
    {
        // Arrange - Test with unsupported blockchain
        var dynamicJwt = JwtTestTokenFactory.CreateValidDynamicJwt(
            subject: TestDataFixtures.DynA_Subject,
            issuer: TestDataFixtures.DynamicIssuer);

        SetAuthorizationHeader(dynamicJwt);

        // Create request with Ethereum instead of Solana
        var ethereumAddress = "0x742d35Cc6634C0532925a3b8D2d25C23E44C4Ce8";
        var message = "Ethereum authentication";
        var signature = "0x1234567890abcdef"; // Invalid signature for testing

        using var requestPayload = CreateExchangeRequestWithCustomChain(
            "ethereum", ethereumAddress, message, signature);

        // Act
        var response = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);

        // Assert - Should fail with unsupported chain error
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content, JsonOptions);

        errorResponse.ShouldNotBeNull();
        errorResponse.Code.ShouldContain("UNSUPPORTED");
    }

    [Test]
    public async Task ExchangeEndpoint_WithInvalidSignatureEncoding_ShouldReturn400()
    {
        // Arrange - Test with invalid signature encoding
        var dynamicJwt = JwtTestTokenFactory.CreateValidDynamicJwt(
            subject: TestDataFixtures.DynA_Subject,
            issuer: TestDataFixtures.DynamicIssuer);

        SetAuthorizationHeader(dynamicJwt);

        using var key = Key.Create(SignatureAlgorithm.Ed25519);
        var walletAddress = GetSolanaAddressFromKey(key);
        var message = CreateChallengeMessage(walletAddress);

        // Use invalid signature encoding
        var invalidSignature = "invalid!@#$%^&*()signature_encoding";

        using var requestPayload = CreateExchangeRequestWithWalletSignature(
            walletAddress, message, invalidSignature);

        // Act
        var response = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);

        // Assert - Should fail with invalid signature error
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content, JsonOptions);

        errorResponse.ShouldNotBeNull();
        errorResponse.Code.ShouldContain("SIGNATURE");
    }

    #endregion

    #region Rate Limiting and Concurrent Request Tests

    [Test]
    public async Task ExchangeEndpoint_ConcurrentRequests_ShouldHandleGracefully()
    {
        // Arrange - Test concurrent signature verification requests
        const int concurrentRequests = 5;
        var tasks = new List<Task<HttpResponseMessage>>();

        var dynamicJwt = JwtTestTokenFactory.CreateValidDynamicJwt(
            subject: TestDataFixtures.DynA_Subject,
            issuer: TestDataFixtures.DynamicIssuer);

        for (int i = 0; i < concurrentRequests; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                using var client = Factory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new("Bearer", dynamicJwt);

                using var key = Key.Create(SignatureAlgorithm.Ed25519);
                var walletAddress = GetSolanaAddressFromKey(key);
                var message = CreateChallengeMessage(walletAddress);
                var signature = CreateSignatureForMessage(key, message);

                using var requestPayload = CreateExchangeRequestWithWalletSignature(
                    walletAddress, message, signature);

                return await client.PostAsync("/api/v1/auth/exchange", requestPayload);
            }));
        }

        // Act
        var responses = await Task.WhenAll(tasks);

        // Assert - All requests should succeed
        foreach (var response in responses)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.Dispose();
        }
    }

    #endregion

    #region Helper Methods

    private static string GetSolanaAddressFromKey(Key key)
    {
        var publicKeyBytes = key.PublicKey.Export(KeyBlobFormat.RawPublicKey);
        return Base58.Bitcoin.Encode(publicKeyBytes);
    }

    private static string CreateChallengeMessage(string walletAddress)
    {
        return $"{{\"wallet\":\"{walletAddress}\",\"action\":\"authenticate\",\"timestamp\":{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}}}";
    }

    private static string CreateSignatureForMessage(Key key, string message)
    {
        var messageBytes = Encoding.UTF8.GetBytes(message);
        var signatureBytes = SignatureAlgorithm.Ed25519.Sign(key, messageBytes);
        return Base58.Bitcoin.Encode(signatureBytes);
    }

    private static StringContent CreateExchangeRequestWithWalletSignature(
        string walletAddress, string message, string signature)
    {
        var requestData = new
        {
            userData = new
            {
                axonUserId = TestDataFixtures.DynA_Subject,
                dynamicEnvironmentId = "test-env-id",
                wallets = new[]
                {
                    new
                    {
                        address = walletAddress,
                        chain = "solana",
                        signature = signature,
                        message = message
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(requestData, JsonOptions);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static StringContent CreateExchangeRequestWithCustomChain(
        string chainId, string address, string message, string signature)
    {
        var requestData = new
        {
            userData = new
            {
                axonUserId = TestDataFixtures.DynA_Subject,
                dynamicEnvironmentId = "test-env-id",
                wallets = new[]
                {
                    new
                    {
                        address = address,
                        chain = chainId,
                        signature = signature,
                        message = message
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(requestData, JsonOptions);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    #endregion

    #region Response Models

    public class ExchangeResponse
    {
        public bool Success { get; set; }
        public string AxonUserId { get; set; } = string.Empty;
        public int WalletsProcessed { get; set; }
        public int WalletsLinked { get; set; }
    }

    public class ErrorResponse
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    #endregion
}