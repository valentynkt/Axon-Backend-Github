using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
using Axon.Modules.Identity.Infrastructure.Persistence.Repositories;
using Axon.Modules.Identity.Application.Common.Models;
using BuildingBlocks.Application;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Infrastructure.Persistence.Write;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// Integration tests specifically for PrincipalChainDefault persistence issues.
/// These tests validate that navigation property changes are properly tracked and saved.
/// </summary>
[TestFixture]
public class PrincipalChainDefaultPersistenceTests
{
    private IdentityWriteDbContext _dbContext = null!;
    private AxonPrincipalWriteRepository _repository = null!;
    private EfUnitOfWork<IdentityWriteDbContext, IdentityModule> _unitOfWork = null!;

    [SetUp]
    public async Task SetUp()
    {
        // Create an in-memory database for testing with a unique name per test run
        // This ensures complete isolation between tests and prevents ID conflicts
        var databaseName = $"PrincipalChainDefaultTests_{Guid.NewGuid():N}_{DateTime.UtcNow.Ticks}";
        var options = new DbContextOptionsBuilder<IdentityWriteDbContext>()
            .UseInMemoryDatabase(databaseName: databaseName)
            .EnableSensitiveDataLogging() // For debugging
            .Options;

        _dbContext = new IdentityWriteDbContext(options);
        _unitOfWork = new EfUnitOfWork<IdentityWriteDbContext, IdentityModule>(_dbContext);
        _repository = new AxonPrincipalWriteRepository(_dbContext, _unitOfWork);

        // Ensure database is created
        await _dbContext.Database.EnsureCreatedAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        // Clear change tracker to prevent any conflicts
        _dbContext.ChangeTracker.Clear();

        // Clean up resources in proper order
        await _dbContext.Database.EnsureDeletedAsync();
        _unitOfWork?.Dispose();
        _repository?.Dispose();
        await _dbContext.DisposeAsync();
    }

    [Test]
    public async Task UpdateAsync_Should_PersistNewPrincipalChainDefaults_When_AddedToTrackedAggregate()
    {
        // Arrange: Create a principal with some wallets
        var principal = CreateTestPrincipal();
        var wallet1 = CreateTestWallet("1", "0x1234567890123456789012345678901234567890");
        var wallet2 = CreateTestWallet("137", "0xabcdefabcdefabcdefabcdefabcdefabcdefabcd");

        // Add initial data and save
        await _dbContext.Wallets.AddAsync(wallet1);
        await _dbContext.Wallets.AddAsync(wallet2);
        await _repository.AddAsync(principal);
        await _unitOfWork.SaveChangesAsync();

        // Step 1: Reload the principal (simulating the exchange flow where principal is loaded from DB)
        var reloadedPrincipal = await _repository.GetByIdAsync(principal.Id);
        reloadedPrincipal.ShouldNotBeNull();
        reloadedPrincipal.PrincipalChainDefaults.ShouldBeEmpty(); // Should start empty

        // Step 2: Add wallet ownerships (simulating the exchange flow)
        var ownership1 = WalletOwnership.Create(
            reloadedPrincipal.Id, wallet1.Id, AccessMode.Signing, OwnershipStatus.Verified);
        var ownership2 = WalletOwnership.Create(
            reloadedPrincipal.Id, wallet2.Id, AccessMode.Signing, OwnershipStatus.Verified);

        var linkResult1 = reloadedPrincipal.LinkWalletOwnership(ownership1, (_, _, _) => Result.Success<bool, Error>(false));
        var linkResult2 = reloadedPrincipal.LinkWalletOwnership(ownership2, (_, _, _) => Result.Success<bool, Error>(false));

        linkResult1.IsSuccess.ShouldBeTrue();
        linkResult2.IsSuccess.ShouldBeTrue();

        // Step 3: Apply chain defaults (this is the critical part that was failing)
        var chainMappings = new[]
        {
            ("ethereum-mainnet", wallet1.Id),
            ("polygon-mainnet", wallet2.Id)
        };

        var batchResult = reloadedPrincipal.ApplyChainDefaultsBatch(chainMappings);
        batchResult.IsSuccess.ShouldBeTrue();
        batchResult.Value.ShouldBe(2); // Should apply 2 defaults

        // Verify in-memory state before persistence
        reloadedPrincipal.PrincipalChainDefaults.Count.ShouldBe(2);
        reloadedPrincipal.PrincipalChainDefaults.ShouldContain(pcd => pcd.ChainId == "1" && pcd.WalletId == wallet1.Id);
        reloadedPrincipal.PrincipalChainDefaults.ShouldContain(pcd => pcd.ChainId == "137" && pcd.WalletId == wallet2.Id);

        // Step 4: Update and save (fixed tracking issue by ensuring proper state management)
        await _repository.UpdateAsync(reloadedPrincipal);
        var rowsAffected = await _unitOfWork.SaveChangesAsync();

        // Step 5: Verify the data was actually persisted to database
        rowsAffected.ShouldBeGreaterThan(0); // Should have affected some rows

        // Clear context to ensure we're reading from database, not cache
        _dbContext.ChangeTracker.Clear();

        // Step 6: Query database directly to verify PrincipalChainDefaults were saved
        var savedDefaults = await _dbContext.PrincipalChainDefaults
            .Where(pcd => pcd.PrincipalId == principal.Id)
            .ToListAsync();

        // CRITICAL ASSERTION: This is what was failing before the fix
        savedDefaults.ShouldNotBeEmpty("PrincipalChainDefaults should be saved to database");
        savedDefaults.Count.ShouldBe(2, "Should have 2 chain defaults saved");
        savedDefaults.ShouldContain(pcd => pcd.ChainId == "1" && pcd.WalletId == wallet1.Id);
        savedDefaults.ShouldContain(pcd => pcd.ChainId == "137" && pcd.WalletId == wallet2.Id);

        // Step 7: Verify a fresh load also includes the defaults
        var freshPrincipal = await _repository.GetByIdAsync(principal.Id);
        freshPrincipal.ShouldNotBeNull();
        freshPrincipal.PrincipalChainDefaults.Count.ShouldBe(2);
    }

