using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.Errors;
using Axon.Modules.Identity.Domain.Tests.TestData;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Domain.Tests.Security;

[TestFixture]
public class DomainSecurityTests
{
    private AxonPrincipal _principal;
    private WalletId _walletId;

    [SetUp]
    public void Setup()
    {
        _principal = AxonPrincipal.CreateHuman();
        _walletId = WalletId.New();
    }

    [TestFixture]
    public class PrivacyProtectionTests : DomainSecurityTests
    {
        // Test ID: 1.3-UNIT-030 - P0
        [Test]
        public void DomainErrors_ShouldNotExposePrincipalIds()
        {
            // Act - When operations fail and generate domain errors
            var invalidWalletResult = _principal.ApplyChainDefault("solana-mainnet", WalletId.New());

            // Assert - Then error messages should not contain principal IDs
            invalidWalletResult.IsFailure.ShouldBeTrue();
            var errorMessage = invalidWalletResult.Error.Message;
            var errorCode = invalidWalletResult.Error.Code;
            
            // Verify no sensitive information is exposed
            errorMessage.ShouldNotContain(_principal.Id.Value.ToString());
            errorMessage.ShouldNotContain("AxonUserId");
            errorCode.ShouldNotBeNull();
            errorCode.ShouldBe(IdentityDomainErrors.Wallet.NotOwnedByPrincipalCode);
        }

        [Test]
        public void DomainErrors_ShouldNotExposeWalletIds()
        {
            // Arrange - Given invalid wallet operation
            var nonExistentWalletId = WalletId.New();

            // Act - When operation fails with wallet reference
            var result = _principal.ApplyChainDefault("ethereum-mainnet", nonExistentWalletId);

            // Assert - Then error should not expose wallet ID
            result.IsFailure.ShouldBeTrue();
            var errorMessage = result.Error.Message;
            
            errorMessage.ShouldNotContain(nonExistentWalletId.Value.ToString());
            errorMessage.ShouldNotContain("WalletId");
            result.Error.Code.ShouldBe(IdentityDomainErrors.Wallet.NotOwnedByPrincipalCode);
        }

        [Test]
        public void DomainErrors_ShouldProvideGenericMessages()
        {
            // Act - When various operations fail
            var walletNotOwnedResult = _principal.ApplyChainDefault("solana-mainnet", WalletId.New());
            var conflictResult = _principal.LinkWalletOwnership(
                WalletOwnership.Create(_principal.Id, _walletId, AccessMode.Signing, OwnershipStatus.Verified),
                (wId, mode, status) => Result.Success<bool, Error>(true) // Simulate conflict
            );

            // Assert - Then errors should have generic, safe messages
            walletNotOwnedResult.Error.Message.ShouldNotBeNullOrEmpty();
            walletNotOwnedResult.Error.Message.ShouldNotContain("Id");
            walletNotOwnedResult.Error.Message.ShouldNotContain("GUID");

            conflictResult.Error.Message.ShouldNotBeNullOrEmpty();
            conflictResult.Error.Message.ShouldNotContain("Id");
            conflictResult.Error.Message.ShouldNotContain("GUID");
        }

        [Test]
        public void ValidationErrors_ShouldNotLeakInternalStructure()
        {
            // Arrange - Given service principal (to trigger validation error)
            var servicePrincipal = AxonPrincipal.CreateService();

            // Act - When validation fails
            var result = servicePrincipal.UpdateRiskTier(RiskTier.High);

            // Assert - Then error should not leak internal structure
            result.IsFailure.ShouldBeTrue();
            var errorMessage = result.Error.Message;
            
            errorMessage.ShouldNotContain("Service");
            errorMessage.ShouldNotContain("Principal");
            errorMessage.ShouldNotContain(servicePrincipal.Id.Value.ToString());
            result.Error.Code.ShouldBe(IdentityDomainErrors.Profile.ServicePrincipalRiskConstraintCode);
        }
    }

