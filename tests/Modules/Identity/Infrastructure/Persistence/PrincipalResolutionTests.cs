using System.Diagnostics;
using Axon.Modules.Identity.Application.Common.Models;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Application.Services;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
using Axon.Modules.Identity.Infrastructure.Persistence.Repositories;
using Axon.Modules.Identity.Infrastructure.Services;
using BuildingBlocks.Application;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Infrastructure.Persistence.Write;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Infrastructure.Tests.Persistence;

/// <summary>
/// Tests for PrincipalResolutionService persistence operations including performance and concurrency.
/// Validates the complete resolution flow using real database operations.
/// </summary>
[TestFixture]
public class PrincipalResolutionPersistenceTests : IdentityPersistenceTestBase
{
    private PrincipalResolutionService _resolutionService = null!;
    private WalletReadRepository _walletReadRepository = null!;
    private WalletOwnershipRepository _walletOwnershipRepository = null!;
    private AxonPrincipalReadRepository _principalReadRepository = null!;
    private IAutoRevocationService _autoRevocationService = null!;
    private ILogger<PrincipalResolutionService> _logger = null!;
    private IdentityReadDbContext _readContext = null!;

    protected override async Task SetUpDerived()
    {
        // Create read context for read repositories
        var readOptions = CreateDbContextOptionsBuilder<IdentityReadDbContext>()
            .UseNpgsql(ConnectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(IdentityReadDbContext).Assembly.FullName);
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "identity");
            })
            .Options;

        _readContext = new IdentityReadDbContext(readOptions);

        _walletReadRepository = new WalletReadRepository(_readContext);
        _walletOwnershipRepository = new WalletOwnershipRepository(_readContext);
        _principalReadRepository = new AxonPrincipalReadRepository(_readContext);
        _autoRevocationService = Substitute.For<IAutoRevocationService>();
        _logger = Substitute.For<ILogger<PrincipalResolutionService>>();

        // Setup default auto-revocation behavior
        _autoRevocationService.ProcessAutoRevocationAsync(
            Arg.Any<WalletId>(),
            Arg.Any<AxonUserId>(),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success<int, Error>(0));

        _resolutionService = new PrincipalResolutionService(
            _principalReadRepository,
            PrincipalRepository,
            _walletReadRepository,
            WalletRepository,
            _walletOwnershipRepository,
            _autoRevocationService,
            _logger);

        await Task.CompletedTask;
    }

    protected override async Task TearDownDerived()
    {
        await _readContext.DisposeAsync();
    }

    #region Performance Tests

    [Test]
    public async Task ResolveAsync_CredentialPath_Performance_ShouldCompleteUnder100ms_P95()
    {
        // Arrange - Create test principal with credential
        var provider = ProviderType.Dynamic;
        var issuer = "dynamic.xyz";
        var baseSubject = "perf-user";
        var chainId = ChainId.From("solana");
        var address = Address.From("EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v");

        // Create a principal with credential for resolution
        var existingPrincipal = AxonPrincipal.CreateWithDynamicCredential(
            provider, issuer, $"{baseSubject}-existing").Value;
        await PrincipalRepository.AddAsync(existingPrincipal);
        await UnitOfWork.SaveChangesAsync();

        // Pre-warm the database connection
        await _principalReadRepository.GetByIdWithActiveOwnershipsAsync(AxonUserId.New(), CancellationToken.None);

        var timings = new List<long>();

        // Act - Measure performance across multiple runs
        for (int i = 0; i < 100; i++)
        {
            var sw = Stopwatch.StartNew();

            var result = await _resolutionService.ResolveAsync(
                provider,
                issuer,
                $"{baseSubject}-existing", // Same subject to hit credential path
                chainId,
                address,
                CancellationToken.None);

            sw.Stop();
            timings.Add(sw.ElapsedMilliseconds);

            // Verify we're hitting the credential path
            result.IsSuccess.ShouldBeTrue();
            result.Value.Path.ShouldBe(ResolutionPath.Credential);
        }

        // Assert - Calculate P95
        timings.Sort();
        var p95Index = (int)Math.Ceiling(timings.Count * 0.95) - 1;
        var p95Value = timings[p95Index];

        p95Value.ShouldBeLessThan(100, $"P95 credential resolution time was {p95Value}ms, expected <100ms");
    }

    [Test]
    public async Task ResolveAsync_WalletPath_Performance_ShouldCompleteUnder100ms_P95()
    {
        // Arrange - Create principal with wallet ownership
        var provider = ProviderType.Dynamic;
        var issuer = "dynamic.xyz";
        var chainId = ChainId.From("ethereum");
        var address = Address.From("0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb0");

        var principal = CreateTestPrincipal();
        var wallet = Wallet.Create(null, chainId.Value, address);

        await SavePrincipalWithWallets(principal, wallet);

        // Create verified signing ownership
        var ownership = WalletOwnership.Create(
            principal.Id,
            wallet.Id,
            AccessMode.Signing,
            OwnershipStatus.Verified,
            VerificationSource.DynamicAttested);

        var linkResult = principal.LinkWalletOwnership(
            ownership,
            (_, _, _) => Result.Success<bool, Error>(false));
        linkResult.IsSuccess.ShouldBeTrue();

        await PrincipalRepository.UpdateAsync(principal);
        await UnitOfWork.SaveChangesAsync();

        // Pre-warm the database connection
        await _walletReadRepository.FindWalletAsync(chainId, address, CancellationToken.None);

        var timings = new List<long>();

        // Act - Measure performance for wallet resolution path
        for (int i = 0; i < 100; i++)
        {
            var sw = Stopwatch.StartNew();

            var result = await _resolutionService.ResolveAsync(
                provider,
                issuer,
                $"nonexistent-user-{i}", // Different subject to bypass credential path
                chainId,
                address,
                CancellationToken.None);

            sw.Stop();
            timings.Add(sw.ElapsedMilliseconds);

            // Verify we're hitting the wallet path
            result.IsSuccess.ShouldBeTrue();
            result.Value.Path.ShouldBe(ResolutionPath.Wallet);
        }

        // Assert - Calculate P95
        timings.Sort();
        var p95Index = (int)Math.Ceiling(timings.Count * 0.95) - 1;
        var p95Value = timings[p95Index];

        p95Value.ShouldBeLessThan(100, $"P95 wallet resolution time was {p95Value}ms, expected <100ms");
    }

    [Test]
    public async Task ResolveAsync_CreatePath_Performance_ShouldCompleteUnder100ms_P95()
    {
        // Arrange
        var provider = ProviderType.Dynamic;
        var issuer = "dynamic.xyz";
        var chainId = ChainId.From("polygon");

        // Pre-warm the database connection
        await _principalReadRepository.GetByIdWithActiveOwnershipsAsync(AxonUserId.New(), CancellationToken.None);

        var timings = new List<long>();
        var createdPrincipals = new List<AxonUserId>();

        // Act - Measure performance for creation path
        for (int i = 0; i < 50; i++) // Reduced iterations for creation test
        {
            var address = Address.From($"0x{i:X40}"); // Unique address each time
            var sw = Stopwatch.StartNew();

            var result = await _resolutionService.ResolveAsync(
                provider,
                issuer,
                $"new-user-{i}",
                chainId,
                address,
                CancellationToken.None);

            sw.Stop();
            timings.Add(sw.ElapsedMilliseconds);

            // Verify we're hitting the creation path
            result.IsSuccess.ShouldBeTrue();
            result.Value.Path.ShouldBe(ResolutionPath.Created);
            createdPrincipals.Add(result.Value.Principal.Id);
        }

        // Clean up created principals
        foreach (var principalId in createdPrincipals)
        {
            var principal = await PrincipalRepository.GetByIdAsync(principalId);
            if (principal != null)
            {
                // Mark as soft deleted instead of hard delete
                principal.SoftDelete();
                await PrincipalRepository.UpdateAsync(principal);
            }
        }
        await UnitOfWork.SaveChangesAsync();

        // Assert - Calculate P95
        timings.Sort();
        var p95Index = (int)Math.Ceiling(timings.Count * 0.95) - 1;
        var p95Value = timings[p95Index];

        p95Value.ShouldBeLessThan(100, $"P95 creation time was {p95Value}ms, expected <100ms");
    }

    #endregion

    #region Concurrency Tests

    [Test]
    public async Task ResolveAsync_ConcurrentCreation_ShouldHandleRaceCondition()
    {
        // Arrange
        var chainId = ChainId.From("binance");
        var address = Address.From("0x1234567890123456789012345678901234567890");
        var provider = ProviderType.Dynamic;
        var issuer = "dynamic.xyz";

        // Create task factory for concurrent resolution attempts
        async Task<Result<PrincipalResolutionResult, Error>> CreateResolutionTask(int index)
        {
            // Create separate DbContexts for each concurrent operation
            using var localWriteContext = CreateConcurrentDbContext();
            using var localReadContext = new IdentityReadDbContext(
                CreateDbContextOptionsBuilder<IdentityReadDbContext>()
                    .UseNpgsql(ConnectionString)
                    .Options);

            using var localUnitOfWork = new EfUnitOfWork<IdentityWriteDbContext, IdentityModule>(localWriteContext);

            var localWalletReadRepository = new WalletReadRepository(localReadContext);
            using var localWalletWriteRepository = new WalletWriteRepository(localWriteContext, localUnitOfWork);
            var localWalletOwnershipRepository = new WalletOwnershipRepository(localReadContext);
            var localPrincipalReadRepository = new AxonPrincipalReadRepository(localReadContext);
            using var localPrincipalWriteRepository = new AxonPrincipalWriteRepository(localWriteContext, localUnitOfWork);
            var localAutoRevocationService = Substitute.For<IAutoRevocationService>();
            localAutoRevocationService.ProcessAutoRevocationAsync(
                Arg.Any<WalletId>(),
                Arg.Any<AxonUserId>(),
                Arg.Any<CancellationToken>())
                .Returns(Result.Success<int, Error>(0));
            var localLogger = Substitute.For<ILogger<PrincipalResolutionService>>();

            var localResolutionService = new PrincipalResolutionService(
                localPrincipalReadRepository,
                localPrincipalWriteRepository,
                localWalletReadRepository,
                localWalletWriteRepository,
                localWalletOwnershipRepository,
                localAutoRevocationService,
                localLogger);

            return await localResolutionService.ResolveAsync(
                provider,
                issuer,
                $"concurrent-user-{index}",
                chainId,
                address,
                CancellationToken.None);
        }

        // Act - Run concurrent resolutions
        var tasks = Enumerable.Range(0, 10)
            .Select(i => CreateResolutionTask(i))
            .ToList();

        var results = await Task.WhenAll(tasks);

        // Assert - Verify race condition handling
        var successfulResults = results.Where(r => r.IsSuccess).ToArray();
        successfulResults.Length.ShouldBeGreaterThan(0, "At least one resolution should succeed");

        // All successful resolutions should return the same principal
        var principalIds = successfulResults
            .Select(r => r.Value.Principal.Id)
            .Distinct()
            .ToList();

        principalIds.Count.ShouldBe(1, "All resolutions should converge to the same principal");

        // Verify paths - at least one should have created
        var createdCount = successfulResults.Count(r => r.Value.Path == ResolutionPath.Created);
        createdCount.ShouldBeGreaterThan(0, "At least one should show creation path");

        // Others might show wallet path due to race condition handling
        var walletPathCount = successfulResults.Count(r => r.Value.Path == ResolutionPath.Wallet);
        (createdCount + walletPathCount).ShouldBe(successfulResults.Length,
            "All successful results should be either Created or Wallet path");
    }

    [Test]
    public async Task ResolveAsync_ConcurrentWalletOwnershipCreation_ShouldPreventDuplicates()
    {
        // Arrange - Pre-create wallet but no ownership
        var chainId = ChainId.From("avalanche");
        var address = Address.From("0xABCDEF1234567890ABCDEF1234567890ABCDEF12");
        var provider = ProviderType.Dynamic;
        var issuer = "dynamic.xyz";

        var wallet = Wallet.Create(null, chainId.Value, address);
        await DbContext.Wallets.AddAsync(wallet);
        await UnitOfWork.SaveChangesAsync();

        // Create concurrent resolution tasks
        async Task<Result<PrincipalResolutionResult, Error>> CreateResolutionTask(int index)
        {
            using var localWriteContext = CreateConcurrentDbContext();
            using var localReadContext = new IdentityReadDbContext(
                CreateDbContextOptionsBuilder<IdentityReadDbContext>()
                    .UseNpgsql(ConnectionString)
                    .Options);

            using var localUnitOfWork = new EfUnitOfWork<IdentityWriteDbContext, IdentityModule>(localWriteContext);

            var localWalletReadRepository = new WalletReadRepository(localReadContext);
            using var localWalletWriteRepository = new WalletWriteRepository(localWriteContext, localUnitOfWork);
            var localWalletOwnershipRepository = new WalletOwnershipRepository(localReadContext);
            var localPrincipalReadRepository = new AxonPrincipalReadRepository(localReadContext);
            using var localPrincipalWriteRepository = new AxonPrincipalWriteRepository(localWriteContext, localUnitOfWork);

            var localAutoRevocationService = Substitute.For<IAutoRevocationService>();
            localAutoRevocationService.ProcessAutoRevocationAsync(
                Arg.Any<WalletId>(),
                Arg.Any<AxonUserId>(),
                Arg.Any<CancellationToken>())
                .Returns(Result.Success<int, Error>(0));

            var localResolutionService = new PrincipalResolutionService(
                localPrincipalReadRepository,
                localPrincipalWriteRepository,
                localWalletReadRepository,
                localWalletWriteRepository,
                localWalletOwnershipRepository,
                localAutoRevocationService,
                Substitute.For<ILogger<PrincipalResolutionService>>());

            // Add small random delay to increase chance of race condition
            await Task.Delay(Random.Shared.Next(10, 50));

            return await localResolutionService.ResolveAsync(
                provider,
                issuer,
                $"ownership-race-{index}",
                chainId,
                address,
                CancellationToken.None);
        }

        // Act
        var tasks = Enumerable.Range(0, 5)
            .Select(i => CreateResolutionTask(i))
            .ToList();

        var results = await Task.WhenAll(tasks);

        // Assert
        var successfulResults = results.Where(r => r.IsSuccess).ToArray();
        successfulResults.Length.ShouldBeGreaterThan(0);

        // Verify only one verified+signing ownership exists
        ClearChangeTracker();
        // Access WalletOwnerships through the aggregate root
        var principals = await DbContext.Principals
            .Include(p => p.WalletOwnerships)
            .Where(p => p.WalletOwnerships.Any(o =>
                o.WalletId == wallet.Id &&
                o.Status == OwnershipStatus.Verified &&
                o.AccessMode == AccessMode.Signing))
            .ToListAsync();

        var ownerships = principals
            .SelectMany(p => p.WalletOwnerships)
            .Where(o => o.WalletId == wallet.Id &&
                       o.Status == OwnershipStatus.Verified &&
                       o.AccessMode == AccessMode.Signing)
            .ToList();

        ownerships.Count.ShouldBe(1, "Only one verified+signing ownership should exist despite concurrent attempts");
    }

    #endregion

    #region Functional Tests

    [Test]
    public async Task ResolveAsync_WithTieBreaking_ShouldApplyCorrectRules()
    {
        // Arrange - Create multiple principals with different ownership characteristics
        var chainId = ChainId.From("optimism");
        var address = Address.From("0x9876543210987654321098765432109876543210");
        var provider = ProviderType.Dynamic;
        var issuer = "dynamic.xyz";

        var wallet = Wallet.Create(null, chainId.Value, address);
        await DbContext.Wallets.AddAsync(wallet);

        // Create older principal with lower priority verification
        var olderPrincipal = CreateTestPrincipal("older", "user1");
        await Task.Delay(50); // Ensure different timestamps
        var newerPrincipal = CreateTestPrincipal("newer", "user2");

        await SavePrincipalWithWallets(olderPrincipal);
        await SavePrincipalWithWallets(newerPrincipal);

        // Create ownerships with different verification sources
        var olderOwnership = WalletOwnership.Create(
            olderPrincipal.Id,
            wallet.Id,
            AccessMode.WatchOnly,
            OwnershipStatus.Verified,
            VerificationSource.DirectSignatureTx); // Lower priority

        var newerOwnership = WalletOwnership.Create(
            newerPrincipal.Id,
            wallet.Id,
            AccessMode.WatchOnly,
            OwnershipStatus.Verified,
            VerificationSource.DynamicAttested); // Higher priority

        olderPrincipal.LinkWalletOwnership(olderOwnership, (_, _, _) => Result.Success<bool, Error>(false));
        newerPrincipal.LinkWalletOwnership(newerOwnership, (_, _, _) => Result.Success<bool, Error>(false));

        await PrincipalRepository.UpdateAsync(olderPrincipal);
        await PrincipalRepository.UpdateAsync(newerPrincipal);
        await UnitOfWork.SaveChangesAsync();

        // Act
        var result = await _resolutionService.ResolveAsync(
            provider,
            issuer,
            "tiebreak-user",
            chainId,
            address,
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Path.ShouldBe(ResolutionPath.Wallet);
        result.Value.Principal.Id.ShouldBe(newerPrincipal.Id,
            "Should select principal with DynamicAttested verification despite being newer");

        // Note: The auto-revocation logic in PrincipalResolutionService attempts to update
        // the losing principal but the test uses separate repositories from the service.
        // In a real scenario with proper transaction boundaries, the auto-revoke would persist.
        // For this test, we're verifying the correct tie-breaking winner is selected.
    }

    [Test]
    public async Task ResolveAsync_VerifiedSigningBypassesTieBreaking()
    {
        // Arrange
        var chainId = ChainId.From("arbitrum");
        var address = Address.From("0xFEDCBA9876543210FEDCBA9876543210FEDCBA98");
        var provider = ProviderType.Dynamic;
        var issuer = "dynamic.xyz";

        var wallet = Wallet.Create(null, chainId.Value, address);
        await DbContext.Wallets.AddAsync(wallet);

        var signingPrincipal = CreateTestPrincipal("signing", "user-signing");
        var watchOnlyPrincipal = CreateTestPrincipal("watchonly", "user-watch");

        await SavePrincipalWithWallets(signingPrincipal);
        await SavePrincipalWithWallets(watchOnlyPrincipal);

        // Create verified+signing ownership (should win regardless of other factors)
        var signingOwnership = WalletOwnership.Create(
            signingPrincipal.Id,
            wallet.Id,
            AccessMode.Signing,
            OwnershipStatus.Verified,
            VerificationSource.WatchOnly); // Even with lowest priority source

        var watchOwnership = WalletOwnership.Create(
            watchOnlyPrincipal.Id,
            wallet.Id,
            AccessMode.WatchOnly,
            OwnershipStatus.Verified,
            VerificationSource.DynamicAttested); // Highest priority source

        signingPrincipal.LinkWalletOwnership(signingOwnership, (_, _, _) => Result.Success<bool, Error>(false));
        watchOnlyPrincipal.LinkWalletOwnership(watchOwnership, (_, _, _) => Result.Success<bool, Error>(false));

        await PrincipalRepository.UpdateAsync(signingPrincipal);
        await PrincipalRepository.UpdateAsync(watchOnlyPrincipal);
        await UnitOfWork.SaveChangesAsync();

        // Act
        var result = await _resolutionService.ResolveAsync(
            provider,
            issuer,
            "bypass-user",
            chainId,
            address,
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Path.ShouldBe(ResolutionPath.Wallet);
        result.Value.Principal.Id.ShouldBe(signingPrincipal.Id,
            "Verified+Signing should always win, bypassing tie-breaking rules");
    }

    [Test]
    public async Task ResolveAsync_CredentialResolution_ReturnsPrincipalWithoutWalletCheck()
    {
        // Arrange
        var provider = ProviderType.Dynamic;
        var issuer = "dynamic.xyz";
        var subject = "credential-user";
        var chainId = ChainId.From("base");
        var address = Address.From("0x1111222233334444555566667777888899990000");

        var principal = AxonPrincipal.CreateWithDynamicCredential(provider, issuer, subject).Value;
        await PrincipalRepository.AddAsync(principal);
        await UnitOfWork.SaveChangesAsync();

        // Act
        var result = await _resolutionService.ResolveAsync(
            provider,
            issuer,
            subject,
            chainId,
            address, // Address is passed but should be ignored for credential resolution
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Principal.Id.ShouldBe(principal.Id);
        result.Value.Path.ShouldBe(ResolutionPath.Credential);
        result.Value.WasAutoLinked.ShouldBeFalse();

        // Verify no wallet was created
        ClearChangeTracker();
        var walletCount = await DbContext.Wallets
            .CountAsync(w => w.ChainId == chainId.Value && w.Address == address);
        walletCount.ShouldBe(0, "Credential resolution should not create wallets");
    }

    #endregion
}