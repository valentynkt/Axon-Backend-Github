using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.Services;
using Axon.Modules.Identity.Application.Specifications.AxonPrincipals;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Application.Tests.Services;

[TestFixture]
public class WalletOwnershipServiceTests
{
    private IAxonPrincipalReadRepository _principalReadRepository = null!;
    private ILogger<WalletOwnershipService> _logger = null!;
    private WalletOwnershipService _service = null!;

    [SetUp]
    public void Setup()
    {
        _principalReadRepository = Substitute.For<IAxonPrincipalReadRepository>();
        _logger = Substitute.For<ILogger<WalletOwnershipService>>();
        _service = new WalletOwnershipService(_principalReadRepository, _logger);
    }

    [TestFixture]
    public class CheckCredentialUniquenessAsync : WalletOwnershipServiceTests
    {
        [Test]
        public async Task Should_ReturnExistingPrincipal_When_CredentialExists()
        {
            // Arrange
            var providerType = ProviderType.DynamicVerified;
            var issuer = "test-issuer";
            var subject = "test-subject";
            var existingPrincipal = CreateTestPrincipal();
            
            _principalReadRepository
                .FirstOrDefaultAsync(Arg.Any<PrincipalByCredentialSpec>(), Arg.Any<CancellationToken>())
                .Returns(existingPrincipal);

            // Act
            var result = await _service.CheckCredentialUniquenessAsync(providerType, issuer, subject);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldNotBeNull();
            result.Value.ShouldBe(existingPrincipal);
        }

        [Test]
        public async Task Should_ReturnNull_When_CredentialDoesNotExist()
        {
            // Arrange
            var providerType = ProviderType.DynamicVerified;
            var issuer = "test-issuer";
            var subject = "test-subject";
            
            _principalReadRepository
                .FirstOrDefaultAsync(Arg.Any<PrincipalByCredentialSpec>(), Arg.Any<CancellationToken>())
                .Returns((AxonPrincipal?)null);

            // Act
            var result = await _service.CheckCredentialUniquenessAsync(providerType, issuer, subject);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldBeNull();
        }

        [Test]
        public async Task Should_ReturnFailure_When_RepositoryThrowsException()
        {
            // Arrange
            var providerType = ProviderType.DynamicVerified;
            var issuer = "test-issuer";
            var subject = "test-subject";
            
            _principalReadRepository
                .FirstOrDefaultAsync(Arg.Any<PrincipalByCredentialSpec>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _service.CheckCredentialUniquenessAsync(providerType, issuer, subject);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.ShouldBe("IDENTITY.CREDENTIAL.CHECK_FAILED");
        }
    }

