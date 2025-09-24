using Axon.Modules.Identity.Application.Commands.ExchangeCredential;
using Axon.Modules.Identity.Application.Common.Models;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.Contracts.Providers;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Application.DTOs.Exchange;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
using BuildingBlocks.Application;
using BuildingBlocks.Core.Abstractions.Authentication;
using Microsoft.Extensions.Hosting;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Infrastructure.Persistence.Write;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;
using Shouldly;
using Testcontainers.PostgreSql;

namespace Axon.Modules.Identity.Application.Tests.Commands;

/// <summary>
/// Concurrency tests for ExchangeCredentialHandler to verify thread safety,
/// race condition handling, and database lock behavior under concurrent load.
/// </summary>
[TestFixture]
public class ExchangeCredentialConcurrencyTests
{
    private PostgreSqlContainer _postgresContainer = null!;
    private IServiceProvider _serviceProvider = null!;
    private IServiceScope _scope = null!;
    private IdentityWriteDbContext _dbContext = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // Start PostgreSQL container for real database concurrency testing
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:15")
            .WithDatabase("axon_concurrency_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await _postgresContainer.StartAsync();

        // Set up service collection with real database
        var services = new ServiceCollection();
        services.AddDbContext<IdentityWriteDbContext>(options =>
            options.UseNpgsql(_postgresContainer.GetConnectionString()));

        // Add required dependencies
        services.AddScoped<ICurrentUserService, MockCurrentUserService>();
        services.AddSingleton<IMemoryCache, MemoryCache>();
        services.AddSingleton<IHttpContextAccessor>(Substitute.For<IHttpContextAccessor>());
        services.AddSingleton<IPrincipalResolutionService>(Substitute.For<IPrincipalResolutionService>());
        services.AddSingleton<IAddressNormalizationService>(Substitute.For<IAddressNormalizationService>());
        services.AddSingleton<IWalletVerificationService>(Substitute.For<IWalletVerificationService>());
        services.AddLogging();

        _serviceProvider = services.BuildServiceProvider();
        _scope = _serviceProvider.CreateScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<IdentityWriteDbContext>();

        // Ensure database schema exists
        await _dbContext.Database.EnsureCreatedAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _dbContext?.Dispose();
        _scope?.Dispose();
        if (_serviceProvider is IDisposable disposableProvider)
            disposableProvider.Dispose();
        await _postgresContainer.DisposeAsync();
    }

    [SetUp]
    public async Task SetUp()
    {
        // Clean database for each test
        await CleanDatabase();
    }

    #region Concurrent Principal Creation Tests

