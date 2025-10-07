using Axon.Modules.Identity.Application.Contracts.ExternalServices;
using Axon.Modules.Identity.Application.Contracts.Providers;
using Axon.Modules.Identity.Application.DTOs.Exchange;
using Axon.Modules.Identity.Domain.Entities;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Application.Tests.Providers._TestInfrastructure;

/// <summary>
/// Test data builders for authentication-related tests.
/// Provides fluent builders for creating test data with sensible defaults.
/// </summary>
public static class AuthenticationTestFixtures
{
    #region DynamicExchangeRequest Builders

    public static DynamicExchangeRequest ValidDynamicExchangeRequest(string? token = null)
    {
        return new DynamicExchangeRequest(
            Token: token ?? "valid-jwt-token-mock");
    }

    public static DynamicExchangeRequest InvalidDynamicExchangeRequest()
    {
        return new DynamicExchangeRequest(
            Token: "invalid-jwt-token");
    }

    #endregion

    #region DynamicUserData Builders

    public static DynamicUserData ValidDynamicUserData(
        string? userId = null,
        string? email = null,
        string? environmentId = null,
        List<WalletData>? wallets = null,
        bool isNewUser = false)
    {
        return new DynamicUserData(
            AxonUserId: userId ?? Guid.NewGuid().ToString(),
            Email: email ?? "test@example.com",
            EnvironmentId: environmentId ?? "test-env-123",
            Wallets: wallets ?? new List<WalletData>(),
            FirstVisitUtc: DateTimeOffset.UtcNow.AddDays(-30),
            LastVisitUtc: DateTimeOffset.UtcNow,
            IsNewUser: isNewUser);
    }

    public static DynamicUserData DynamicUserDataWithSingleWallet(
        string? userId = null,
        string? walletAddress = null,
        string? chain = null)
    {
        var wallet = SingleWalletData(walletAddress, chain);
        return ValidDynamicUserData(
            userId: userId,
            wallets: new List<WalletData> { wallet });
    }

    public static DynamicUserData DynamicUserDataWithMultipleWallets(
        string? userId = null,
        int walletCount = 3)
    {
        var wallets = Enumerable.Range(1, walletCount)
            .Select(i => SingleWalletData(
                address: $"0x{i:D40}",
                chain: i % 2 == 0 ? "evm-1" : "solana-mainnet"))
            .ToList();

        return ValidDynamicUserData(
            userId: userId,
            wallets: wallets);
    }

    #endregion

    #region WalletData Builders

    public static WalletData SingleWalletData(
        string? address = null,
        string? chain = null,
        string? walletName = null,
        string? provider = null)
    {
        return new WalletData(
            Id: Guid.NewGuid().ToString(),
            Address: address ?? "0x1234567890123456789012345678901234567890",
            Chain: chain ?? "evm-1",
            WalletName: walletName ?? "MetaMask",
            Provider: provider ?? "metamask",
            ConnectedAtUtc: DateTimeOffset.UtcNow);
    }

    public static WalletData EthereumWalletData(string? address = null)
    {
        return SingleWalletData(
            address: address ?? "0x1234567890123456789012345678901234567890",
            chain: "ethereum",
            walletName: "MetaMask",
            provider: "metamask");
    }

    public static WalletData SolanaWalletData(string? address = null)
    {
        return SingleWalletData(
            address: address ?? "DYw8jCTfwHNRJhhmFcbXvVDTqWMEVFBX6ZKUmG5CNSKK",
            chain: "solana-mainnet",
            walletName: "Phantom",
            provider: "phantom");
    }

    public static WalletData InvalidChainWalletData()
    {
        return SingleWalletData(
            address: "0x1234567890123456789012345678901234567890",
            chain: "unsupported-chain-999",
            walletName: "Unknown",
            provider: "unknown");
    }

    public static WalletData InvalidAddressWalletData()
    {
        return SingleWalletData(
            address: "invalid-address-format",
            chain: "evm-1",
            walletName: "Invalid",
            provider: "test");
    }

    #endregion

    #region ExchangeWalletData Builders

    public static ExchangeWalletData ValidExchangeWalletData(
        string? address = null,
        string? chain = null)
    {
        return new ExchangeWalletData(
            Address: address ?? "0x1234567890123456789012345678901234567890",
            Chain: chain ?? "evm-1",
            WalletName: "MetaMask",
            Provider: "metamask",
            ConnectedAtUtc: DateTimeOffset.UtcNow);
    }

    public static List<ExchangeWalletData> MultipleExchangeWallets(int count = 3)
    {
        return Enumerable.Range(1, count)
            .Select(i => ValidExchangeWalletData(
                address: $"0x{i:D40}",
                chain: i % 2 == 0 ? "evm-1" : "solana-mainnet"))
            .ToList();
    }

    #endregion

    #region ExchangeUserData Builders

    public static ExchangeUserData ValidExchangeUserData(
        string? userId = null,
        string? email = null,
        List<ExchangeWalletData>? wallets = null,
        bool isNewUser = false)
    {
        return new ExchangeUserData(
            AxonUserId: userId ?? Guid.NewGuid().ToString(),
            Email: email ?? "test@example.com",
            DynamicEnvironmentId: "test-env-123",
            Wallets: wallets ?? new List<ExchangeWalletData>(),
            FirstVisitUtc: DateTimeOffset.UtcNow.AddDays(-30),
            LastVisitUtc: DateTimeOffset.UtcNow,
            IsNewUser: isNewUser);
    }