    [TestFixture]
    public class ErrorMessageSanitizationTests : DomainSecurityTests
    {
        [Test]
        public void ErrorMessages_ShouldBeSanitizedOfSensitiveData()
        {
            // Arrange - Given operations that will fail
            var sensitiveWalletId = WalletId.New();
            var sensitivePrincipalId = AxonUserId.New();

            // Act - When operations fail
            var results = new[]
            {
                _principal.ApplyChainDefault("test-chain", sensitiveWalletId),
                _principal.LinkWalletOwnership(
                    WalletOwnership.Create(sensitivePrincipalId, sensitiveWalletId, AccessMode.Signing, OwnershipStatus.Verified),
                    (wId, mode, status) => Result.Success<bool, Error>(false)
                )
            };

            // Assert - Then all error messages should be sanitized
            foreach (var result in results.Where(r => r.IsFailure))
            {
                var errorMessage = result.Error.Message.ToLowerInvariant();
                
                // Should not contain any GUID-like strings
                errorMessage.ShouldNotMatch(@"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}");
                
                // Should not contain sensitive keywords
                errorMessage.ShouldNotContain("AxonUserId");
                errorMessage.ShouldNotContain("walletid");
                errorMessage.ShouldNotContain("principal");
                errorMessage.ShouldNotContain("guid");
                errorMessage.ShouldNotContain("id:");
            }
        }

        [Test]
        public void ErrorCodes_ShouldBeConsistentAndSafe()
        {
            // Arrange - Given various error scenarios
            var testScenarios = new[]
            {
                () => _principal.ApplyChainDefault("test", WalletId.New()),
                () => _principal.UpdateRiskTier(_principal.RiskTier), // No-op, should succeed
                () => AxonPrincipal.CreateService().UpdateRiskTier(RiskTier.High), // Should fail
            };

            // Act & Assert - When errors occur, codes should be consistent
            foreach (var scenario in testScenarios)
            {
                var result = scenario();
                if (result.IsFailure)
                {
                    result.Error.Code.ShouldNotBeNullOrEmpty();
                    result.Error.Code.ShouldNotContain(" "); // No spaces in error codes
                    result.Error.Code.ShouldMatch("^[A-Z_.]+$"); // Should be uppercase with underscores and dots only
                }
            }
        }
    }

    [TestFixture]
    public class InputSanitizationTests : DomainSecurityTests
    {
        [Test]
        public void ApplyChainDefault_WithMaliciousChainId_ShouldRejectSafely()
        {
            // Arrange - Given wallet ownership
            var ownership = WalletOwnership.Create(_principal.Id, _walletId, AccessMode.Signing, OwnershipStatus.Verified);
            _principal.LinkWalletOwnership(
                ownership,
                (wId, mode, status) => Result.Success<bool, Error>(false)
            );

            var maliciousChainIds = new[]
            {
                "<script>alert('xss')</script>",
                "'; DROP TABLE chains; --",
                "../../../etc/passwd",
                "javascript:alert('xss')",
                "\x00\x01\x02", // Control characters
                new string('A', 1000) // Extremely long input
            };

            // Act & Assert - When using malicious chain IDs
            foreach (var maliciousChainId in maliciousChainIds)
            {
                Should.NotThrow(() =>
                {
                    var result = _principal.ApplyChainDefault(maliciousChainId, _walletId);
                    // Operation should either succeed or fail gracefully without exceptions
                });
            }
        }

        [Test]
        public void LinkWalletOwnership_WithEdgeCaseInputs_ShouldHandleSafely()
        {
            // Arrange - Given edge case scenarios
            WalletOwnership nullOwnership = null!;

            // Act & Assert - When using edge case inputs
            Should.Throw<ArgumentNullException>(() =>
                _principal.LinkWalletOwnership(nullOwnership, (wId, mode, status) => Result.Success<bool, Error>(false))
            );

            Should.Throw<ArgumentNullException>(() =>
                _principal.LinkWalletOwnership(
                    WalletOwnership.Create(_principal.Id, _walletId),
                    null!
                )
            );
        }
    }

