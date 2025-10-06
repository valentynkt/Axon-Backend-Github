using System.Net;
using System.Text;
using System.Text.Json;
using Axon.Api.Contracts.V1.Auth;
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
[NonParallelizable] // CRITICAL: Prevent parallel execution to avoid test isolation issues with shared state
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
        // Arrange - Create a valid Ed25519 signature for a test wallet
        using var key = Key.Create(SignatureAlgorithm.Ed25519);
        var walletAddress = GetSolanaAddressFromKey(key);

        // Create Dynamic JWT with wallet data embedded in verified_credentials
        var dynamicJwt = JwtTestTokenFactory.CreateValidDynamicJwtWithWallets(
            subject: TestDataFixtures.DynA_Subject,
            issuer: TestDataFixtures.DynamicIssuer,
            wallets: new List<(string address, string chain, string? walletName, string? provider)>
            {
                (walletAddress, "solana-mainnet", "Phantom", "phantom")
            });

        SetAuthorizationHeader(dynamicJwt);

        // Exchange endpoint accepts empty body - wallet data comes from JWT claims
        using var requestPayload = new StringContent("{}", Encoding.UTF8, "application/json");

        // Act - Send exchange request
        var response = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);

        // Assert - Should succeed with wallet linking
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var exchangeResponse = JsonSerializer.Deserialize<AuthTokenResponseDto>(content, JsonOptions);

        exchangeResponse.ShouldNotBeNull();
        exchangeResponse.AxonUserId.ShouldNotBeNullOrEmpty();
        exchangeResponse.WalletsLinked.ShouldBeGreaterThan(0);
        exchangeResponse.AccessToken.ShouldNotBeNullOrEmpty();
    }

    [Test]
    public async Task ExchangeEndpoint_WithNoWallets_ShouldSucceedButLinkZeroWallets()
    {
        // Arrange - Create Dynamic JWT without wallet credentials
        // This simulates a user who authenticated with email/social but has no wallets
        var dynamicJwt = JwtTestTokenFactory.CreateValidDynamicJwt(
            subject: TestDataFixtures.DynA_Subject,
            issuer: TestDataFixtures.DynamicIssuer);

        SetAuthorizationHeader(dynamicJwt);

        using var requestPayload = new StringContent("{}", Encoding.UTF8, "application/json");

        // Act
        var response = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);

        // Assert - Should succeed but with 0 wallets linked
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var exchangeResponse = JsonSerializer.Deserialize<AuthTokenResponseDto>(content, JsonOptions);

        exchangeResponse.ShouldNotBeNull();
        exchangeResponse.AxonUserId.ShouldNotBeNullOrEmpty();
        exchangeResponse.WalletsLinked.ShouldBe(0); // No wallets to link
        exchangeResponse.AccessToken.ShouldNotBeNullOrEmpty();
    }

    [Test]
    public async Task ExchangeEndpoint_WithMultipleWallets_ShouldLinkAll()
    {
        // Arrange - Create multiple wallet addresses
        using var key1 = Key.Create(SignatureAlgorithm.Ed25519);
        using var key2 = Key.Create(SignatureAlgorithm.Ed25519);
        var wallet1 = GetSolanaAddressFromKey(key1);
        var wallet2 = GetSolanaAddressFromKey(key2);

        var dynamicJwt = JwtTestTokenFactory.CreateValidDynamicJwtWithWallets(
            subject: TestDataFixtures.DynA_Subject,
            issuer: TestDataFixtures.DynamicIssuer,
            wallets: new List<(string address, string chain, string? walletName, string? provider)>
            {
                (wallet1, "solana-mainnet", "Phantom", "phantom"),
                (wallet2, "solana-mainnet", "Phantom", "phantom")
            });

        SetAuthorizationHeader(dynamicJwt);

        using var requestPayload = new StringContent("{}", Encoding.UTF8, "application/json");

        // Act
        var response = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);

        // Assert - Should link both wallets
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var exchangeResponse = JsonSerializer.Deserialize<AuthTokenResponseDto>(content, JsonOptions);

        exchangeResponse.ShouldNotBeNull();
        exchangeResponse.WalletsLinked.ShouldBe(2);
        exchangeResponse.AxonUserId.ShouldNotBeNullOrEmpty();
    }

    // NOTE: The following tests have been removed because they test Dynamic's signature verification,
    // which happens before Axon receives the JWT. Axon receives pre-verified wallet credentials
    // from Dynamic in the JWT's verified_credentials claim. Signature verification is Dynamic's responsibility.
    //
    // Removed tests:
    // - ExchangeEndpoint_WithBase64UrlEncodedSignature_ShouldSucceed
    // - ExchangeEndpoint_MessageFormattingDifference_ShouldFail
    // - ExchangeEndpoint_UnicodeInMessage_ShouldSucceed
    // - ExchangeEndpoint_WithInvalidSignatureEncoding_ShouldReturn400

    #endregion

    #region Error Handling E2E Tests

    [Test]
    public async Task ExchangeEndpoint_WithUnsupportedChain_ShouldStillSucceed()
    {
        // Arrange - Axon accepts any chain from Dynamic's verified credentials
        // Chain validation is handled by application logic, not at exchange level
        using var key = Key.Create(SignatureAlgorithm.Ed25519);
        var walletAddress = GetSolanaAddressFromKey(key);

        var dynamicJwt = JwtTestTokenFactory.CreateValidDynamicJwtWithWallets(
            subject: TestDataFixtures.DynA_Subject,
            issuer: TestDataFixtures.DynamicIssuer,
            wallets: new List<(string address, string chain, string? walletName, string? provider)>
            {
                (walletAddress, "ethereum-mainnet", "MetaMask", "metamask") // Different chain
            });

        SetAuthorizationHeader(dynamicJwt);

        using var requestPayload = new StringContent("{}", Encoding.UTF8, "application/json");

        // Act
        var response = await HttpClient.PostAsync("/api/v1/auth/exchange", requestPayload);

        // Assert - Should succeed; chain validation happens elsewhere
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var exchangeResponse = JsonSerializer.Deserialize<AuthTokenResponseDto>(content, JsonOptions);

        exchangeResponse.ShouldNotBeNull();
        exchangeResponse.WalletsLinked.ShouldBeGreaterThan(0);
    }

    #endregion

    #region Rate Limiting and Concurrent Request Tests

    [Test]
    public async Task ExchangeEndpoint_ConcurrentRequests_ShouldHandleGracefully()
    {
        // Arrange - Test concurrent exchange requests
        const int concurrentRequests = 5;
        var tasks = new List<Task<HttpResponseMessage>>();

        for (int i = 0; i < concurrentRequests; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                using var client = Factory.CreateClient();

                using var key = Key.Create(SignatureAlgorithm.Ed25519);
                var walletAddress = GetSolanaAddressFromKey(key);

                var dynamicJwt = JwtTestTokenFactory.CreateValidDynamicJwtWithWallets(
                    subject: $"{TestDataFixtures.DynA_Subject}_{i}", // Different subject per request
                    issuer: TestDataFixtures.DynamicIssuer,
                    wallets: new List<(string address, string chain, string? walletName, string? provider)>
                    {
                        (walletAddress, "solana-mainnet", "Phantom", "phantom")
                    });

                client.DefaultRequestHeaders.Authorization = new("Bearer", dynamicJwt);

                using var requestPayload = new StringContent("{}", Encoding.UTF8, "application/json");

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

    #endregion
}