    public static ExchangeUserData ExchangeUserDataWithWallets(
        string? userId = null,
        int walletCount = 2)
    {
        return ValidExchangeUserData(
            userId: userId,
            wallets: MultipleExchangeWallets(walletCount));
    }

    #endregion

    #region AuthenticationResponse Builders

    public static AuthenticationResponse SuccessfulAuthenticationResponse(
        Guid? userId = null,
        string? providerType = null,
        Dictionary<string, object>? additionalData = null)
    {
        return new AuthenticationResponse(
            AccessToken: "mock-access-token-" + Guid.NewGuid(),
            RefreshToken: "mock-refresh-token-" + Guid.NewGuid(),
            UserId: userId ?? Guid.NewGuid(),
            ProviderType: providerType ?? "dynamic",
            ExpiresAt: DateTime.UtcNow.AddMinutes(30),
            AdditionalData: additionalData ?? new Dictionary<string, object>
            {
                ["created"] = false,
                ["wallets_processed"] = 1,
                ["wallets_linked"] = 1,
                ["defaults_applied"] = 0,
                ["skipped"] = 0,
                ["conflicts"] = 0
            });
    }

    public static AuthenticationResponse NewPrincipalAuthenticationResponse(Guid? userId = null)
    {
        return new AuthenticationResponse(
            AccessToken: "mock-access-token-" + Guid.NewGuid(),
            RefreshToken: "mock-refresh-token-" + Guid.NewGuid(),
            UserId: userId ?? Guid.NewGuid(),
            ProviderType: "dynamic",
            ExpiresAt: DateTime.UtcNow.AddMinutes(30),
            AdditionalData: new Dictionary<string, object>
            {
                ["created"] = true,
                ["wallets_processed"] = 2,
                ["wallets_linked"] = 2,
                ["defaults_applied"] = 1,
                ["skipped"] = 0,
                ["conflicts"] = 0
            });
    }

    #endregion

    #region AxonUserAuth Builders

    public static AxonUserAuth CreateTestUserAuth(
        AxonUserId? principalId = null,
        string? dynamicUserId = null,
        string? email = null,
        string? environmentId = null)
    {
        var user = AxonUserAuth.Create(
            principalId: principalId ?? new AxonUserId(Guid.NewGuid()),
            providerType: "dynamic",
            issuer: "https://app.dynamic.xyz",
            subject: dynamicUserId ?? Guid.NewGuid().ToString(),
            dynamicEnvironmentId: environmentId ?? "test-env-123",
            dynamicUserId: dynamicUserId ?? Guid.NewGuid().ToString());

        if (!string.IsNullOrEmpty(email))
        {
            user.Email = email;
            user.NormalizedEmail = email.ToUpperInvariant();
        }

        return user;
    }

    #endregion

    #region Test Scenario Builders

    /// <summary>
    /// Creates a complete test scenario for new user registration with wallets
    /// </summary>
    public static (DynamicUserData userData, List<ExchangeWalletData> exchangeWallets) NewUserWithWalletsScenario()
    {
        var userId = Guid.NewGuid().ToString();
        var wallets = new List<WalletData>
        {
            EthereumWalletData("0x1111111111111111111111111111111111111111"),
            SolanaWalletData("DYw8jCTfwHNRJhhmFcbXvVDTqWMEVFBX6ZKUmG5CNSKK")
        };

        var userData = ValidDynamicUserData(
            userId: userId,
            email: "newuser@example.com",
            wallets: wallets,
            isNewUser: true);

        var exchangeWallets = new List<ExchangeWalletData>
        {
            ValidExchangeWalletData("0x1111111111111111111111111111111111111111", "evm-1"),
            ValidExchangeWalletData("DYw8jCTfwHNRJhhmFcbXvVDTqWMEVFBX6ZKUmG5CNSKK", "solana-mainnet")
        };

        return (userData, exchangeWallets);
    }

    /// <summary>
    /// Creates a complete test scenario for existing user login
    /// </summary>
    public static (DynamicUserData userData, AxonUserAuth existingUser) ExistingUserScenario()
    {
        var userId = Guid.NewGuid().ToString();
        var principalId = new AxonUserId(Guid.NewGuid());

        var userData = ValidDynamicUserData(
            userId: userId,
            email: "existing@example.com",
            isNewUser: false);

        var existingUser = CreateTestUserAuth(
            principalId: principalId,
            dynamicUserId: userId,
            email: "existing@example.com");

        return (userData, existingUser);
    }

    /// <summary>
    /// Creates a test scenario for wallet normalization with mixed valid/invalid wallets
    /// </summary>
    public static List<WalletData> MixedValidityWalletsScenario()
    {
        return new List<WalletData>
        {
            EthereumWalletData("0x1111111111111111111111111111111111111111"), // Valid
            InvalidAddressWalletData(), // Invalid address
            SolanaWalletData("DYw8jCTfwHNRJhhmFcbXvVDTqWMEVFBX6ZKUmG5CNSKK"), // Valid
            InvalidChainWalletData() // Invalid chain
        };
    }

    #endregion
}