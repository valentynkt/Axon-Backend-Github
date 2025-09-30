using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Application.Common.Models;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Infrastructure.Tests.Persistence;

/// <summary>
/// Fluent builder for creating consistent test data across Identity persistence tests.
/// Provides reasonable defaults with ability to override specific properties.
/// </summary>
public class IdentityTestDataBuilder
{
    private readonly Random _random = new(42); // Deterministic seed for consistent test runs

    #region Principal Builder

    public PrincipalBuilder Principal() => new(this);

    public class PrincipalBuilder
    {
        private readonly IdentityTestDataBuilder _parent;
        private AxonUserId? _id;
        private string? _issuer;
        private string? _subject;
        private ProviderType? _providerType;
        private readonly List<WalletOwnership> _ownerships = new();
        private readonly List<PrincipalChainDefault> _chainDefaults = new();

        internal PrincipalBuilder(IdentityTestDataBuilder parent)
        {
            _parent = parent;
        }

        public PrincipalBuilder WithId(AxonUserId id)
        {
            _id = id;
            return this;
        }

        public PrincipalBuilder WithDynamicCredential(string? issuer = null, string? subject = null)
        {
            _issuer = issuer ?? $"https://app.dynamic.xyz/test-{_parent._random.Next(1000, 9999)}";
            _subject = subject ?? $"test-user-{Guid.NewGuid():N}";
            _providerType = ProviderType.Create("dynamic").Value;
            return this;
        }

        public PrincipalBuilder WithCustomCredential(string provider, string issuer, string subject)
        {
            _providerType = ProviderType.Create(provider).Value;
            _issuer = issuer;
            _subject = subject;
            return this;
        }

        public PrincipalBuilder WithWalletOwnership(WalletId walletId, AccessMode accessMode = AccessMode.Signing, OwnershipStatus status = OwnershipStatus.Verified)
        {
            var principalId = _id ?? AxonUserId.New();
            var ownership = WalletOwnership.Create(principalId, walletId, accessMode, status);
            _ownerships.Add(ownership);
            return this;
        }

        public PrincipalBuilder WithChainDefault(string chainId, WalletId walletId)
        {
            var principalId = _id ?? AxonUserId.New();
            var chainDefault = PrincipalChainDefault.Create(principalId, chainId, walletId);
            _chainDefaults.Add(chainDefault);
            return this;
        }

        public PrincipalBuilder WithMultiChainDefaults(params (string chainId, WalletId walletId)[] defaults)
        {
            ArgumentNullException.ThrowIfNull(defaults);

            foreach (var (chainId, walletId) in defaults)
            {
                WithChainDefault(chainId, walletId);
            }
            return this;
        }

        public AxonPrincipal Build()
        {
            // Apply defaults if not set
            var providerType = _providerType ?? ProviderType.Create("dynamic").Value;
            var issuer = _issuer ?? $"https://app.dynamic.xyz/test-{_parent._random.Next(1000, 9999)}";
            var subject = _subject ?? $"test-user-{Guid.NewGuid():N}";

            var principal = AxonPrincipal.CreateWithDynamicCredential(
                providerType,
                issuer,
                subject,
                _id).Value;

            // Add ownerships
            foreach (var ownership in _ownerships)
            {
                var linkResult = principal.LinkWalletOwnership(ownership, (_, _, _) => Result.Success<bool, Error>(false), TimeProvider.System);
                if (linkResult.IsFailure)
                {
                    throw new InvalidOperationException($"Failed to link wallet ownership: {linkResult.Error}");
                }
            }

            // Add chain defaults
            if (_chainDefaults.Count > 0)
            {
                var chainDefaultsArray = _chainDefaults.Select(cd => (cd.ChainId, cd.WalletId)).ToArray();
                var chainDefaultsResult = principal.ApplyChainDefaultsBatch(chainDefaultsArray, TimeProvider.System);
                if (chainDefaultsResult.IsFailure)
                {
                    throw new InvalidOperationException($"Failed to apply chain defaults: {chainDefaultsResult.Error}");
                }
            }

            return principal;
        }
    }

    #endregion

    #region Wallet Builder

    public WalletBuilder Wallet() => new(this);

    public class WalletBuilder
    {
        private readonly IdentityTestDataBuilder _parent;
        private WalletId? _id;
        private string? _chainId;
        private string? _address;

        internal WalletBuilder(IdentityTestDataBuilder parent)
        {
            _parent = parent;
        }

        public WalletBuilder WithId(WalletId id)
        {
            _id = id;
            return this;
        }

        public WalletBuilder OnChain(string chainId)
        {
            _chainId = chainId;
            return this;
        }

