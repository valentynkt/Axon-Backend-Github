using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Domain.Tests.TestData;

/// <summary>
/// Advanced fluent test data builders for Identity Domain entities.
/// Provides realistic test scenarios and complex object construction patterns.
/// </summary>
public static class AdvancedTestDataBuilders
{
    #region AxonPrincipal Builders

    /// <summary>
    /// Fluent builder for creating AxonPrincipal aggregates with complex scenarios.
    /// </summary>
    public class AxonPrincipalBuilder
    {
        private AxonUserId? _id;
        private PrincipalType _type = PrincipalType.Human;
        private RiskTier _riskTier = RiskTier.Low;
        private readonly List<IdentityCredential> _credentials = new();
        private readonly List<WalletOwnership> _ownerships = new();
        private readonly List<PrincipalChainDefault> _chainDefaults = new();

        public static AxonPrincipalBuilder Create() => new();

        public AxonPrincipalBuilder WithId(AxonUserId id)
        {
            _id = id;
            return this;
        }

        public AxonPrincipalBuilder AsHuman()
        {
            _type = PrincipalType.Human;
            return this;
        }

        public AxonPrincipalBuilder AsService()
        {
            _type = PrincipalType.Service;
            return this;
        }

        public AxonPrincipalBuilder WithRiskTier(RiskTier riskTier)
        {
            _riskTier = riskTier;
            return this;
        }

        public AxonPrincipalBuilder WithDynamicCredential(string issuer = "app.dynamicauth.com/test-env", string subject = "test-user-123")
        {
            var principalId = _id ?? AxonUserId.New();
            var credential = IdentityCredential.Create(principalId, "dynamic", issuer, subject);
            _credentials.Add(credential);
            return this;
        }

        public AxonPrincipalBuilder WithGitHubCredential(string username = "testuser")
        {
            var principalId = _id ?? AxonUserId.New();
            var credential = IdentityCredential.Create(principalId, "github", "github.com", username);
            _credentials.Add(credential);
            return this;
        }

        public AxonPrincipalBuilder WithCustomCredential(string provider, string issuer, string subject)
        {
            var principalId = _id ?? AxonUserId.New();
            var credential = IdentityCredential.Create(principalId, provider, issuer, subject);
            _credentials.Add(credential);
            return this;
        }

        public AxonPrincipalBuilder WithVerifiedEthereumWallet()
        {
            var principalId = _id ?? AxonUserId.New();
            var walletId = WalletId.New();
            var ownership = WalletOwnership.Create(principalId, walletId, AccessMode.Signing, OwnershipStatus.Verified);
            _ownerships.Add(ownership);
            return this;
        }

        public AxonPrincipalBuilder WithPendingSolanaWallet()
        {
            var principalId = _id ?? AxonUserId.New();
            var walletId = WalletId.New();
            var ownership = WalletOwnership.Create(principalId, walletId, AccessMode.Signing, OwnershipStatus.Pending);
            _ownerships.Add(ownership);
            return this;
        }

        public AxonPrincipalBuilder WithWatchOnlyWallet(WalletId? walletId = null)
        {
            var principalId = _id ?? AxonUserId.New();
            var effectiveWalletId = walletId ?? WalletId.New();
            var ownership = WalletOwnership.Create(principalId, effectiveWalletId, AccessMode.WatchOnly, OwnershipStatus.Verified);
            _ownerships.Add(ownership);
            return this;
        }

        public AxonPrincipalBuilder WithCustomOwnership(WalletId walletId, AccessMode accessMode, OwnershipStatus status)
        {
            var principalId = _id ?? AxonUserId.New();
            var ownership = WalletOwnership.Create(principalId, walletId, accessMode, status);
            _ownerships.Add(ownership);
            return this;
        }

        public AxonPrincipalBuilder WithChainDefault(string chainId, WalletId? walletId = null)
        {
            var principalId = _id ?? AxonUserId.New();
            var effectiveWalletId = walletId ?? _ownerships.FirstOrDefault()?.WalletId ?? WalletId.New();
            var chainDefault = PrincipalChainDefault.Create(principalId, chainId, effectiveWalletId);
            _chainDefaults.Add(chainDefault);
            return this;
        }

        public AxonPrincipalBuilder WithMultiChainDefaults(params string[] chainIds)
        {
            foreach (var chainId in chainIds)
            {
                WithChainDefault(chainId);
            }
            return this;
        }

