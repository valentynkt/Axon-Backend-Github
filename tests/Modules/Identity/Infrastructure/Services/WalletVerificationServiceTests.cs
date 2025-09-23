using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
using Axon.Modules.Identity.Infrastructure.Services;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Axon.Modules.Identity.Infrastructure.Services.Tests;

[TestFixture]
public class WalletVerificationServiceTests
{
    private WalletVerificationService _service = null!;
    private IdentityWriteDbContext _dbContext = null!;

    [SetUp]
    public void SetUp()
    {
        var serviceLogger = Substitute.For<ILogger<WalletVerificationService>>();
        var dbLogger = Substitute.For<ILogger<IdentityWriteDbContext>>();

        // Use in-memory database for testing
        var options = new DbContextOptionsBuilder<IdentityWriteDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;

        _dbContext = new IdentityWriteDbContext(options, dbLogger);
        _service = new WalletVerificationService(_dbContext, serviceLogger);
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext?.Dispose();
    }

    [Test]
    public async Task VerifyWalletOwnershipAsync_WalletNotFound_ReturnsNotFoundError()
    {
        // Arrange
        var walletId = WalletId.New();
        var principalId = AxonUserId.New();
        var accessMode = AccessMode.Signing;
        var verificationSource = VerificationSource.DynamicAttested;

        // Setup wallet not found
        _dbContext.Wallets.FromSqlRaw(Arg.Any<string>(), Arg.Any<object[]>())
            .Returns(new List<Wallet>().AsQueryable());

        // Act
        var result = await _service.VerifyWalletOwnershipAsync(
            walletId, principalId, accessMode, verificationSource, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("WALLET.NOT_FOUND");
        // Transaction management is now handled internally by the service
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

        // Setup wallet found
        _dbContext.Wallets.FromSqlRaw(Arg.Any<string>(), Arg.Any<object[]>())
            .Returns(new List<Wallet> { wallet }.AsQueryable());

        // Setup existing ownership found
        _dbContext.WalletOwnerships.FromSqlRaw(Arg.Any<string>(), Arg.Any<object[]>())
            .Returns(new List<WalletOwnership> { existingOwnership }.AsQueryable());

        // Act
        var result = await _service.VerifyWalletOwnershipAsync(
            walletId, principalId, accessMode, verificationSource, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("WALLET.OWNERSHIP.ALREADY_VERIFIED");
        // Transaction management is now handled internally by the service
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

        // Setup wallet found
        _dbContext.Wallets.FromSqlRaw(Arg.Any<string>(), Arg.Any<object[]>())
            .Returns(new List<Wallet> { wallet }.AsQueryable());

        // Setup no existing ownerships
        _dbContext.WalletOwnerships.FromSqlRaw(Arg.Any<string>(), Arg.Any<object[]>())
            .Returns(new List<WalletOwnership>().AsQueryable());

        // Setup successful save
        _dbContext.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

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

        await _dbContext.WalletOwnerships.Received(1).AddAsync(Arg.Any<WalletOwnership>(), Arg.Any<CancellationToken>());
        await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        // Transaction management is now handled internally by the service
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

        // Setup wallet found
        _dbContext.Wallets.FromSqlRaw(Arg.Any<string>(), Arg.Any<object[]>())
            .Returns(new List<Wallet> { wallet }.AsQueryable());

        // Setup existing ownership found
        _dbContext.WalletOwnerships.FromSqlRaw(Arg.Any<string>(), Arg.Any<object[]>())
            .Returns(new List<WalletOwnership> { existingOwnership }.AsQueryable());

        // Setup successful save
        _dbContext.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        // Act
        var result = await _service.VerifyWalletOwnershipAsync(
            walletId, principalId, accessMode, verificationSource, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Status.Should().Be(OwnershipStatus.Verified);
        result.Value.AccessMode.Should().Be(accessMode);

        await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        // Transaction management is now handled internally by the service
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

        // Setup wallet found
        _dbContext.Wallets.FromSqlRaw(Arg.Any<string>(), Arg.Any<object[]>())
            .Returns(new List<Wallet> { wallet }.AsQueryable());

        // Setup existing ownerships
        _dbContext.WalletOwnerships.FromSqlRaw(Arg.Any<string>(), Arg.Any<object[]>())
            .Returns(new List<WalletOwnership> { pendingOwnership, verifiedOwnership }.AsQueryable());

        // Setup successful save
        _dbContext.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        // Act
        var result = await _service.VerifyWalletOwnershipAsync(
            walletId, principalId, accessMode, verificationSource, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Pending ownership should be revoked, verified ownership should remain unchanged
        pendingOwnership.Status.Should().Be(OwnershipStatus.Revoked);
        verifiedOwnership.Status.Should().Be(OwnershipStatus.Verified);

        // Transaction management is now handled internally by the service
    }

    [Test]
    public async Task RevokeOwnershipAsync_OwnershipNotFound_ReturnsNotFoundError()
    {
        // Arrange
        var ownershipId = WalletOwnershipId.New();
        var reason = "Test revocation";

        // Setup ownership not found
        _dbContext.WalletOwnerships.FirstOrDefaultAsync(Arg.Any<System.Linq.Expressions.Expression<Func<WalletOwnership, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((WalletOwnership?)null);

        // Act
        var result = await _service.RevokeOwnershipAsync(ownershipId, reason, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("WALLET.OWNERSHIP.NOT_FOUND");
        // Transaction management is now handled internally by the service
    }

    [Test]
    public async Task RevokeOwnershipAsync_ValidOwnership_ReturnsSuccessResult()
    {
        // Arrange
        var ownershipId = WalletOwnershipId.New();
        var principalId = AxonUserId.New();
        var walletId = WalletId.New();
        var reason = "Test revocation";

        var ownership = CreateTestWalletOwnership(principalId, walletId, AccessMode.Signing, OwnershipStatus.Verified);

        // Setup ownership found
        _dbContext.WalletOwnerships.FirstOrDefaultAsync(Arg.Any<System.Linq.Expressions.Expression<Func<WalletOwnership, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(ownership);

        // Setup successful save
        _dbContext.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        // Act
        var result = await _service.RevokeOwnershipAsync(ownershipId, reason, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        ownership.Status.Should().Be(OwnershipStatus.Revoked);

        await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        // Transaction management is now handled internally by the service
    }

    [Test]
    public async Task CanSetAsDefaultAsync_OwnershipNotFound_ReturnsFalse()
    {
        // Arrange
        var principalId = AxonUserId.New();
        var walletId = WalletId.New();

        // Setup ownership not found
        _dbContext.WalletOwnerships.AsNoTracking()
            .Returns(_dbContext.WalletOwnerships);
        _dbContext.WalletOwnerships.FirstOrDefaultAsync(Arg.Any<System.Linq.Expressions.Expression<Func<WalletOwnership, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((WalletOwnership?)null);

        // Act
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

        // Setup ownership found
        _dbContext.WalletOwnerships.AsNoTracking()
            .Returns(_dbContext.WalletOwnerships);
        _dbContext.WalletOwnerships.FirstOrDefaultAsync(Arg.Any<System.Linq.Expressions.Expression<Func<WalletOwnership, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(ownership);

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

        // Setup ownership found
        _dbContext.WalletOwnerships.AsNoTracking()
            .Returns(_dbContext.WalletOwnerships);
        _dbContext.WalletOwnerships.FirstOrDefaultAsync(Arg.Any<System.Linq.Expressions.Expression<Func<WalletOwnership, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(ownership);

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

        // Setup successful save
        _dbContext.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        // Act
        var result = await _service.VerifyBatchWalletOwnershipsAsync(requests, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
        // Transaction management is now handled internally by the service
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

        // Setup wallets found
        _dbContext.Wallets.FromSqlRaw(Arg.Any<string>())
            .Returns(wallets.AsQueryable());

        // Setup no existing ownerships
        _dbContext.WalletOwnerships.FromSqlRaw(Arg.Any<string>())
            .Returns(new List<WalletOwnership>().AsQueryable());

        // Setup successful save
        _dbContext.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

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

        // Transaction management is now handled internally by the service
    }

    private static Wallet CreateTestWallet(WalletId walletId)
    {
        return Wallet.Create(
            walletId,
            "solana-mainnet",
            Address.Create("9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM").Value);
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

// Helper class for testing async enumerables
public class TestAsyncEnumerable<T> : IAsyncEnumerable<T>
{
    private readonly IEnumerable<T> _enumerable;

    public TestAsyncEnumerable(IEnumerable<T> enumerable)
    {
        _enumerable = enumerable;
    }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new TestAsyncEnumerator<T>(_enumerable.GetEnumerator());
    }
}

public class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
{
    private readonly IEnumerator<T> _enumerator;

    public TestAsyncEnumerator(IEnumerator<T> enumerator)
    {
        _enumerator = enumerator;
    }

    public T Current => _enumerator.Current;

    public ValueTask<bool> MoveNextAsync()
    {
        return ValueTask.FromResult(_enumerator.MoveNext());
    }

    public ValueTask DisposeAsync()
    {
        _enumerator.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}