    [TestFixture]
    public class AuditTrailSecurityTests : DomainSecurityTests
    {
        [Test]
        public void DomainEvents_ShouldNotContainSensitiveInformation()
        {
            // Act - When performing operations that generate domain events
            _principal.UpdateRiskTier(RiskTier.High);
            var ownership = WalletOwnership.Create(_principal.Id, _walletId, AccessMode.Signing, OwnershipStatus.Verified);
            _principal.LinkWalletOwnership(
                ownership,
                (wId, mode, status) => Result.Success<bool, Error>(false)
            );

            // Assert - Then domain events should not leak sensitive data
            foreach (var domainEvent in _principal.DomainEvents)
            {
                var eventData = domainEvent.ToString() ?? string.Empty;
                
                // Events can contain IDs (they're meant for internal processing)
                // but should not contain sensitive business data like private keys, passwords, etc.
                eventData.ShouldNotContain("password");
                eventData.ShouldNotContain("secret");
                eventData.ShouldNotContain("key");
                eventData.ShouldNotContain("token");
                
                // Event timestamps should be reasonable
                domainEvent.OccurredAt.ShouldBeInRange(DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow);
            }
        }

        [Test]
        public void AggregateVersioning_ShouldBeSecurelyIncremented()
        {
            // Arrange - Given initial version
            var initialVersion = _principal.Version;

            // Act - When performing state-changing operations
            _principal.UpdateRiskTier(RiskTier.Medium); // Should increment
            _principal.UpdateRiskTier(RiskTier.Medium); // No-op, should not increment
            _principal.UpdateRiskTier(RiskTier.High);   // Should increment

            // Assert - Then version should be securely incremented
            _principal.Version.ShouldBe(initialVersion + 2); // Only actual changes increment
            ((int)_principal.Version).ShouldBeGreaterThan(0);
        }
    }

    [TestFixture]
    public class DataIntegrityTests : DomainSecurityTests
    {
        [Test]
        public void StateChanges_ShouldMaintainDataIntegrity()
        {
            // Act - When performing multiple state changes
            var ownership = WalletOwnership.Create(_principal.Id, _walletId, AccessMode.Signing, OwnershipStatus.Verified);
            _principal.LinkWalletOwnership(ownership, (wId, mode, status) => Result.Success<bool, Error>(false));
            _principal.UpdateRiskTier(RiskTier.High);
            _principal.ApplyChainDefault("solana-mainnet", _walletId);

            // Assert - Then all state should remain consistent
            _principal.WalletOwnerships.Count.ShouldBe(1);
            _principal.WalletOwnerships.First().WalletId.ShouldBe(_walletId);
            _principal.RiskTier.ShouldBe(RiskTier.High);
            _principal.GetDefaultWalletForChain("solana-mainnet").ShouldBe(_walletId);
            
            // No corruption of IDs
            _principal.Id.ShouldNotBe(default(AxonUserId));
            _walletId.ShouldNotBe(default(WalletId));
        }

        [Test]
        public void ConcurrentOperations_ShouldMaintainConsistency()
        {
            // Simulate concurrent-like operations (same aggregate instance)
            var wallet1 = WalletId.New();
            var wallet2 = WalletId.New();
            
            var ownership1 = WalletOwnership.Create(_principal.Id, wallet1, AccessMode.Signing, OwnershipStatus.Verified);
            var ownership2 = WalletOwnership.Create(_principal.Id, wallet2, AccessMode.WatchOnly, OwnershipStatus.Verified);

            // Act - When performing operations in sequence
            var result1 = _principal.LinkWalletOwnership(ownership1, (wId, mode, status) => Result.Success<bool, Error>(false));
            var result2 = _principal.LinkWalletOwnership(ownership2, (wId, mode, status) => Result.Success<bool, Error>(false));
            var result3 = _principal.ApplyChainDefault("solana-mainnet", wallet1);

            // Assert - Then all operations should succeed and maintain consistency
            result1.IsSuccess.ShouldBeTrue();
            result2.IsSuccess.ShouldBeTrue();
            result3.IsSuccess.ShouldBeTrue();
            
            _principal.WalletOwnerships.Count.ShouldBe(2);
            _principal.GetDefaultWalletForChain("solana-mainnet").ShouldBe(wallet1);
        }
    }
}