    [Test]
    public async Task UpdateAsync_Should_PersistUpdatedPrincipalChainDefaults_When_ExistingDefaultIsChanged()
    {
        // Arrange: Create principal with existing chain default
        var principal = CreateTestPrincipal();
        var wallet1 = CreateTestWallet("1", "0x1234567890123456789012345678901234567890");
        var wallet2 = CreateTestWallet("1", "0xabcdefabcdefabcdefabcdefabcdefabcdefabcd"); // Same chain, different wallet

        await _dbContext.Wallets.AddAsync(wallet1);
        await _dbContext.Wallets.AddAsync(wallet2);

        // Add initial default
        var ownership1 = WalletOwnership.Create(principal.Id, wallet1.Id, AccessMode.Signing, OwnershipStatus.Verified);
        principal.LinkWalletOwnership(ownership1, (_, _, _) => Result.Success<bool, Error>(false));
        principal.ApplyChainDefaultsBatch(new[] { ("ethereum-mainnet", wallet1.Id) });

        await _repository.AddAsync(principal);
        await _unitOfWork.SaveChangesAsync();

        // Reload and change the default
        var reloadedPrincipal = await _repository.GetByIdAsync(principal.Id);
        reloadedPrincipal.ShouldNotBeNull();

        var ownership2 = WalletOwnership.Create(reloadedPrincipal.Id, wallet2.Id, AccessMode.Signing, OwnershipStatus.Verified);
        reloadedPrincipal.LinkWalletOwnership(ownership2, (_, _, _) => Result.Success<bool, Error>(false));

        // Change the default to wallet2
        var updateResult = reloadedPrincipal.ApplyChainDefaultsBatch(new[] { ("ethereum-mainnet", wallet2.Id) });
        updateResult.IsSuccess.ShouldBeTrue();
        updateResult.Value.ShouldBe(1); // Should update 1 default

        // Persist the change
        await _repository.UpdateAsync(reloadedPrincipal);
        await _unitOfWork.SaveChangesAsync();

        // Verify the change was persisted
        _dbContext.ChangeTracker.Clear();
        var savedDefaults = await _dbContext.PrincipalChainDefaults
            .Where(pcd => pcd.PrincipalId == principal.Id)
            .ToListAsync();

        savedDefaults.Count.ShouldBe(1);
        savedDefaults.Single().WalletId.ShouldBe(wallet2.Id);
    }

    private static AxonPrincipal CreateTestPrincipal()
    {
        var providerType = ProviderType.Create("dynamic").Value;
        return AxonPrincipal.CreateWithDynamicCredential(
            providerType,
            "https://app.dynamic.xyz/test",
            "test-user-123").Value;
    }

    private static Wallet CreateTestWallet(string chainId, string address)
    {
        return Wallet.Create(
            null,
            chainId,
            Address.Create(address).Value);
    }
}