    [TestFixture]
    public class CheckWalletOwnershipConflictAsync : WalletOwnershipServiceTests
    {
        [Test]
        public async Task Should_ReturnTrue_When_ProofTypeIsNotVerifiedSigning()
        {
            // Arrange
            var walletId = WalletId.New();
            var principalId = AxonId.New();
            var proofType = ProofType.WatchOnly;

            // Act
            var result = await _service.CheckWalletOwnershipConflictAsync(walletId, principalId, proofType);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldBeTrue();
            
            // Should not have called repository for non-verified signing proof types
            await _principalReadRepository.DidNotReceive()
                .FirstOrDefaultAsync(Arg.Any<WalletOwnedByVerifiedSigningSpec>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task Should_ReturnTrue_When_NoExistingVerifiedSigningOwner()
        {
            // Arrange
            var walletId = WalletId.New();
            var principalId = AxonId.New();
            var proofType = ProofType.DynamicVerified;
            
            _principalReadRepository
                .FirstOrDefaultAsync(Arg.Any<WalletOwnedByVerifiedSigningSpec>(), Arg.Any<CancellationToken>())
                .Returns((AxonPrincipal?)null);

            // Act
            var result = await _service.CheckWalletOwnershipConflictAsync(walletId, principalId, proofType);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldBeTrue();
        }

        [Test]
        public async Task Should_ReturnTrue_When_ExistingOwnerIsSamePrincipal()
        {
            // Arrange
            var walletId = WalletId.New();
            var principalId = AxonId.New();
            var proofType = ProofType.DynamicVerified;
            var existingPrincipal = CreateTestPrincipal(principalId);
            
            _principalReadRepository
                .FirstOrDefaultAsync(Arg.Any<WalletOwnedByVerifiedSigningSpec>(), Arg.Any<CancellationToken>())
                .Returns(existingPrincipal);

            // Act
            var result = await _service.CheckWalletOwnershipConflictAsync(walletId, principalId, proofType);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldBeTrue();
        }

        [Test]
        public async Task Should_ReturnConflictError_When_DifferentPrincipalOwnsWallet()
        {
            // Arrange
            var walletId = WalletId.New();
            var requestingPrincipalId = AxonId.New();
            var existingOwnerPrincipalId = AxonId.New();
            var proofType = ProofType.DynamicVerified;
            var existingPrincipal = CreateTestPrincipal(existingOwnerPrincipalId);
            
            _principalReadRepository
                .FirstOrDefaultAsync(Arg.Any<WalletOwnedByVerifiedSigningSpec>(), Arg.Any<CancellationToken>())
                .Returns(existingPrincipal);

            // Act
            var result = await _service.CheckWalletOwnershipConflictAsync(walletId, requestingPrincipalId, proofType);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.Conflict);
            result.Error.Code.ShouldBe("IDENTITY.WALLET.VERIFIED_SIGNING_CONFLICT");
        }

        [Test]
        public async Task Should_ReturnFailure_When_RepositoryThrowsException()
        {
            // Arrange
            var walletId = WalletId.New();
            var principalId = AxonId.New();
            var proofType = ProofType.DynamicVerified;
            
            _principalReadRepository
                .FirstOrDefaultAsync(Arg.Any<WalletOwnedByVerifiedSigningSpec>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _service.CheckWalletOwnershipConflictAsync(walletId, principalId, proofType);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.ShouldBe("IDENTITY.WALLET.CONFLICT_CHECK_FAILED");
        }
    }

    [TestFixture]
    public class ValidateWalletLinkingAsync : WalletOwnershipServiceTests
    {
        [Test]
        public async Task Should_ReturnTrue_When_NoConflicts()
        {
            // Arrange
            var principal = CreateTestPrincipal();
            var walletId = WalletId.New();
            var proofType = ProofType.DynamicVerified;
            
            _principalReadRepository
                .FirstOrDefaultAsync(Arg.Any<WalletOwnedByVerifiedSigningSpec>(), Arg.Any<CancellationToken>())
                .Returns((AxonPrincipal?)null);

            // Act
            var result = await _service.ValidateWalletLinkingAsync(principal, walletId, proofType);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldBeTrue();
        }

        [Test]
        public async Task Should_ReturnConflictError_When_ConflictExists()
        {
            // Arrange
            var principal = CreateTestPrincipal();
            var walletId = WalletId.New();
            var proofType = ProofType.DynamicVerified;
            var conflictingPrincipal = CreateTestPrincipal();
            
            _principalReadRepository
                .FirstOrDefaultAsync(Arg.Any<WalletOwnedByVerifiedSigningSpec>(), Arg.Any<CancellationToken>())
                .Returns(conflictingPrincipal);

            // Act
            var result = await _service.ValidateWalletLinkingAsync(principal, walletId, proofType);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.Conflict);
        }

        [Test]
        public async Task Should_ReturnFailure_When_ExceptionOccurs()
        {
            // Arrange
            var principal = CreateTestPrincipal();
            var walletId = WalletId.New();
            var proofType = ProofType.DynamicVerified;
            
            _principalReadRepository
                .FirstOrDefaultAsync(Arg.Any<WalletOwnedByVerifiedSigningSpec>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _service.ValidateWalletLinkingAsync(principal, walletId, proofType);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.ShouldBe("IDENTITY.WALLET.LINKING_VALIDATION_FAILED");
        }
    }

    private static AxonPrincipal CreateTestPrincipal(AxonId? id = null)
    {
        var principalResult = AxonPrincipal.CreateHumanPrincipal();
        var principal = principalResult.Value;
        
        if (id.HasValue)
        {
            // Use reflection to set the ID for test purposes
            var idField = typeof(AxonPrincipal).GetField("_id", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            idField?.SetValue(principal, id.Value);
        }

        return principal;
    }
}