        public WalletBuilder WithAddress(string address)
        {
            _address = address;
            return this;
        }

        public WalletBuilder WithRandomAddress()
        {
            _address = $"0x{_parent._random.Next():X8}{_parent._random.Next():X8}{_parent._random.Next():X8}{_parent._random.Next():X8}";
            return this;
        }

        public WalletBuilder OnEthereum() => OnChain("ethereum");
        public WalletBuilder OnPolygon() => OnChain("polygon");
        public WalletBuilder OnBsc() => OnChain("binance");
        public WalletBuilder OnArbitrum() => OnChain("arbitrum");

        public Domain.Aggregates.Wallet.Wallet Build()
        {
            _chainId ??= "ethereum"; // Default to Ethereum
            _address ??= $"0x{_parent._random.Next():X8}{_parent._random.Next():X8}{_parent._random.Next():X8}{_parent._random.Next():X8}";

            var address = Address.Create(_address).Value;
            return Domain.Aggregates.Wallet.Wallet.Create(_id, _chainId, address);
        }
    }

    #endregion

    #region Ownership Builder

    public static OwnershipBuilder Ownership() => new();

    public class OwnershipBuilder
    {
        private AxonUserId? _principalId;
        private WalletId? _walletId;
        private AccessMode _accessMode = AccessMode.Signing;
        private OwnershipStatus _status = OwnershipStatus.Verified;

        public OwnershipBuilder ForPrincipal(AxonUserId principalId)
        {
            _principalId = principalId;
            return this;
        }

        public OwnershipBuilder ForWallet(WalletId walletId)
        {
            _walletId = walletId;
            return this;
        }

        public OwnershipBuilder WithSigning() => WithAccessMode(AccessMode.Signing);
        public OwnershipBuilder WithWatchOnly() => WithAccessMode(AccessMode.WatchOnly);

        public OwnershipBuilder WithAccessMode(AccessMode accessMode)
        {
            _accessMode = accessMode;
            return this;
        }

        public OwnershipBuilder WithStatus(OwnershipStatus status)
        {
            _status = status;
            return this;
        }

        public OwnershipBuilder Verified() => WithStatus(OwnershipStatus.Verified);
        public OwnershipBuilder Pending() => WithStatus(OwnershipStatus.Pending);
        public OwnershipBuilder Revoked() => WithStatus(OwnershipStatus.Revoked);

        public WalletOwnership Build()
        {
            if (_principalId == null)
                throw new InvalidOperationException("Principal ID is required");
            if (_walletId == null)
                throw new InvalidOperationException("Wallet ID is required");

            return WalletOwnership.Create(_principalId.Value, _walletId.Value, _accessMode, _status);
        }
    }

    #endregion

    #region Chain Default Builder

    public static ChainDefaultBuilder ChainDefault() => new();

    public class ChainDefaultBuilder
    {
        private AxonUserId? _principalId;
        private string? _chainId;
        private WalletId? _walletId;

        public ChainDefaultBuilder ForPrincipal(AxonUserId principalId)
        {
            _principalId = principalId;
            return this;
        }

        public ChainDefaultBuilder OnChain(string chainId)
        {
            _chainId = chainId;
            return this;
        }

        public ChainDefaultBuilder WithWallet(WalletId walletId)
        {
            _walletId = walletId;
            return this;
        }

        public ChainDefaultBuilder OnEthereum() => OnChain("ethereum");
        public ChainDefaultBuilder OnPolygon() => OnChain("polygon");
        public ChainDefaultBuilder OnBsc() => OnChain("binance");

        public PrincipalChainDefault Build()
        {
            if (_principalId == null)
                throw new InvalidOperationException("Principal ID is required");
            if (_chainId == null)
                throw new InvalidOperationException("Chain ID is required");
            if (_walletId == null)
                throw new InvalidOperationException("Wallet ID is required");

            return PrincipalChainDefault.Create(_principalId.Value, _chainId, _walletId.Value);
        }
    }

    #endregion

    #region Scenario Builders

    /// <summary>
    /// Creates a complete test scenario with principal and multiple wallets with ownerships.
    /// </summary>
    public CompleteScenarioBuilder CompleteScenario() => new(this);

    public class CompleteScenarioBuilder
    {
        private readonly IdentityTestDataBuilder _parent;
        private readonly List<(Domain.Aggregates.Wallet.Wallet wallet, AccessMode accessMode, OwnershipStatus status)> _wallets = new();
        private readonly List<(string chainId, int walletIndex)> _chainDefaults = new();
        private string? _issuer;
        private string? _subject;