        public AxonPrincipal Build()
        {
            var effectiveId = _id ?? AxonUserId.New();

            var principal = _type switch
            {
                PrincipalType.Human => AxonPrincipal.CreateHuman(effectiveId),
                PrincipalType.Service => AxonPrincipal.CreateService(effectiveId),
                _ => throw new ArgumentOutOfRangeException()
            };

            // Update risk tier if different from default
            if (_riskTier != RiskTier.Low)
            {
                principal.UpdateRiskTier(_riskTier);
            }

            // Add credentials using reflection (since they're normally added through domain commands)
            var credentialsField = typeof(AxonPrincipal)
                .GetField("_credentials", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var credentials = (List<IdentityCredential>)credentialsField!.GetValue(principal)!;
            foreach (var credential in _credentials)
            {
                credentials.Add(credential);
            }

            // Add ownerships using reflection
            var ownershipsField = typeof(AxonPrincipal)
                .GetField("_walletOwnerships", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var ownerships = (List<WalletOwnership>)ownershipsField!.GetValue(principal)!;
            foreach (var ownership in _ownerships)
            {
                ownerships.Add(ownership);
            }

            // Add chain defaults using reflection
            var chainDefaultsField = typeof(AxonPrincipal)
                .GetField("_principalChainDefaults", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var chainDefaults = (List<PrincipalChainDefault>)chainDefaultsField!.GetValue(principal)!;
            foreach (var chainDefault in _chainDefaults)
            {
                chainDefaults.Add(chainDefault);
            }

            return principal;
        }
    }

    #endregion

    #region Wallet Builders

    /// <summary>
    /// Fluent builder for creating Wallet aggregates with various configurations.
    /// </summary>
    public class WalletBuilder
    {
        private WalletId? _id;
        private string _chainId = TestConstants.EthereumChain;
        private Address? _address;
        private DateTime? _timestamp;

        public static WalletBuilder Create() => new();

        public WalletBuilder WithId(WalletId id)
        {
            _id = id;
            return this;
        }

        public WalletBuilder OnEthereum(Address? address = null)
        {
            _chainId = TestConstants.EthereumChain;
            _address = address ?? Builders.EthereumAddress;
            return this;
        }

        public WalletBuilder OnSolana(Address? address = null)
        {
            _chainId = TestConstants.SolanaChain;
            _address = address ?? Builders.SolanaAddress;
            return this;
        }

        public WalletBuilder OnPolygon(Address? address = null)
        {
            _chainId = "polygon-mainnet";
            _address = address ?? Address.From("0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e45");
            return this;
        }

        public WalletBuilder OnChain(string chainId, Address address)
        {
            _chainId = chainId;
            _address = address;
            return this;
        }

        public WalletBuilder WithAddress(Address address)
        {
            _address = address;
            return this;
        }

        public WalletBuilder WithTimestamp(DateTime timestamp)
        {
            _timestamp = timestamp;
            return this;
        }

        public WalletBuilder SeenDaysAgo(int days)
        {
            _timestamp = DateTime.UtcNow.AddDays(-days);
            return this;
        }

        public Wallet Build()
        {
            var effectiveAddress = _address ?? (_chainId.Contains("solana", StringComparison.OrdinalIgnoreCase) ? Builders.SolanaAddress : Builders.EthereumAddress);
            return Wallet.Create(_id, _chainId, effectiveAddress, _timestamp);
        }
    }

    #endregion

    #region Scenario Builders

    /// <summary>
    /// Builds complete test scenarios with multiple related entities.
    /// </summary>
    public static class ScenarioBuilder
    {
        /// <summary>
        /// Creates a new user scenario with fresh Dynamic credential and pending wallet.
        /// </summary>
        public static AxonPrincipal NewUserWithDynamicAndPendingWallet(string? userId = null)
        {
            var effectiveUserId = userId ?? $"user-{Guid.NewGuid():N}";
            return AxonPrincipalBuilder.Create()
                .AsHuman()
                .WithDynamicCredential("dynamic:prod", effectiveUserId)
                .WithPendingSolanaWallet()
                .Build();
        }

        /// <summary>
        /// Creates an experienced user with multiple verified wallets across chains.
        /// </summary>
        public static AxonPrincipal ExperiencedUserWithMultiChainWallets()
        {
            return AxonPrincipalBuilder.Create()
                .AsHuman()
                .WithRiskTier(RiskTier.Medium)
                .WithDynamicCredential()
                .WithGitHubCredential()
                .WithVerifiedEthereumWallet()
                .WithVerifiedEthereumWallet() // Second Ethereum wallet
                .WithCustomOwnership(WalletId.New(), AccessMode.Signing, OwnershipStatus.Verified) // Solana wallet
                .WithChainDefault("ethereum-mainnet")
                .WithChainDefault("solana-mainnet")
                .Build();
        }

        /// <summary>
        /// Creates a high-risk user with complex ownership patterns.
        /// </summary>
        public static AxonPrincipal HighRiskUserWithComplexOwnerships()
        {
            var sharedWalletId = WalletId.New();
            return AxonPrincipalBuilder.Create()
                .AsHuman()
                .WithRiskTier(RiskTier.High)
                .WithDynamicCredential()
                .WithVerifiedEthereumWallet()
                .WithWatchOnlyWallet(sharedWalletId)
                .WithCustomOwnership(WalletId.New(), AccessMode.Signing, OwnershipStatus.Revoked)
                .WithChainDefault("ethereum-mainnet")
                .Build();
        }

        /// <summary>
        /// Creates a service principal with minimal setup.
        /// </summary>
        public static AxonPrincipal ServicePrincipalWithApiKey()
        {
            return AxonPrincipalBuilder.Create()
                .AsService()
                .WithCustomCredential("api_key", "internal", "service-123")
                .Build();
        }

        /// <summary>
        /// Creates a pair of principals sharing a wallet (conflict scenario).
        /// </summary>
        public static (AxonPrincipal First, AxonPrincipal Second, WalletId SharedWallet) ConflictingPrincipalsWithSharedWallet()
        {
            var sharedWalletId = WalletId.New();

            var first = AxonPrincipalBuilder.Create()
                .AsHuman()
                .WithDynamicCredential("dynamic:prod", "user-1")
                .WithCustomOwnership(sharedWalletId, AccessMode.Signing, OwnershipStatus.Verified)
                .Build();

            var second = AxonPrincipalBuilder.Create()
                .AsHuman()
                .WithDynamicCredential("dynamic:prod", "user-2")
                .WithCustomOwnership(sharedWalletId, AccessMode.WatchOnly, OwnershipStatus.Verified)
                .Build();

            return (first, second, sharedWalletId);
        }

        /// <summary>
        /// Creates a collection of wallets for bulk testing scenarios.
        /// </summary>
        public static List<Wallet> MultiChainWalletCollection(int count = 10)
        {
            var wallets = new List<Wallet>();
            var chains = new[] { "ethereum-mainnet", "solana-mainnet", "polygon-mainnet", "arbitrum-one" };

            for (int i = 0; i < count; i++)
            {
                var chain = chains[i % chains.Length];
                var builder = WalletBuilder.Create().OnChain(chain, GenerateAddressForChain(chain, i));

                if (i % 3 == 0) builder.SeenDaysAgo(i); // Vary last seen times

                wallets.Add(builder.Build());
            }

            return wallets;
        }

        /// <summary>
        /// Creates a realistic user journey from signup to experienced user.
        /// </summary>
        public static List<AxonPrincipal> UserJourneyProgression()
        {
            var baseUserId = $"user-journey-{Guid.NewGuid():N}";

            return new List<AxonPrincipal>
            {
                // Day 1: Signup with Dynamic
                AxonPrincipalBuilder.Create()
                    .AsHuman()
                    .WithDynamicCredential("dynamic:prod", baseUserId)
                    .Build(),

                // Day 2: Connected first wallet (pending)
                AxonPrincipalBuilder.Create()
                    .AsHuman()
                    .WithDynamicCredential("dynamic:prod", baseUserId)
                    .WithPendingSolanaWallet()
                    .Build(),

                // Day 5: Verified wallet and added Ethereum
                AxonPrincipalBuilder.Create()
                    .AsHuman()
                    .WithDynamicCredential("dynamic:prod", baseUserId)
                    .WithCustomOwnership(WalletId.New(), AccessMode.Signing, OwnershipStatus.Verified)
                    .WithVerifiedEthereumWallet()
                    .WithChainDefault("solana-mainnet")
                    .Build(),

                // Day 30: Power user with multiple credentials and wallets
                AxonPrincipalBuilder.Create()
                    .AsHuman()
                    .WithRiskTier(RiskTier.Medium)
                    .WithDynamicCredential("dynamic:prod", baseUserId)
                    .WithGitHubCredential($"{baseUserId}-github")
                    .WithVerifiedEthereumWallet()
                    .WithCustomOwnership(WalletId.New(), AccessMode.Signing, OwnershipStatus.Verified)
                    .WithWatchOnlyWallet()
                    .WithMultiChainDefaults("ethereum-mainnet", "solana-mainnet")
                    .Build()
            };
        }

        private static Address GenerateAddressForChain(string chainId, int index)
        {
            return chainId.ToLowerInvariant() switch
            {
                var c when c.Contains("solana", StringComparison.OrdinalIgnoreCase) => Address.From($"9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWW{index:D1}"),
                _ => Address.From($"0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e{index:D2}")
            };
        }
    }

    #endregion
}

/// <summary>
/// Enhanced assertion helpers for complex domain scenarios.
/// </summary>
public static class AssertionHelpers
{
        /// <summary>
        /// Asserts that a principal represents a realistic new user.
        /// </summary>
        public static void ShouldBeNewUser(this AxonPrincipal principal)
        {
            principal.Type.ShouldBe(PrincipalType.Human);
            principal.RiskTier.ShouldBe(RiskTier.Low);
            principal.Credentials.ShouldNotBeEmpty();
            principal.WalletOwnerships.Count.ShouldBeLessThanOrEqualTo(2); // At most 1-2 wallets for new users
        }

        /// <summary>
        /// Asserts that a principal represents an experienced user.
        /// </summary>
        public static void ShouldBeExperiencedUser(this AxonPrincipal principal)
        {
            principal.Type.ShouldBe(PrincipalType.Human);
            principal.Credentials.Count.ShouldBeGreaterThanOrEqualTo(2); // Multiple auth methods
            principal.WalletOwnerships.Count.ShouldBeGreaterThanOrEqualTo(3); // Multiple wallets
            principal.PrincipalChainDefaults.ShouldNotBeEmpty(); // Has preferences
        }

        /// <summary>
        /// Asserts that a principal has secure wallet ownership patterns.
        /// </summary>
        public static void ShouldHaveSecureWalletPatterns(this AxonPrincipal principal)
        {
            var verifiedSigningWallets = principal.WalletOwnerships
                .Where(o => o.Status == OwnershipStatus.Verified && o.AccessMode == AccessMode.Signing)
                .ToList();

            verifiedSigningWallets.ShouldNotBeEmpty();

            // Each chain should have at most one verified signing wallet
            var chainGroups = verifiedSigningWallets.GroupBy(o => o.WalletId);
            chainGroups.ShouldAllBe(g => g.Count() == 1);
        }

        /// <summary>
        /// Asserts that two principals don't have conflicting wallet ownerships.
        /// </summary>
        public static void ShouldNotHaveConflictingOwnerships(this AxonPrincipal principal1, AxonPrincipal principal2)
        {
            var principal1VerifiedSigning = principal1.WalletOwnerships
                .Where(o => o.IsVerifiedSigning)
                .Select(o => o.WalletId)
                .ToHashSet();

            var principal2VerifiedSigning = principal2.WalletOwnerships
                .Where(o => o.IsVerifiedSigning)
                .Select(o => o.WalletId)
                .ToHashSet();

            principal1VerifiedSigning.Intersect(principal2VerifiedSigning).ShouldBeEmpty();
        }

        /// <summary>
        /// Asserts that a wallet collection represents a realistic distribution.
        /// </summary>
        public static void ShouldHaveRealisticDistribution(this IEnumerable<Wallet> wallets)
        {
            var walletList = wallets.ToList();
            walletList.ShouldNotBeEmpty();

            // Should have multiple chains represented
            var chainIds = walletList.Select(w => w.ChainId).Distinct().ToList();
            chainIds.Count.ShouldBeGreaterThan(1);

            // Should have varied timestamps
            var timestamps = walletList.Select(w => w.FirstSeenAt).Distinct().ToList();
            if (walletList.Count > 1)
            {
                timestamps.Count.ShouldBeGreaterThan(1);
            }
        }
}