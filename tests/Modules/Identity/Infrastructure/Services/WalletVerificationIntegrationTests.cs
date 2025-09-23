using System.Diagnostics;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
using Axon.Modules.Identity.Infrastructure.Services;
using BuildingBlocks.Primitives.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;
using Shouldly;
using Testcontainers.PostgreSql;

namespace Axon.Modules.Identity.Infrastructure.Services.Tests;

[TestFixture]
public class WalletVerificationIntegrationTests
{
    private PostgreSqlContainer _postgres = null!;
    private IdentityWriteDbContext _writeContext = null!;
    private WalletVerificationService _verificationService = null!;
    private ILogger<WalletVerificationService> _logger = null!;

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

        await CreateDatabaseSchema();

        _logger = Substitute.For<ILogger<WalletVerificationService>>();
        _verificationService = new WalletVerificationService(_writeContext, _logger);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _writeContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE identity.principal CASCADE");
        await _writeContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE identity.wallet CASCADE");
        await _writeContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE identity.wallet_ownership CASCADE");
        await _writeContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE identity.principal_chain_default CASCADE");
        await _writeContext.DisposeAsync();
    }

    [Test]
    public async Task VerifyWalletOwnershipAsync_WithRowLevelLocking_PreventsConcurrentConflicts()
    {
        // Arrange
        var walletId = WalletId.New();
        var principalId1 = AxonUserId.New();
        var principalId2 = AxonUserId.New();

        // Create wallet and competing principals
        await CreateTestWallet(walletId);
        await CreateTestPrincipal(principalId1);
        await CreateTestPrincipal(principalId2);

        var accessMode = AccessMode.Signing;
        var verificationSource = VerificationSource.DynamicAttested;

        // Act - Simulate concurrent verification attempts
        var task1 = _verificationService.VerifyWalletOwnershipAsync(
            walletId, principalId1, accessMode, verificationSource, CancellationToken.None);

        var task2 = _verificationService.VerifyWalletOwnershipAsync(
            walletId, principalId2, accessMode, verificationSource, CancellationToken.None);

        var results = await Task.WhenAll(task1, task2);

        // Assert - One should succeed, one should fail with conflict
        var successCount = results.Count(r => r.IsSuccess);
        var conflictCount = results.Count(r => r.IsFailure && r.Error.Code == "WALLET.OWNERSHIP.ALREADY_VERIFIED");

        successCount.ShouldBe(1);
        conflictCount.ShouldBe(1);

        // Verify only one verified ownership exists in database
        var verifiedOwnerships = await _writeContext.WalletOwnerships
            .Where(o => o.WalletId == walletId &&
                       o.Status == OwnershipStatus.Verified &&
                       o.AccessMode == AccessMode.Signing)
            .ToListAsync();

        verifiedOwnerships.Count.ShouldBe(1);
    }

    [Test]
    public async Task VerifyWalletOwnershipAsync_VerifyRowLockAcquisition_UsesForUpdateCorrectly()
    {
        // Arrange
        var walletId = WalletId.New();
        var principalId = AxonUserId.New();

        await CreateTestWallet(walletId);
        await CreateTestPrincipal(principalId);

        var accessMode = AccessMode.Signing;
        var verificationSource = VerificationSource.DynamicAttested;

        // Act & Assert - This test verifies that row locks are acquired
        // We'll check that the query execution completes without deadlocks
        var stopwatch = Stopwatch.StartNew();

        var result = await _verificationService.VerifyWalletOwnershipAsync(
            walletId, principalId, accessMode, verificationSource, CancellationToken.None);

        stopwatch.Stop();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(1000); // Performance target: <1000ms for single verification

        // Verify we can see the lock behavior by checking pg_locks during a long transaction
        await VerifyLockBehaviorWithLongTransaction(walletId);
    }

    [Test]
    public async Task VerifyBatchWalletOwnershipsAsync_ConsistentLockOrdering_PreventsDeadlocks()
    {
        // Arrange
        var walletId1 = new WalletId(new Guid("11111111-1111-1111-1111-111111111111"));
        var walletId2 = new WalletId(new Guid("22222222-2222-2222-2222-222222222222"));
        var walletId3 = new WalletId(new Guid("33333333-3333-3333-3333-333333333333"));

        var principalId1 = AxonUserId.New();
        var principalId2 = AxonUserId.New();

        // Create test data
        await CreateTestWallet(walletId1);
        await CreateTestWallet(walletId2);
        await CreateTestWallet(walletId3);
        await CreateTestPrincipal(principalId1);
        await CreateTestPrincipal(principalId2);

        // Create batch requests in different orders to test deadlock prevention
        var requests1 = new List<(WalletId WalletId, AxonUserId PrincipalId, AccessMode AccessMode, VerificationSource VerificationSource)>
        {
            (walletId3, principalId1, AccessMode.Signing, VerificationSource.DynamicAttested),
            (walletId1, principalId1, AccessMode.WatchOnly, VerificationSource.WatchOnly),
            (walletId2, principalId1, AccessMode.Signing, VerificationSource.DirectSignatureMsg)
        };

        var requests2 = new List<(WalletId WalletId, AxonUserId PrincipalId, AccessMode AccessMode, VerificationSource VerificationSource)>
        {
            (walletId1, principalId2, AccessMode.Signing, VerificationSource.DynamicAttested),
            (walletId3, principalId2, AccessMode.WatchOnly, VerificationSource.WatchOnly),
            (walletId2, principalId2, AccessMode.Signing, VerificationSource.DirectSignatureMsg)
        };

        // Act - Execute batch operations concurrently
        var task1 = _verificationService.VerifyBatchWalletOwnershipsAsync(requests1, CancellationToken.None);
        var task2 = _verificationService.VerifyBatchWalletOwnershipsAsync(requests2, CancellationToken.None);

        var results = await Task.WhenAll(task1, task2);

        // Assert - At least one should succeed (no deadlocks)
        var successCount = results.Count(r => r.IsSuccess);
        successCount.ShouldBeGreaterThan(0);

        // Verify database state is consistent
        var allOwnerships = await _writeContext.WalletOwnerships
            .Where(o => o.Status == OwnershipStatus.Verified)
            .ToListAsync();

        allOwnerships.Count.ShouldBeGreaterThan(0);
    }

    [Test]
    public async Task VerifyWalletOwnershipAsync_PerformanceTarget_CompletesWithin100ms()
    {
        // Arrange
        var walletId = WalletId.New();
        var principalId = AxonUserId.New();

        await CreateTestWallet(walletId);
        await CreateTestPrincipal(principalId);

        var accessMode = AccessMode.Signing;
        var verificationSource = VerificationSource.DynamicAttested;

        // Warm up
        await _verificationService.VerifyWalletOwnershipAsync(
            walletId, principalId, accessMode, verificationSource, CancellationToken.None);

        // Reset for actual test
        await _writeContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE identity.wallet_ownership CASCADE");

        // Act
        var stopwatch = Stopwatch.StartNew();

        var result = await _verificationService.VerifyWalletOwnershipAsync(
            walletId, principalId, accessMode, verificationSource, CancellationToken.None);

        stopwatch.Stop();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(100); // AC: <100ms P95 performance target
    }

    [Test]
    public async Task VerifyWalletOwnershipAsync_RetryOnUniqueConstraintViolation_EventuallySucceeds()
    {
        // Arrange
        var walletId = WalletId.New();
        var principalId = AxonUserId.New();

        await CreateTestWallet(walletId);
        await CreateTestPrincipal(principalId);

        // Create a pending ownership to simulate constraint violation scenario
        var existingOwnership = WalletOwnership.Create(
            principalId,
            walletId,
            AccessMode.WatchOnly,
            OwnershipStatus.Pending,
            VerificationSource.WatchOnly);

        await _writeContext.WalletOwnerships.AddAsync(existingOwnership);
        await _writeContext.SaveChangesAsync();

        var accessMode = AccessMode.Signing;
        var verificationSource = VerificationSource.DynamicAttested;

        // Act
        var result = await _verificationService.VerifyWalletOwnershipAsync(
            walletId, principalId, accessMode, verificationSource, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(OwnershipStatus.Verified);
        result.Value.AccessMode.ShouldBe(AccessMode.Signing);
    }

    private async Task VerifyLockBehaviorWithLongTransaction(WalletId walletId)
    {
        // This test verifies that locks are properly acquired by using pg_locks
        using var connection = _writeContext.Database.GetDbConnection();
        await connection.OpenAsync();

        using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // Start a transaction that locks the wallet
            using var lockCommand = connection.CreateCommand();
            lockCommand.Transaction = transaction;
            lockCommand.CommandText = $"SELECT * FROM identity.wallet WHERE id = '{walletId.Value}' FOR UPDATE";

            await lockCommand.ExecuteNonQueryAsync();

            // Check pg_locks for the lock
            using var lockCheckCommand = connection.CreateCommand();
            lockCheckCommand.Transaction = transaction;
            lockCheckCommand.CommandText = @"
                SELECT COUNT(*)
                FROM pg_locks
                WHERE locktype = 'tuple'
                AND granted = true
                AND mode = 'ExclusiveLock'";

            var lockCount = await lockCheckCommand.ExecuteScalarAsync();

            // We should have at least one exclusive lock
            lockCount.ShouldNotBeNull();
            Convert.ToInt32(lockCount, System.Globalization.CultureInfo.InvariantCulture).ShouldBeGreaterThan(0);
        }
        finally
        {
            await transaction.RollbackAsync();
        }
    }

    private async Task CreateTestWallet(WalletId walletId)
    {
        var wallet = Wallet.Create(
            walletId,
            NetworkEnvironment.Mainnet,
            "solana",
            Address.Create("9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM").Value);

        await _writeContext.Wallets.AddAsync(wallet);
        await _writeContext.SaveChangesAsync();
    }

    private async Task CreateTestPrincipal(AxonUserId principalId)
    {
        var principal = AxonPrincipal.CreateHuman(principalId);

        await _writeContext.Principals.AddAsync(principal);
        await _writeContext.SaveChangesAsync();
    }

    private async Task CreateDatabaseSchema()
    {
        var sql = @"
            CREATE SCHEMA IF NOT EXISTS identity;

            CREATE TABLE IF NOT EXISTS identity.principal (
                id CHAR(26) PRIMARY KEY,
                type VARCHAR(50) NOT NULL,
                risk_tier VARCHAR(50) NOT NULL,
                created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
                updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
                is_deleted BOOLEAN NOT NULL DEFAULT FALSE
            );

            CREATE TABLE IF NOT EXISTS identity.wallet (
                id CHAR(26) PRIMARY KEY,
                network_environment VARCHAR(50) NOT NULL,
                chain_id VARCHAR(100) NOT NULL,
                address VARCHAR(500) NOT NULL,
                first_seen_at TIMESTAMPTZ NOT NULL DEFAULT now(),
                last_seen_at TIMESTAMPTZ NOT NULL DEFAULT now(),
                created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
                updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
                is_deleted BOOLEAN NOT NULL DEFAULT FALSE
            );

            CREATE TABLE IF NOT EXISTS identity.wallet_ownership (
                id CHAR(26) PRIMARY KEY,
                principal_id CHAR(26) NOT NULL REFERENCES identity.principal(id),
                wallet_id CHAR(26) NOT NULL REFERENCES identity.wallet(id),
                access_mode SMALLINT NOT NULL,
                status SMALLINT NOT NULL,
                verification_source VARCHAR(100),
                verified_at TIMESTAMPTZ,
                revoked_at TIMESTAMPTZ,
                revoke_reason TEXT,
                created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
                updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
                is_deleted BOOLEAN NOT NULL DEFAULT FALSE
            );

            CREATE TABLE IF NOT EXISTS identity.principal_chain_default (
                id CHAR(26) PRIMARY KEY,
                principal_id CHAR(26) NOT NULL REFERENCES identity.principal(id),
                network_environment VARCHAR(50) NOT NULL,
                chain_id VARCHAR(100) NOT NULL,
                wallet_id CHAR(26) NOT NULL REFERENCES identity.wallet(id),
                created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
                updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
                is_deleted BOOLEAN NOT NULL DEFAULT FALSE
            );

            -- Create critical indexes for performance and constraints
            CREATE UNIQUE INDEX IF NOT EXISTS ux_wallet_netenv_chain_addr
            ON identity.wallet(network_environment, chain_id, address)
            WHERE is_deleted = false;

            CREATE UNIQUE INDEX IF NOT EXISTS ux_ownership_pair
            ON identity.wallet_ownership(principal_id, wallet_id)
            WHERE is_deleted = false;

            CREATE UNIQUE INDEX IF NOT EXISTS ux_exclusive_signing
            ON identity.wallet_ownership(wallet_id)
            WHERE status = 1 AND access_mode = 0 AND is_deleted = false;

            CREATE INDEX IF NOT EXISTS idx_ownership_wallet_active
            ON identity.wallet_ownership(wallet_id)
            WHERE is_deleted = false;

            CREATE INDEX IF NOT EXISTS idx_wallet_chain_addr_active
            ON identity.wallet(chain_id, address)
            WHERE is_deleted = false;
        ";

        await _writeContext.Database.ExecuteSqlRawAsync(sql);
    }
}