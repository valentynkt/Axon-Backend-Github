using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Infrastructure.Persistence;
using Axon.Modules.Identity.Infrastructure.Services;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Axon.Modules.Identity.Infrastructure.Services.Tests;

[TestFixture]
public class WalletVerificationServiceTests : IdentityPersistenceTestBase
{
    private WalletVerificationService _service = null!;

    protected override async Task SetUpDerived()
    {
        var serviceLogger = Substitute.For<ILogger<WalletVerificationService>>();
        _service = new WalletVerificationService(DbContext, serviceLogger);
        await Task.CompletedTask;
    }

    [Test]
    public async Task VerifyWalletOwnershipAsync_WalletNotFound_ReturnsNotFoundError()
    {
        // Arrange
        var walletId = WalletId.New();
        var principalId = AxonUserId.New();
        var accessMode = AccessMode.Signing;
        var verificationSource = VerificationSource.DynamicAttested;

        // Act - No wallet setup, database is empty by default
        var result = await _service.VerifyWalletOwnershipAsync(
            walletId, principalId, accessMode, verificationSource, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("WALLET.NOT_FOUND");
    }

    [Test]
    public async Task VerifyWalletOwnershipAsync_ExistingVerifiedSigningByDifferentPrincipal_ReturnsConflictError()
    {
        // Arrange
        var walletId = WalletId.New();
        var principalId = AxonUserId.New();
        var existingPrincipalId = AxonUserId.New();
        var accessMode = AccessMode.Signing;
        var verificationSource = VerificationSource.DynamicAttested;

        var wallet = CreateTestWallet(walletId);
        var existingOwnership = CreateTestWalletOwnership(existingPrincipalId, walletId, AccessMode.Signing, OwnershipStatus.Verified);

        // Setup wallet and existing ownership in database
        await DbContext.Wallets.AddAsync(wallet);
        await DbContext.WalletOwnerships.AddAsync(existingOwnership);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _service.VerifyWalletOwnershipAsync(
            walletId, principalId, accessMode, verificationSource, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("WALLET.OWNERSHIP.ALREADY_VERIFIED");
    }

    [Test]
    public async Task VerifyWalletOwnershipAsync_CreateNewOwnership_ReturnsSuccessResult()
    {
        // Arrange
        var walletId = WalletId.New();
        var principalId = AxonUserId.New();
        var accessMode = AccessMode.Signing;
        var verificationSource = VerificationSource.DynamicAttested;

        var wallet = CreateTestWallet(walletId);

        // Setup wallet in database
        await DbContext.Wallets.AddAsync(wallet);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _service.VerifyWalletOwnershipAsync(
            walletId, principalId, accessMode, verificationSource, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.PrincipalId.Should().Be(principalId);
        result.Value.WalletId.Should().Be(walletId);
        result.Value.AccessMode.Should().Be(accessMode);
        result.Value.Status.Should().Be(OwnershipStatus.Verified);
    }

    [Test]
    public async Task VerifyWalletOwnershipAsync_UpdateExistingOwnership_ReturnsSuccessResult()
    {
        // Arrange
        var walletId = WalletId.New();
        var principalId = AxonUserId.New();
        var accessMode = AccessMode.Signing;
        var verificationSource = VerificationSource.DynamicAttested;

        var wallet = CreateTestWallet(walletId);
        var existingOwnership = CreateTestWalletOwnership(principalId, walletId, AccessMode.WatchOnly, OwnershipStatus.Pending);

        // Setup wallet and existing ownership in database
        await DbContext.Wallets.AddAsync(wallet);
        await DbContext.WalletOwnerships.AddAsync(existingOwnership);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _service.VerifyWalletOwnershipAsync(
            walletId, principalId, accessMode, verificationSource, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Status.Should().Be(OwnershipStatus.Verified);
        result.Value.AccessMode.Should().Be(accessMode);
    }

    [Test]
    public async Task VerifyWalletOwnershipAsync_AutoRevokePendingOwnerships_RevokesOnlyPendingOwnerships()
    {
        // Arrange
        var walletId = WalletId.New();
        var principalId = AxonUserId.New();
        var otherPrincipalId1 = AxonUserId.New();
        var otherPrincipalId2 = AxonUserId.New();
        var accessMode = AccessMode.Signing;
        var verificationSource = VerificationSource.DynamicAttested;

        var wallet = CreateTestWallet(walletId);
        var pendingOwnership = CreateTestWalletOwnership(otherPrincipalId1, walletId, AccessMode.Signing, OwnershipStatus.Pending);
        var verifiedOwnership = CreateTestWalletOwnership(otherPrincipalId2, walletId, AccessMode.WatchOnly, OwnershipStatus.Verified);

        // Setup wallet and existing ownerships in database
        await DbContext.Wallets.AddAsync(wallet);
        await DbContext.WalletOwnerships.AddAsync(pendingOwnership);
        await DbContext.WalletOwnerships.AddAsync(verifiedOwnership);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _service.VerifyWalletOwnershipAsync(
            walletId, principalId, accessMode, verificationSource, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Reload entities from database to check final state
        await DbContext.Entry(pendingOwnership).ReloadAsync();
        await DbContext.Entry(verifiedOwnership).ReloadAsync();

        // Pending ownership should be revoked, verified ownership should remain unchanged
        pendingOwnership.Status.Should().Be(OwnershipStatus.Revoked);
        verifiedOwnership.Status.Should().Be(OwnershipStatus.Verified);
    }

    [Test]
    public async Task RevokeOwnershipAsync_OwnershipNotFound_ReturnsNotFoundError()
    {
        // Arrange
        var ownershipId = WalletOwnershipId.New();
        var reason = "Test revocation";

        // Act - No ownership setup, database is empty
        var result = await _service.RevokeOwnershipAsync(ownershipId, reason, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("WALLET.OWNERSHIP.NOT_FOUND");
    }

    [Test]
    public async Task RevokeOwnershipAsync_ValidOwnership_ReturnsSuccessResult()
    {
        // Arrange
        var principalId = AxonUserId.New();
        var walletId = WalletId.New();
        var reason = "Test revocation";

        var ownership = CreateTestWalletOwnership(principalId, walletId, AccessMode.Signing, OwnershipStatus.Verified);

        // Setup ownership in database
        await DbContext.WalletOwnerships.AddAsync(ownership);
        await DbContext.SaveChangesAsync();

        var ownershipId = ownership.Id;

        // Act
        var result = await _service.RevokeOwnershipAsync(ownershipId, reason, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Reload entity to check final state
        await DbContext.Entry(ownership).ReloadAsync();
        ownership.Status.Should().Be(OwnershipStatus.Revoked);
    }

    [Test]
    public async Task CanSetAsDefaultAsync_OwnershipNotFound_ReturnsFalse()
    {
        // Arrange
        var principalId = AxonUserId.New();
        var walletId = WalletId.New();

        // Act - No ownership in database
        var result = await _service.CanSetAsDefaultAsync(principalId, walletId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }

    [Test]
    public async Task CanSetAsDefaultAsync_VerifiedSigningOwnership_ReturnsTrue()
    {
        // Arrange
        var principalId = AxonUserId.New();
        var walletId = WalletId.New();

        var ownership = CreateTestWalletOwnership(principalId, walletId, AccessMode.Signing, OwnershipStatus.Verified);

        // Setup ownership in database
        await DbContext.WalletOwnerships.AddAsync(ownership);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _service.CanSetAsDefaultAsync(principalId, walletId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Test]
    public async Task CanSetAsDefaultAsync_WatchOnlyOwnership_ReturnsFalse()
    {
        // Arrange
        var principalId = AxonUserId.New();
        var walletId = WalletId.New();

        var ownership = CreateTestWalletOwnership(principalId, walletId, AccessMode.WatchOnly, OwnershipStatus.Verified);

        // Setup ownership in database
        await DbContext.WalletOwnerships.AddAsync(ownership);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _service.CanSetAsDefaultAsync(principalId, walletId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }

    [Test]
    public async Task VerifyBatchWalletOwnershipsAsync_EmptyRequests_ReturnsEmptyList()
    {
        // Arrange
        var requests = new List<(WalletId WalletId, AxonUserId PrincipalId, AccessMode AccessMode, VerificationSource VerificationSource)>();

        // Act
        var result = await _service.VerifyBatchWalletOwnershipsAsync(requests, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Test]
    public async Task VerifyBatchWalletOwnershipsAsync_MultipleWallets_ProcessesInConsistentOrder()
    {
        // Arrange - Create requests in non-alphabetical order to test ordering
        var walletId1 = new WalletId(new Guid("11111111-1111-1111-1111-111111111111"));
        var walletId2 = new WalletId(new Guid("22222222-2222-2222-2222-222222222222"));
        var walletId3 = new WalletId(new Guid("33333333-3333-3333-3333-333333333333"));

        var principalId1 = AxonUserId.New();
        var principalId2 = AxonUserId.New();

        // Submit in reverse order to test consistent ordering
        var requests = new List<(WalletId WalletId, AxonUserId PrincipalId, AccessMode AccessMode, VerificationSource VerificationSource)>
        {
            (walletId3, principalId1, AccessMode.Signing, VerificationSource.DynamicAttested),
            (walletId1, principalId2, AccessMode.Signing, VerificationSource.DirectSignatureMsg),
            (walletId2, principalId1, AccessMode.WatchOnly, VerificationSource.WatchOnly)
        };

        var wallets = new List<Wallet>
        {
            CreateTestWallet(walletId1),
            CreateTestWallet(walletId2),
            CreateTestWallet(walletId3)
        };

        // Setup wallets in database
        await DbContext.Wallets.AddRangeAsync(wallets);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _service.VerifyBatchWalletOwnershipsAsync(requests, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);

        // Verify correct ownerships were created
        var ownership1 = result.Value.First(o => o.WalletId == walletId1);
        var ownership2 = result.Value.First(o => o.WalletId == walletId2);
        var ownership3 = result.Value.First(o => o.WalletId == walletId3);

        ownership1.PrincipalId.Should().Be(principalId2);
        ownership1.AccessMode.Should().Be(AccessMode.Signing);

        ownership2.PrincipalId.Should().Be(principalId1);
        ownership2.AccessMode.Should().Be(AccessMode.WatchOnly);

        ownership3.PrincipalId.Should().Be(principalId1);
        ownership3.AccessMode.Should().Be(AccessMode.Signing);
    }

    private static Wallet CreateTestWallet(WalletId walletId)
    {
        return Wallet.Create(
            walletId,
            "solana-mainnet",
            Address.From("9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM"));
    }

    private static WalletOwnership CreateTestWalletOwnership(
        AxonUserId principalId,
        WalletId walletId,
        AccessMode accessMode,
        OwnershipStatus status)
    {
        var ownership = WalletOwnership.Create(
            principalId,
            walletId,
            accessMode,
            status,
            VerificationSource.DynamicAttested);

        return ownership;
    }
}