        internal CompleteScenarioBuilder(IdentityTestDataBuilder parent)
        {
            _parent = parent;
        }

        public CompleteScenarioBuilder WithDynamicCredential(string? issuer = null, string? subject = null)
        {
            _issuer = issuer;
            _subject = subject;
            return this;
        }

        public CompleteScenarioBuilder WithEthereumWallet(AccessMode accessMode = AccessMode.Signing, OwnershipStatus status = OwnershipStatus.Verified)
        {
            var wallet = _parent.Wallet().OnEthereum().Build();
            _wallets.Add((wallet, accessMode, status));
            return this;
        }

        public CompleteScenarioBuilder WithPolygonWallet(AccessMode accessMode = AccessMode.Signing, OwnershipStatus status = OwnershipStatus.Verified)
        {
            var wallet = _parent.Wallet().OnPolygon().Build();
            _wallets.Add((wallet, accessMode, status));
            return this;
        }

        public CompleteScenarioBuilder WithBscWallet(AccessMode accessMode = AccessMode.Signing, OwnershipStatus status = OwnershipStatus.Verified)
        {
            var wallet = _parent.Wallet().OnBsc().Build();
            _wallets.Add((wallet, accessMode, status));
            return this;
        }

        public CompleteScenarioBuilder WithChainDefault(string chainId, int walletIndex)
        {
            _chainDefaults.Add((chainId, walletIndex));
            return this;
        }

        public CompleteScenarioBuilder WithMultiChainDefaults()
        {
            // Set chain defaults for all wallets based on their chain
            for (int i = 0; i < _wallets.Count; i++)
            {
                var wallet = _wallets[i].wallet;
                _chainDefaults.Add((wallet.ChainId, i));
            }
            return this;
        }

        public (AxonPrincipal principal, Domain.Aggregates.Wallet.Wallet[] wallets) Build()
        {
            if (_wallets.Count == 0)
            {
                // Default multi-chain setup
                WithEthereumWallet().WithPolygonWallet().WithBscWallet(AccessMode.WatchOnly);
            }

            var wallets = _wallets.Select(w => w.wallet).ToArray();
            var principalBuilder = _parent.Principal()
                .WithDynamicCredential(_issuer, _subject);

            // Add ownerships
            foreach (var (wallet, accessMode, status) in _wallets)
            {
                principalBuilder.WithWalletOwnership(wallet.Id, accessMode, status);
            }

            // Add chain defaults
            foreach (var (chainId, walletIndex) in _chainDefaults)
            {
                if (walletIndex < wallets.Length)
                {
                    principalBuilder.WithChainDefault(chainId, wallets[walletIndex].Id);
                }
            }

            var principal = principalBuilder.Build();
            return (principal, wallets);
        }
    }

    #endregion

    #region Predefined Test Data

    /// <summary>
    /// Creates common test addresses for consistent testing.
    /// </summary>
    public static class CommonAddresses
    {
        public static readonly string ZeroAddress = "0x0000000000000000000000000000000000000000";
        public static readonly string TestAddress1 = "0x1111111111111111111111111111111111111111";
        public static readonly string TestAddress2 = "0x2222222222222222222222222222222222222222";
        public static readonly string TestAddress3 = "0x3333333333333333333333333333333333333333";
        public static readonly string VitalikAddress = "0xd8dA6BF26964aF9D7eEd9e03E53415D37aA96045";
        public static readonly string InvalidAddress = "invalid-address";
        public static readonly string MixedCaseAddress = "0xAbCdEf1234567890AbCdEf1234567890AbCdEf12";
    }

    /// <summary>
    /// Creates common test chain IDs for consistent testing.
    /// </summary>
    public static class CommonChains
    {
        public static readonly string Ethereum = "ethereum";
        public static readonly string Polygon = "polygon";
        public static readonly string Bsc = "binance";
        public static readonly string Arbitrum = "arbitrum";
        public static readonly string Optimism = "optimism";
        public static readonly string Avalanche = "avalanche";
        public static readonly string InvalidChain = "";
        public static readonly string VeryLongChain = new string('x', 1000);
    }

    /// <summary>
    /// Creates common test provider configurations.
    /// </summary>
    public static class CommonProviders
    {
        public static readonly (string provider, string issuer, string subject) Dynamic =
            ("dynamic", "https://app.dynamic.xyz", "dyn_user_123");

        public static readonly (string provider, string issuer, string subject) Auth0 =
            ("auth0", "https://dev-auth0.auth0.com/", "auth0|user123");

        public static readonly (string provider, string issuer, string subject) Firebase =
            ("firebase", "https://securetoken.google.com/project", "firebase_user_123");
    }

    #endregion
}