using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace Axon.Modules.Identity.Application.Commands;

using System.Diagnostics;
using Axon.Modules.Identity.Application.Commands.ExchangeCredential;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Infrastructure.Persistence;
using Axon.Modules.Identity.Infrastructure.Persistence.Repositories;
using Axon.Modules.Identity.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;
using Shouldly;
using Testcontainers.PostgreSql;

[TestFixture]
public class PrincipalResolutionIntegrationTests
{
    private PostgreSqlContainer _postgres = null!;
    private IdentityDbContext _context = null!;
    private IPrincipalResolutionService _resolutionService = null!;
    private IWalletReadRepository _walletReadRepository = null!;
    private IWalletWriteRepository _walletWriteRepository = null!;
    private IWalletOwnershipRepository _walletOwnershipRepository = null!;
    private IAxonPrincipalReadRepository _principalReadRepository = null!;
    private IAxonPrincipalWriteRepository _principalWriteRepository = null!;
    private IAddressNormalizationService _addressNormalization = null!;
    private ILogger<PrincipalResolutionService> _logger = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("axon_test")
            .WithUsername("test")
            .WithPassword("test")
            .Build();

        await _postgres.StartAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _postgres.DisposeAsync();
    }

    [SetUp]
    public async Task SetUp()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        _context = new IdentityDbContext(options);
        await _context.Database.EnsureCreatedAsync();

        await CreateIndexes();

        _walletReadRepository = new WalletReadRepository(_context);
        _walletWriteRepository = new WalletWriteRepository(_context);
        _walletOwnershipRepository = new WalletOwnershipRepository(_context);
        _principalReadRepository = new AxonPrincipalReadRepository(_context);
        _principalWriteRepository = new AxonPrincipalWriteRepository(_context);
        _addressNormalization = new AddressNormalizationService();
        _logger = Substitute.For<ILogger<PrincipalResolutionService>>();

        _resolutionService = new PrincipalResolutionService(
            _principalReadRepository,
            _principalWriteRepository,
            _walletReadRepository,
            _walletWriteRepository,
            _walletOwnershipRepository,
            _addressNormalization,
            _logger);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE identity.principal CASCADE");
        await _context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE identity.wallet CASCADE");
        await _context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE identity.wallet_ownership CASCADE");
        await _context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE identity.credential CASCADE");
        await _context.DisposeAsync();
    }

    private async Task CreateIndexes()
    {
        var sql = @"
            CREATE SCHEMA IF NOT EXISTS identity;

            CREATE TABLE IF NOT EXISTS identity.principal (
                id CHAR(26) PRIMARY KEY,
                type VARCHAR(50),
                risk_tier VARCHAR(50),
                created_at TIMESTAMPTZ,
                is_deleted BOOLEAN DEFAULT FALSE
            );

            CREATE TABLE IF NOT EXISTS identity.wallet (
                id CHAR(26) PRIMARY KEY,
                network_environment VARCHAR(50),
                chain_id VARCHAR(50),
                address VARCHAR(255),
                first_seen_at TIMESTAMPTZ,
                last_seen_at TIMESTAMPTZ,
                is_deleted BOOLEAN DEFAULT FALSE
            );

            CREATE TABLE IF NOT EXISTS identity.wallet_ownership (
                id CHAR(26) PRIMARY KEY,
                principal_id CHAR(26) REFERENCES identity.principal(id),
                wallet_id CHAR(26) REFERENCES identity.wallet(id),
                status VARCHAR(50),
                access_mode VARCHAR(50),
                verification_source VARCHAR(50),
                created_at TIMESTAMPTZ,
                is_deleted BOOLEAN DEFAULT FALSE
            );

            CREATE TABLE IF NOT EXISTS identity.credential (
                id CHAR(26) PRIMARY KEY,
                principal_id CHAR(26) REFERENCES identity.principal(id),
                provider VARCHAR(50),
                issuer VARCHAR(255),
                subject VARCHAR(255),
                last_seen_at TIMESTAMPTZ,
                is_deleted BOOLEAN DEFAULT FALSE
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ux_wallet_netenv_chain_addr
            ON identity.wallet(network_environment, chain_id, address)
            WHERE is_deleted = false;

            CREATE UNIQUE INDEX IF NOT EXISTS ux_credential_provider
            ON identity.credential(provider, issuer, subject)
            WHERE is_deleted = false;

            CREATE UNIQUE INDEX IF NOT EXISTS ux_ownership_pair
            ON identity.wallet_ownership(principal_id, wallet_id)
            WHERE is_deleted = false;

            CREATE UNIQUE INDEX IF NOT EXISTS ux_exclusive_signing
            ON identity.wallet_ownership(wallet_id)
            WHERE status = 'verified' AND access_mode = 'signing' AND is_deleted = false;

            CREATE INDEX IF NOT EXISTS idx_wallet_chain_addr_active
            ON identity.wallet(chain_id, address)
            WHERE is_deleted = false;";

        await _context.Database.ExecuteSqlRawAsync(sql);
    }

    [Test]
    public async Task ResolveAsync_Performance_ShouldCompleteUnder100ms_P95()
    {
        // Arrange - Create test data
        var networkEnv = NetworkEnvironment.Mainnet;
        var chainId = "solana";
        var address = new Address("EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v");

        // Pre-warm the database connection
        await _principalReadRepository.FindByIdAsync(AxonUserId.New(), CancellationToken.None);

        var timings = new List<long>();

        // Act - Measure performance across multiple runs
        for (int i = 0; i < 100; i++)
        {
            var sw = Stopwatch.StartNew();

            var result = await _resolutionService.ResolveAsync(
                ProviderType.Dynamic,
                "dynamic.xyz",
                $"user{i}",
                networkEnv,
                ChainId.Create(chainId).Value,
                address,
                CancellationToken.None);

            sw.Stop();
            timings.Add(sw.ElapsedMilliseconds);

            // Clean up created principal for next iteration
            if (result.IsSuccess && result.Value.Path == ResolutionPath.Created)
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM identity.wallet_ownership WHERE principal_id = {0}",
                    result.Value.Principal.Id.Value.ToString());
                await _context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM identity.principal WHERE id = {0}",
                    result.Value.Principal.Id.Value.ToString());
            }
        }

        // Assert - Calculate P95
        timings.Sort();
        var p95Index = (int)Math.Ceiling(timings.Count * 0.95) - 1;
        var p95Value = timings[p95Index];

        p95Value.ShouldBeLessThan(100, $"P95 resolution time was {p95Value}ms, expected <100ms");
    }

    [Test]
    public async Task ResolveAsync_TripleKeyLookup_ShouldUseIndex()
    {
        // Arrange
        var networkEnv = NetworkEnvironment.Mainnet;
        var chainId = "solana";
        var address = Address.Create("EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v").Value;

        // Create a wallet first
        var wallet = Wallet.Create(
            networkEnv,
            ChainId.Create(chainId).Value,
            address).Value;
        await _walletWriteRepository.AddAsync(wallet, CancellationToken.None);
        await _context.SaveChangesAsync();

        // Act - Get query plan
        var explainQuery = @"
            EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)
            SELECT w.id, w.network_environment, w.chain_id, w.address
            FROM identity.wallet w
            WHERE w.network_environment = 'mainnet'
              AND w.chain_id = 'solana'
              AND w.address = 'EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v'
              AND w.is_deleted = false";

        var result = await _context.Database
            .SqlQueryRaw<string>(explainQuery)
            .ToListAsync();

        // Assert - Verify index usage
        result.ShouldNotBeEmpty();
        var plan = result.First();
        plan.ShouldContain("Index Scan", Case.Insensitive);
        plan.ShouldContain("ux_wallet_netenv_chain_addr", Case.Insensitive);
    }

    [Test]
    public async Task ResolveAsync_CrossEnvironmentConflict_ShouldReturn409()
    {
        // Arrange - Create principal A with credential
        var principalA = new AxonPrincipal(
            new AxonId(Ulid.NewUlid()),
            PrincipalType.Human,
            RiskTier.Low);

        var credential = new IdentityCredential(
            new CredentialId(Ulid.NewUlid()),
            principalA.Id,
            ProviderType.Dynamic,
            "dynamic.xyz",
            "user123");
        principalA.AddCredential(credential);

        await _principalWriteRepository.AddAsync(principalA, CancellationToken.None);
        await _context.SaveChangesAsync();

        // Create principal B with verified+signing ownership on devnet
        var principalB = new AxonPrincipal(
            new AxonId(Ulid.NewUlid()),
            PrincipalType.Human,
            RiskTier.Low);
        await _principalWriteRepository.AddAsync(principalB, CancellationToken.None);

        var address = new Address("EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v");
        var devnetWallet = new Wallet(
            new WalletId(Ulid.NewUlid()),
            NetworkEnvironment.Devnet,
            "solana",
            address);
        await _walletWriteRepository.AddAsync(devnetWallet, CancellationToken.None);

        var ownership = new WalletOwnership(
            new OwnershipId(Ulid.NewUlid()),
            principalB.Id,
            devnetWallet.Id,
            OwnershipStatus.Verified,
            AccessMode.Signing,
            VerificationSource.DirectSignatureMsg);
        await _walletOwnershipRepository.AddAsync(ownership, CancellationToken.None);
        await _context.SaveChangesAsync();

        // Act - Try to resolve on mainnet with principal A's credential
        var result = await _resolutionService.ResolveAsync(
            ProviderType.Dynamic,
            "dynamic.xyz",
            "user123",
            NetworkEnvironment.Mainnet,
            ChainId.Create("solana").Value,
            address,
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Message.ShouldContain("Cross-environment verified+signing conflict");
    }

    [Test]
    public async Task ResolveAsync_ConcurrentCreation_ShouldHandleRaceCondition()
    {
        // Arrange
        var networkEnv = NetworkEnvironment.Mainnet;
        var chainId = "solana";
        var address = new Address("EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v");

        // Act - Run concurrent resolutions
        var tasks = new List<Task<Result<PrincipalResolutionResult, Error>>>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(_resolutionService.ResolveAsync(
                ProviderType.Dynamic,
                "dynamic.xyz",
                $"concurrent{i}",
                networkEnv,
                ChainId.Create(chainId).Value,
                address,
                CancellationToken.None));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - Only one should create, others should resolve to same principal
        var createdCount = results.Count(r => r.IsSuccess && r.Value.Path == ResolutionPath.Created);
        var principalIds = results
            .Where(r => r.IsSuccess)
            .Select(r => r.Value.Principal.Id)
            .Distinct()
            .ToList();

        createdCount.ShouldBeLessThanOrEqualTo(1, "Only one principal should be created");
        principalIds.Count.ShouldBe(1, "All resolutions should return the same principal");
    }

    [Test]
    public async Task ResolveAsync_CrossEnvironmentAutoLink_WhenPrincipalsMatch()
    {
        // Arrange - Create principal with credential and devnet wallet
        var principal = new AxonPrincipal(
            new AxonId(Ulid.NewUlid()),
            PrincipalType.Human,
            RiskTier.Low);

        var credential = new IdentityCredential(
            new CredentialId(Ulid.NewUlid()),
            principal.Id,
            ProviderType.Dynamic,
            "dynamic.xyz",
            "user123");
        principal.AddCredential(credential);

        await _principalWriteRepository.AddAsync(principal, CancellationToken.None);

        var address = new Address("EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v");
        var devnetWallet = new Wallet(
            new WalletId(Ulid.NewUlid()),
            NetworkEnvironment.Devnet,
            "solana",
            address);
        await _walletWriteRepository.AddAsync(devnetWallet, CancellationToken.None);

        var ownership = new WalletOwnership(
            new OwnershipId(Ulid.NewUlid()),
            principal.Id,
            devnetWallet.Id,
            OwnershipStatus.Verified,
            AccessMode.Signing,
            VerificationSource.DirectSignatureMsg);
        await _walletOwnershipRepository.AddAsync(ownership, CancellationToken.None);
        await _context.SaveChangesAsync();

        // Act - Resolve on mainnet with same credential
        var result = await _resolutionService.ResolveAsync(
            ProviderType.Dynamic,
            "dynamic.xyz",
            "user123",
            NetworkEnvironment.Mainnet,
            ChainId.Create("solana").Value,
            address,
            CancellationToken.None);

        // Assert - Should auto-link and create mainnet wallet
        result.IsSuccess.ShouldBeTrue();
        result.Value.Principal.Id.ShouldBe(principal.Id);
        result.Value.WasAutoLinked.ShouldBeTrue();

        // Verify mainnet wallet was created
        var mainnetWallet = await _walletReadRepository.FindWalletAsync(
            NetworkEnvironment.Mainnet,
            "solana",
            address,
            CancellationToken.None);
        mainnetWallet.ShouldNotBeNull();
    }
}