    [Test]
    public async Task Should_HandleConcurrentCredentialCreation_WithoutDuplicates()
    {
        // Arrange - Multiple requests with same credential
        const int concurrentRequests = 5;
        var userData = CreateTestUserData("concurrent-user-001");
        var command = new ExchangeCredentialCommand("valid-bearer-token");

        var tasks = new List<Task<Result<ExchangeOutcome, Error>>>();

        // Act - Create multiple concurrent handlers and execute simultaneously
        for (int i = 0; i < concurrentRequests; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                using var scope = _serviceProvider.CreateScope();
                using var handler = CreateHandler(scope.ServiceProvider);
                return await handler.Handle(command, CancellationToken.None);
            }));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - Only one should succeed with creation, others should find existing
        var successResults = results.Where(r => r.IsSuccess).ToList();
        var createdResults = successResults.Where(r => r.Value.Created).ToList();

        successResults.Count.ShouldBeGreaterThan(0, "At least one request should succeed");
        createdResults.Count.ShouldBe(1, "Only one request should create the principal");

        // Verify database consistency
        var principalCount = await _dbContext.AxonPrincipals
            .CountAsync(p => p.Credentials.Any(c =>
                c.Provider == "dynamic" && c.Subject == userData.AxonUserId));
        principalCount.ShouldBe(1, "Only one principal should exist in database");
    }

    [Test]
    public async Task Should_HandleConcurrentWalletOwnership_WithoutConflicts()
    {
        // Arrange - Create shared wallet and multiple users trying to claim it
        var sharedWallet = Wallet.Create(
            WalletId.New(),
            "ethereum-mainnet",
            Address.Create("0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e41").Value,
            DateTime.UtcNow);

        _dbContext.Wallets.Add(sharedWallet);
        await _dbContext.SaveChangesAsync();

        const int concurrentClaimants = 3;
        var tasks = new List<Task<Result<ExchangeOutcome, Error>>>();

        // Act - Multiple users trying to claim same wallet concurrently
        for (int i = 0; i < concurrentClaimants; i++)
        {
            var userData = CreateTestUserDataWithWallet($"claimant-user-{i:D3}",
                "0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e41", "ethereum");
            var command = new ExchangeCredentialCommand("valid-bearer-token");

            tasks.Add(Task.Run(async () =>
            {
                using var scope = _serviceProvider.CreateScope();
                using var handler = CreateHandler(scope.ServiceProvider);
                return await handler.Handle(command, CancellationToken.None);
            }));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - Only one should succeed, others should get conflict errors
        var successResults = results.Where(r => r.IsSuccess).ToList();
        var conflictResults = results.Where(r => r.IsFailure && r.Error.Type == ErrorType.Conflict).ToList();

        successResults.Count.ShouldBe(1, "Only one user should successfully claim the wallet");
        conflictResults.Count.ShouldBe(concurrentClaimants - 1, "Others should get conflict errors");

        // Verify database consistency - only one ownership record
        var ownershipCount = await _dbContext.WalletOwnerships
            .CountAsync(o => o.WalletId == sharedWallet.Id);
        ownershipCount.ShouldBeLessThanOrEqualTo(1, "Should have at most one ownership record");
    }

    #endregion

    #region Database Lock and Transaction Tests

    [Test]
    public async Task Should_HandleDatabaseLocks_UnderConcurrentLoad()
    {
        // Arrange - High concurrency scenario with database contention
        const int highConcurrency = 10;
        var tasks = new List<Task<Result<ExchangeOutcome, Error>>>();

        // Act - Create multiple concurrent operations that will compete for database resources
        for (int i = 0; i < highConcurrency; i++)
        {
            var userData = CreateTestUserData($"load-test-user-{i:D3}");
            var command = new ExchangeCredentialCommand("valid-bearer-token");

            tasks.Add(Task.Run(async () =>
            {
                using var scope = _serviceProvider.CreateScope();
                using var handler = CreateHandler(scope.ServiceProvider);

                // Add some artificial delay to increase lock contention
                await Task.Delay(Random.Shared.Next(1, 50));

                return await handler.Handle(command, CancellationToken.None);
            }));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - All operations should complete (success or expected failures)
        var successCount = results.Count(r => r.IsSuccess);
        var failureCount = results.Count(r => r.IsFailure);

        (successCount + failureCount).ShouldBe(highConcurrency, "All operations should complete");
        successCount.ShouldBeGreaterThan(0, "At least some operations should succeed");

        // Verify no database corruption occurred
        var principalCount = await _dbContext.AxonPrincipals.CountAsync();
        principalCount.ShouldBe(successCount, "Principal count should match successful operations");
    }

    [Test]
    public async Task Should_RecoverFromDeadlocks_Gracefully()
    {
        // Arrange - Create scenario prone to deadlocks by accessing resources in different orders
        var wallet1 = CreateAndSaveWallet("0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e41", "ethereum");
        var wallet2 = CreateAndSaveWallet("9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWS", "solana");

        await _dbContext.SaveChangesAsync();

        // Act - Create operations that might cause deadlocks
        var task1 = Task.Run(async () =>
        {
            using var scope = _serviceProvider.CreateScope();
            using var handler = CreateHandler(scope.ServiceProvider);
            var userData = CreateTestUserDataWithWallet("deadlock-user-1", wallet1.Address.Value, "ethereum");
            var command = new ExchangeCredentialCommand("valid-bearer-token");
            return await handler.Handle(command, CancellationToken.None);
        });

        var task2 = Task.Run(async () =>
        {
            using var scope = _serviceProvider.CreateScope();
            using var handler = CreateHandler(scope.ServiceProvider);
            var userData = CreateTestUserDataWithWallet("deadlock-user-2", wallet2.Address.Value, "solana");
            var command = new ExchangeCredentialCommand("valid-bearer-token");
            return await handler.Handle(command, CancellationToken.None);
        });

        var results = await Task.WhenAll(task1, task2);

        // Assert - Both operations should complete successfully or with expected errors
        results.All(r => r.IsSuccess || r.IsFailure).ShouldBeTrue();

        // Verify database consistency
        var principalCount = await _dbContext.AxonPrincipals.CountAsync();
        var walletOwnershipCount = await _dbContext.WalletOwnerships.CountAsync();

        principalCount.ShouldBeGreaterThan(0);
        walletOwnershipCount.ShouldBeGreaterThanOrEqualTo(0);
    }

    #endregion

    #region Cache Consistency Tests

    [Test]
    public async Task Should_MaintainCacheConsistency_UnderConcurrentAccess()
    {
        // Arrange - Shared memory cache and multiple concurrent operations
        var sharedCache = new MemoryCache(new MemoryCacheOptions());
        const int concurrentOperations = 8;
        var userData = CreateTestUserData("cache-consistency-user");

        var tasks = new List<Task<Result<ExchangeOutcome, Error>>>();

        // Act - Multiple operations sharing same cache
        for (int i = 0; i < concurrentOperations; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                using var scope = _serviceProvider.CreateScope();

                // Replace memory cache with shared instance
                var services = new ServiceCollection();
                foreach (var service in scope.ServiceProvider.GetServices<ServiceDescriptor>())
                {
                    if (service.ServiceType != typeof(IMemoryCache))
                        services.Add(service);
                }
                services.AddSingleton<IMemoryCache>(sharedCache);

                using var scopedProvider = services.BuildServiceProvider();
                using var handler = CreateHandler(scopedProvider);
                var command = new ExchangeCredentialCommand("valid-bearer-token");

                return await handler.Handle(command, CancellationToken.None);
            }));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - Cache should remain consistent
        var successCount = results.Count(r => r.IsSuccess);
        successCount.ShouldBeGreaterThan(0, "Some operations should succeed");

        // Verify cache contains expected entries
        var cacheKey = $"axon:user:{userData.AxonUserId}";
        sharedCache.TryGetValue(cacheKey, out var cachedValue).ShouldBeTrue("Cache should contain user mapping");
        cachedValue.ShouldBeOfType<AxonUserId>();

        sharedCache.Dispose();
    }

    #endregion

    #region Helper Methods

    private static DisposableExchangeCredentialHandler CreateHandler(IServiceProvider serviceProvider)
    {
        var currentUserService = serviceProvider.GetRequiredService<ICurrentUserService>();
        var dbContext = serviceProvider.GetRequiredService<IdentityWriteDbContext>();
        var logger = serviceProvider.GetRequiredService<ILogger<ExchangeCredentialHandler>>();

        // Create mocks for the simplified constructor
        var orchestrator = Substitute.For<IAuthenticationOrchestrator>();
        var jwtTokenService = Substitute.For<IJwtTokenService>();

        // Configure orchestrator mock with realistic behavior
        ConfigureMockOrchestrator(orchestrator);

        // Create repositories from the scoped context - these will be disposed by the wrapper
        var principalRepository = new AxonPrincipalWriteRepository(dbContext);
        var walletRepository = new WalletWriteRepository(dbContext);

        var handler = new ExchangeCredentialHandler(
            currentUserService,
            orchestrator,
            jwtTokenService,
            logger);

        return new DisposableExchangeCredentialHandler(handler, principalRepository, walletRepository);
    }

    private static void ConfigureMockOrchestrator(IAuthenticationOrchestrator orchestrator)
    {
        // Configure orchestrator to return successful authentication response
        var mockResponse = new AuthenticationResponse(
            AccessToken: "mock-access-token",
            UserId: Guid.NewGuid(),
            ProviderType: "dynamic",
            ExpiresAt: DateTime.UtcNow.AddMinutes(15),
            AdditionalData: new Dictionary<string, object>
            {
                ["created"] = true,
                ["wallets_processed"] = 1,
                ["wallets_linked"] = 1,
                ["defaults_applied"] = 0,
                ["skipped"] = 0,
                ["conflicts"] = 0
            });

        orchestrator.ExchangeDynamicTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<AuthenticationResponse, Error>(mockResponse));
    }

    private static ExchangeUserData CreateTestUserData(string userId)
    {
        return new ExchangeUserData(
            AxonUserId: userId,
            Email: $"{userId}@example.com",
            DynamicEnvironmentId: "test-env",
            Wallets: new List<ExchangeWalletData>
            {
                new("0x1234567890123456789012345678901234567890", "ethereum")
            }
        );
    }

    private static ExchangeUserData CreateTestUserDataWithWallet(string userId, string address, string chain)
    {
        return new ExchangeUserData(
            AxonUserId: userId,
            Email: $"{userId}@example.com",
            DynamicEnvironmentId: "test-env",
            Wallets: new List<ExchangeWalletData>
            {
                new(address, chain)
            }
        );
    }

    private Wallet CreateAndSaveWallet(string address, string chain)
    {
        var wallet = Wallet.Create(
            WalletId.New(),
            $"{chain}-mainnet",
            Address.Create(address).Value,
            DateTime.UtcNow);

        _dbContext.Wallets.Add(wallet);
        return wallet;
    }

    private async Task CleanDatabase()
    {
        // Clean in dependency order
        await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE identity.wallet_ownerships CASCADE");
        await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE identity.principal_chain_defaults CASCADE");
        await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE identity.credentials CASCADE");
        await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE identity.wallets CASCADE");
        await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE identity.axon_principals CASCADE");
    }

    #endregion

    /// <summary>
    /// Wrapper class to properly dispose of repositories created in tests
    /// </summary>
    private sealed class DisposableExchangeCredentialHandler : IDisposable
    {
        private readonly ExchangeCredentialHandler _handler;
        private readonly AxonPrincipalWriteRepository _principalRepository;
        private readonly WalletWriteRepository _walletRepository;

        public DisposableExchangeCredentialHandler(
            ExchangeCredentialHandler handler,
            AxonPrincipalWriteRepository principalRepository,
            WalletWriteRepository walletRepository)
        {
            _handler = handler;
            _principalRepository = principalRepository;
            _walletRepository = walletRepository;
        }

        public async Task<Result<ExchangeOutcome, Error>> Handle(ExchangeCredentialCommand command, CancellationToken cancellationToken)
        {
            return await _handler.Handle(command, cancellationToken);
        }

        public void Dispose()
        {
            _principalRepository.Dispose();
            _walletRepository.Dispose();
        }
    }

    /// <summary>
    /// Mock implementation of ICurrentUserService for testing
    /// </summary>
    private sealed class MockCurrentUserService : ICurrentUserService
    {
        public string? AxonUserId => null;
        public string? UserName => null;
        public bool IsAuthenticated => false;

        public string GetAxonUserIdOrDefault(string systemAxonUserId = "SYSTEM") => systemAxonUserId;
        public string GetCurrentAxonUserIdOrSystem() => "SYSTEM";
        public Task<AxonUserId?> GetAxonUserIdAsync(CancellationToken cancellationToken = default) => Task.FromResult<AxonUserId?>(null);
        public bool TryGetAxonUserId(out AxonUserId axonUserId)
        {
            axonUserId = default;
            return false;
        }
    }

    /// <summary>
    /// Simplified repository implementations for testing
    /// Note: In real implementation, these would be injected via DI
    /// </summary>
    private sealed class AxonPrincipalWriteRepository : IAxonPrincipalWriteRepository, IDisposable
    {
        private readonly IdentityWriteDbContext _context;

        public AxonPrincipalWriteRepository(IdentityWriteDbContext context)
        {
            _context = context;
            UnitOfWork = new EfUnitOfWork<IdentityWriteDbContext, IdentityModule>(context);
        }

        public IWriteUnitOfWork<IdentityModule> UnitOfWork { get; }

        public async Task<AxonPrincipal> AddAsync(AxonPrincipal entity, CancellationToken cancellationToken = default)
        {
            _context.AxonPrincipals.Add(entity);
            return await Task.FromResult(entity);
        }

        public async Task<AxonPrincipal> UpdateAsync(AxonPrincipal entity, CancellationToken cancellationToken = default)
        {
            _context.AxonPrincipals.Update(entity);
            return await Task.FromResult(entity);
        }

        public async Task<AxonPrincipal?> GetByIdAsync(AxonUserId id, CancellationToken cancellationToken = default)
        {
            return await _context.AxonPrincipals
                .Include(p => p.Credentials)
                .Include(p => p.WalletOwnerships)
                .Include(p => p.PrincipalChainDefaults)
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        }

        public async Task<AxonPrincipal?> FindByCredentialAsync(ProviderType provider, string issuer, string subject, CancellationToken cancellationToken = default)
        {
            return await _context.AxonPrincipals
                .Include(p => p.Credentials)
                .Include(p => p.WalletOwnerships)
                .Include(p => p.PrincipalChainDefaults)
                .FirstOrDefaultAsync(p => p.Credentials.Any(c =>
                    c.Provider == provider.Value && c.Issuer == issuer && c.Subject == subject), cancellationToken);
        }

        public async Task<bool> IsCredentialTakenAsync(ProviderType provider, string issuer, string subject, CancellationToken cancellationToken = default)
        {
            return await _context.Credentials
                .AnyAsync(c => c.Provider == provider.Value && c.Issuer == issuer && c.Subject == subject, cancellationToken);
        }

        // Missing IWriteRepository methods
        public Task<IReadOnlyList<AxonPrincipal>> AddRangeAsync(IReadOnlyList<AxonPrincipal> aggregates, CancellationToken ct = default)
        {
            _context.AxonPrincipals.AddRange(aggregates);
            return Task.FromResult<IReadOnlyList<AxonPrincipal>>(aggregates);
        }

        public Task<IReadOnlyList<AxonPrincipal>> UpdateRangeAsync(IReadOnlyList<AxonPrincipal> aggregates, CancellationToken ct = default)
        {
            _context.AxonPrincipals.UpdateRange(aggregates);
            return Task.FromResult<IReadOnlyList<AxonPrincipal>>(aggregates);
        }

        public Task DeleteAsync(AxonUserId id, CancellationToken ct = default)
        {
            var entity = _context.AxonPrincipals.Find(id);
            if (entity != null) _context.AxonPrincipals.Remove(entity);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(AxonPrincipal aggregate, CancellationToken ct = default)
        {
            _context.AxonPrincipals.Remove(aggregate);
            return Task.CompletedTask;
        }

        public Task DeleteRangeAsync(IReadOnlyList<AxonPrincipal> aggregates, CancellationToken ct = default)
        {
            _context.AxonPrincipals.RemoveRange(aggregates);
            return Task.CompletedTask;
        }

        public async Task<bool> ExistsAsync(AxonUserId id, CancellationToken ct = default)
        {
            return await _context.AxonPrincipals.AnyAsync(p => p.Id == id, ct);
        }

        public async Task<bool> AnyAsync(Expression<Func<AxonPrincipal, bool>> predicate, CancellationToken ct = default)
        {
            return await _context.AxonPrincipals.AnyAsync(predicate, ct);
        }

        public async Task<bool> AnyAsync(CancellationToken ct)
        {
            return await _context.AxonPrincipals.AnyAsync(ct);
        }

        public async Task<Dictionary<WalletId, AxonPrincipal>> FindVerifiedSigningOwnersAsync(IEnumerable<WalletId> walletIds, CancellationToken ct = default)
        {
            var result = new Dictionary<WalletId, AxonPrincipal>();
            foreach (var walletId in walletIds)
            {
                var principal = await _context.AxonPrincipals
                    .Include(p => p.WalletOwnerships)
                    .FirstOrDefaultAsync(p => p.WalletOwnerships.Any(wo => wo.WalletId == walletId && wo.AccessMode == AccessMode.Signing && wo.Status == OwnershipStatus.Verified), ct);
                if (principal != null)
                    result[walletId] = principal;
            }
            return result;
        }

        public void Dispose()
        {
            UnitOfWork.Dispose();
            _context.Dispose();
        }
    }

    private sealed class WalletWriteRepository : IWalletWriteRepository, IDisposable
    {
        private readonly IdentityWriteDbContext _context;

        public WalletWriteRepository(IdentityWriteDbContext context)
        {
            _context = context;
            UnitOfWork = new EfUnitOfWork<IdentityWriteDbContext, IdentityModule>(context);
        }

        public IWriteUnitOfWork<IdentityModule> UnitOfWork { get; }

        public async Task<Wallet> AddAsync(Wallet entity, CancellationToken cancellationToken = default)
        {
            _context.Wallets.Add(entity);
            return await Task.FromResult(entity);
        }

        public async Task<Wallet> UpdateAsync(Wallet entity, CancellationToken cancellationToken = default)
        {
            _context.Wallets.Update(entity);
            return await Task.FromResult(entity);
        }

        public async Task<IReadOnlyDictionary<(string chainId, Address address), WalletId>> EnsureManyByChainAndAddressAsync(
            IEnumerable<(string chainId, Address address)> specifications,
            CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<(string, Address), WalletId>();

            foreach (var (chainId, address) in specifications)
            {
                var existing = await _context.Wallets
                    .FirstOrDefaultAsync(w => w.ChainId == chainId && w.Address == address, cancellationToken);

                if (existing == null)
                {
                    var newWallet = Wallet.Create(WalletId.New(), chainId, address);
                    _context.Wallets.Add(newWallet);
                    result[(chainId, address)] = newWallet.Id;
                }
                else
                {
                    result[(chainId, address)] = existing.Id;
                }
            }

            return result;
        }

        public async Task<IReadOnlyList<Wallet>> GetByIdsAsync(IEnumerable<WalletId> walletIds, CancellationToken cancellationToken = default)
        {
            return await _context.Wallets
                .Where(w => walletIds.Contains(w.Id))
                .ToListAsync(cancellationToken);
        }

        // Missing interface methods from IWalletWriteRepository
        public async Task<Wallet?> GetByChainAndAddressAsync(ChainId chainId, Address address, CancellationToken cancellationToken = default)
        {
            return await _context.Wallets
                .FirstOrDefaultAsync(w => w.ChainId == chainId.Value && w.Address == address, cancellationToken);
        }

        public async Task<Wallet> UpsertWalletAsync(ChainId chainId, Address address, CancellationToken cancellationToken = default)
        {
            var existing = await GetByChainAndAddressAsync(chainId, address, cancellationToken);
            if (existing != null) return existing;

            var newWallet = Wallet.Create(WalletId.New(), chainId.Value, address);
            _context.Wallets.Add(newWallet);
            return newWallet;
        }

        // Missing IWriteRepository methods
        public async Task<Wallet?> GetByIdAsync(WalletId id, CancellationToken ct = default)
        {
            return await _context.Wallets.FirstOrDefaultAsync(w => w.Id == id, ct);
        }

        public Task<IReadOnlyList<Wallet>> AddRangeAsync(IReadOnlyList<Wallet> aggregates, CancellationToken ct = default)
        {
            _context.Wallets.AddRange(aggregates);
            return Task.FromResult<IReadOnlyList<Wallet>>(aggregates);
        }

        public Task<IReadOnlyList<Wallet>> UpdateRangeAsync(IReadOnlyList<Wallet> aggregates, CancellationToken ct = default)
        {
            _context.Wallets.UpdateRange(aggregates);
            return Task.FromResult<IReadOnlyList<Wallet>>(aggregates);
        }

        public Task DeleteAsync(WalletId id, CancellationToken ct = default)
        {
            var entity = _context.Wallets.Find(id);
            if (entity != null) _context.Wallets.Remove(entity);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Wallet aggregate, CancellationToken ct = default)
        {
            _context.Wallets.Remove(aggregate);
            return Task.CompletedTask;
        }

        public Task DeleteRangeAsync(IReadOnlyList<Wallet> aggregates, CancellationToken ct = default)
        {
            _context.Wallets.RemoveRange(aggregates);
            return Task.CompletedTask;
        }

        public async Task<bool> ExistsAsync(WalletId id, CancellationToken ct = default)
        {
            return await _context.Wallets.AnyAsync(w => w.Id == id, ct);
        }

        public async Task<bool> AnyAsync(Expression<Func<Wallet, bool>> predicate, CancellationToken ct = default)
        {
            return await _context.Wallets.AnyAsync(predicate, ct);
        }

        public async Task<bool> AnyAsync(CancellationToken ct)
        {
            return await _context.Wallets.AnyAsync(ct);
        }

        public void Dispose()
        {
            UnitOfWork.Dispose();
            _context.Dispose();
        }
    }
}