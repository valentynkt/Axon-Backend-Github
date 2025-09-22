using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using BuildingBlocks.Application;
using Axon.Modules.Identity.Application.Common.Models;

namespace Axon.Modules.Identity.Application.Commands;

using System.Diagnostics;
using Axon.Modules.Identity.Application.Commands.ExchangeCredential;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Infrastructure.Persistence;
using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
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
    private IdentityWriteDbContext _writeContext = null!;
    private IdentityReadDbContext _readContext = null!;
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
        var options = new DbContextOptionsBuilder<IdentityWriteDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        _writeContext = new IdentityWriteDbContext(options);
        await _writeContext.Database.EnsureCreatedAsync();

        await CreateIndexes();

        var mockUnitOfWork = Substitute.For<IWriteUnitOfWork<IdentityModule>>();
        var addressLogger = Substitute.For<ILogger<AddressNormalizationService>>();

        _walletReadRepository = new WalletReadRepository(_readContext);
        _walletWriteRepository = new WalletWriteRepository(_writeContext, mockUnitOfWork);
        _walletOwnershipRepository = new WalletOwnershipRepository(_writeContext, _readContext, mockUnitOfWork);
        _principalReadRepository = new AxonPrincipalReadRepository(_readContext);
        _principalWriteRepository = new AxonPrincipalWriteRepository(_writeContext, mockUnitOfWork);
        _addressNormalization = new AddressNormalizationService(addressLogger);
        _logger = Substitute.For<ILogger<PrincipalResolutionService>>();

        _resolutionService = new PrincipalResolutionService(
            _principalReadRepository,
            _principalWriteRepository,
            _walletReadRepository,
            _walletWriteRepository,
            _walletOwnershipRepository,
            _logger);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _writeContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE identity.principal CASCADE");
        await _writeContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE identity.wallet CASCADE");
        await _writeContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE identity.wallet_ownership CASCADE");
        await _writeContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE identity.credential CASCADE");
        await _writeContext.DisposeAsync();
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

        await _writeContext.Database.ExecuteSqlRawAsync(sql);
    }

    [Test]
    public async Task ResolveAsync_Performance_ShouldCompleteUnder100ms_P95()
    {
        // Arrange - Create test data
        var networkEnv = NetworkEnvironment.Mainnet;
        var chainId = "solana";
        var address = Address.From("EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v");

        // Pre-warm the database connection
        await _principalReadRepository.GetByIdWithActiveOwnershipsAsync(AxonUserId.New(), CancellationToken.None);

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
                ChainId.From(chainId),
                address,
                CancellationToken.None);

            sw.Stop();
            timings.Add(sw.ElapsedMilliseconds);

            // Clean up created principal for next iteration
            if (result.IsSuccess && result.Value.Path == ResolutionPath.Created)
            {
                await _writeContext.Database.ExecuteSqlRawAsync(
                    "DELETE FROM identity.wallet_ownership WHERE principal_id = {0}",
                    result.Value.Principal.Id.Value.ToString());
                await _writeContext.Database.ExecuteSqlRawAsync(
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
        var address = Address.From("EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v");

        // Create a wallet first
        var wallet = Wallet.Create(
            null,
            networkEnv,
            chainId,
            address);
        await _walletWriteRepository.AddAsync(wallet, CancellationToken.None);
        await _writeContext.SaveChangesAsync();

        // Act - Get query plan
        var explainQuery = @"
            EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)
            SELECT w.id, w.network_environment, w.chain_id, w.address
            FROM identity.wallet w
            WHERE w.network_environment = 'mainnet'
              AND w.chain_id = 'solana'
              AND w.address = 'EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v'
              AND w.is_deleted = false";

        var result = await _readContext.Database
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
        var principalA = AxonPrincipal.CreateHuman(AxonUserId.New());

        var credential = IdentityCredential.Create(
            principalA.Id,
            ProviderType.Dynamic.Value,
            "dynamic.xyz",
            "user123");
        var addCredentialResult = principalA.AddCredential(credential, (provider, issuer, subject) => Result.Success<bool, Error>(false));
        addCredentialResult.IsSuccess.ShouldBeTrue();

        await _principalWriteRepository.AddAsync(principalA, CancellationToken.None);
        await _writeContext.SaveChangesAsync();

        // Create principal B with verified+signing ownership on devnet
        var principalB = AxonPrincipal.CreateHuman(AxonUserId.New());
        await _principalWriteRepository.AddAsync(principalB, CancellationToken.None);

        var address = Address.From("EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v");
        var devnetWallet = Wallet.Create(
            WalletId.New(),
            NetworkEnvironment.Devnet,
            "solana",
            address);
        await _walletWriteRepository.AddAsync(devnetWallet, CancellationToken.None);

        var ownership = await _walletOwnershipRepository.CreateOwnershipAsync(
            principalB.Id,
            devnetWallet.Id,
            AccessMode.Signing,
            OwnershipStatus.Verified,
            VerificationSource.DirectSignatureMsg,
            CancellationToken.None);
        await _writeContext.SaveChangesAsync();

        // Act - Try to resolve on mainnet with principal A's credential
        var result = await _resolutionService.ResolveAsync(
            ProviderType.Dynamic,
            "dynamic.xyz",
            "user123",
            NetworkEnvironment.Mainnet,
            ChainId.From("solana"),
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
        var address = Address.From("EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v");

        // Act - Run concurrent resolutions
        var tasks = new List<Task<Result<PrincipalResolutionResult, Error>>>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(_resolutionService.ResolveAsync(
                ProviderType.Dynamic,
                "dynamic.xyz",
                $"concurrent{i}",
                networkEnv,
                ChainId.From(chainId),
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
        var principal = AxonPrincipal.CreateHuman(AxonUserId.New());

        var credential = IdentityCredential.Create(
            principal.Id,
            ProviderType.Dynamic.Value,
            "dynamic.xyz",
            "user123");
        var addCredentialResult3 = principal.AddCredential(credential, (provider, issuer, subject) => Result.Success<bool, Error>(false));
        addCredentialResult3.IsSuccess.ShouldBeTrue();

        await _principalWriteRepository.AddAsync(principal, CancellationToken.None);

        var address = Address.From("EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v");
        var devnetWallet = Wallet.Create(
            WalletId.New(),
            NetworkEnvironment.Devnet,
            "solana",
            address);
        await _walletWriteRepository.AddAsync(devnetWallet, CancellationToken.None);

        var ownership = await _walletOwnershipRepository.CreateOwnershipAsync(
            principal.Id,
            devnetWallet.Id,
            AccessMode.Signing,
            OwnershipStatus.Verified,
            VerificationSource.DirectSignatureMsg,
            CancellationToken.None);
        await _writeContext.SaveChangesAsync();

        // Act - Resolve on mainnet with same credential
        var result = await _resolutionService.ResolveAsync(
            ProviderType.Dynamic,
            "dynamic.xyz",
            "user123",
            NetworkEnvironment.Mainnet,
            ChainId.From("solana"),
            address,
            CancellationToken.None);

        // Assert - Should auto-link and create mainnet wallet
        result.IsSuccess.ShouldBeTrue();
        result.Value.Principal.Id.ShouldBe(principal.Id);
        result.Value.WasAutoLinked.ShouldBeTrue();

        // Verify mainnet wallet was created
        var mainnetWallet = await _walletReadRepository.FindWalletAsync(
            NetworkEnvironment.Mainnet,
            ChainId.From("solana"),
            address,
            CancellationToken.None);
        mainnetWallet.ShouldNotBeNull();
    }
}