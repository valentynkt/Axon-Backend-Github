using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Infrastructure.Tests.Persistence.DbInvariants;

/// <summary>
/// DB Invariant tests for Identity Module as specified in TDD document section A.
/// These tests validate that database constraints alone prevent identity violations.
/// Tests are designed to initially fail (TDD approach) to expose gaps in current implementation.
/// </summary>
[TestFixture]
public class IdentityDbInvariantsTests : IdentityDbInvariantsTestBase
{
    #region Test 1: WALLET_UNIQUE_chain_address

    [Test]
    public async Task Test_WALLET_UNIQUE_chain_address_SameChainAddress_ShouldFail()
    {
        // Arrange: Create two wallets with same chain and address
        var wallet1 = TestDataFixtures.CreateW1Main();
        var wallet2 = TestDataFixtures.CreateW1Main(); // Same chain/address

        // Act: Insert first wallet (should succeed)
        await WalletRepository.AddAsync(wallet1);
        await UnitOfWork.SaveChangesAsync();

        // Try to insert duplicate
        await WalletRepository.AddAsync(wallet2);

        // Assert: Should violate unique constraint
        await AssertPostgreSQLConstraintViolation(
            async () => await UnitOfWork.SaveChangesAsync(),
            "ux_wallet_chain_addr"
        );
    }

    [Test]
    public async Task Test_WALLET_UNIQUE_chain_address_DifferentChains_ShouldSucceed()
    {
        // Arrange: Create wallets with same address but different chains
        var walletMainnet = TestDataFixtures.CreateW1Main();
        var walletDevnet = TestDataFixtures.CreateW1Dev(); // Same address, different chain

        // Act: Insert both wallets
        await WalletRepository.AddAsync(walletMainnet);
        await WalletRepository.AddAsync(walletDevnet);

        // Assert: Should succeed (different chains allowed)
        await UnitOfWork.SaveChangesAsync(); // Should not throw
    }

    #endregion

    #region Test 2: CREDENTIAL_UNIQUE_provider_issuer_subject

    [Test]
    public async Task Test_CREDENTIAL_UNIQUE_provider_issuer_subject_SameTuple_ShouldFail()
    {
        // Arrange: Create plain principal (no credentials)
        var principal = TestDataFixtures.CreatePlainPrincipal();

        // Add first credential through aggregate
        var firstCredential = TestDataFixtures.CreateDynACredential(principal.Id);
        principal.AddCredential(firstCredential, (_, _, _) => Result.Success<bool, Error>(false));

        await PrincipalRepository.AddAsync(principal);
        await UnitOfWork.SaveChangesAsync();

        // Act: Try to create duplicate credential tuple for another principal
        var principal2 = TestDataFixtures.CreatePlainPrincipal();
        var duplicateCredential = TestDataFixtures.CreateDynACredential(principal2.Id);
        principal2.AddCredential(duplicateCredential, (_, _, _) => Result.Success<bool, Error>(false));

        await PrincipalRepository.AddAsync(principal2);

        // Assert: Should violate unique constraint
        await AssertPostgreSQLConstraintViolation(
            async () => await UnitOfWork.SaveChangesAsync(),
            "ux_credential_provider"
        );
    }

    [Test]
    public async Task Test_CREDENTIAL_UNIQUE_provider_issuer_subject_DifferentSubject_ShouldSucceed()
    {
        // Arrange: Create plain principal (no credentials)
        var principal = TestDataFixtures.CreatePlainPrincipal();

        // Act: Create credentials for same provider/issuer but different subjects
        var credentialA = TestDataFixtures.CreateDynACredential(principal.Id);
        var credentialB = TestDataFixtures.CreateDynBCredential(principal.Id); // Different subject

        principal.AddCredential(credentialA, (_, _, _) => Result.Success<bool, Error>(false));
        principal.AddCredential(credentialB, (_, _, _) => Result.Success<bool, Error>(false));

        await PrincipalRepository.AddAsync(principal);

        // Assert: Should succeed (different subjects allowed)
        await UnitOfWork.SaveChangesAsync(); // Should not throw
    }

    #endregion

    #region Test 3: OWNERSHIP_PAIR_UNIQUE

    [Test]
    public async Task Test_OWNERSHIP_PAIR_UNIQUE_SamePrincipalWallet_ShouldFail()
    {
        // Arrange: Create principal and wallet
        var principal = TestDataFixtures.CreatePrincipalA();
        var wallet = TestDataFixtures.CreateW1Main();

        // Add first ownership to principal before saving
        var ownership1 = TestDataFixtures.CreateVerifiedSigningOwnership(principal.Id, wallet.Id);
        principal.LinkWalletOwnership(ownership1, (_, _, _) => Result.Success<bool, Error>(false));

        await PrincipalRepository.AddAsync(principal);
        await WalletRepository.AddAsync(wallet);
        await UnitOfWork.SaveChangesAsync();

        // Act: Try to directly manipulate data to create duplicate ownership pair
        // This simulates a data integrity violation scenario
        var duplicateInsert = @"INSERT INTO identity.""WalletOwnership""
                                 (principal_id, id, wallet_id, access_mode, status, verification_source, created_at, updated_at, is_deleted)
                                 VALUES (@principalId, @id, @walletId, 'Signing', 'Pending', 'Manual', NOW(), NOW(), false)";

        // Assert: Should violate unique constraint for (principal_id, wallet_id) (using Raw assertion for direct SQL)
        await AssertPostgreSQLConstraintViolationRaw(
            async () => await DbContext.Database.ExecuteSqlRawAsync(
                duplicateInsert,
                new NpgsqlParameter("@principalId", principal.Id.Value),
                new NpgsqlParameter("@id", Guid.NewGuid()),
                new NpgsqlParameter("@walletId", wallet.Id.Value)),
            "ux_ownership_pair"
        );
    }

    [Test]
    public async Task Test_OWNERSHIP_PAIR_UNIQUE_DifferentPairs_ShouldSucceed()
    {
        // Arrange: Create principals and wallets
        var (principalA, principalB, wallet) = TestDataFixtures.CreateExclusivityScenario();
        var wallet2 = TestDataFixtures.CreateW2Main();

        // Act: Create different ownership pairs through aggregates
        var ownership1 = TestDataFixtures.CreateVerifiedSigningOwnership(principalA.Id, wallet.Id);
        var ownership2 = TestDataFixtures.CreateVerifiedSigningOwnership(principalB.Id, wallet2.Id);
        var ownership3 = TestDataFixtures.CreateVerifiedWatchOnlyOwnership(principalA.Id, wallet2.Id);

        principalA.LinkWalletOwnership(ownership1, (_, _, _) => Result.Success<bool, Error>(false));
        principalA.LinkWalletOwnership(ownership3, (_, _, _) => Result.Success<bool, Error>(false));
        principalB.LinkWalletOwnership(ownership2, (_, _, _) => Result.Success<bool, Error>(false));

        await PrincipalRepository.AddAsync(principalA);
        await PrincipalRepository.AddAsync(principalB);
        await WalletRepository.AddAsync(wallet);
        await WalletRepository.AddAsync(wallet2);

        // Assert: Should succeed (different pairs)
        await UnitOfWork.SaveChangesAsync(); // Should not throw
    }

    #endregion

    #region Test 4: EXCLUSIVITY_PARTIAL_UNIQUE_verified_signing

    [Test]
    public async Task Test_EXCLUSIVITY_PARTIAL_UNIQUE_verified_signing_TwoVerifiedSigning_ShouldFail()
    {
        // Arrange: Create two principals and one shared wallet
        var (principalA, principalB, sharedWallet) = TestDataFixtures.CreateExclusivityScenario();

        // Give first principal verified signing ownership
        var ownership1 = TestDataFixtures.CreateVerifiedSigningOwnership(principalA.Id, sharedWallet.Id);
        principalA.LinkWalletOwnership(ownership1, (_, _, _) => Result.Success<bool, Error>(false));

        await PrincipalRepository.AddAsync(principalA);
        await PrincipalRepository.AddAsync(principalB);
        await WalletRepository.AddAsync(sharedWallet);
        await UnitOfWork.SaveChangesAsync();

        // Act: Try to give second principal also verified signing ownership via direct SQL
        // This simulates a scenario where constraint must be enforced at database level
        var duplicateInsert = @"INSERT INTO identity.""WalletOwnership""
                                 (principal_id, id, wallet_id, access_mode, status, verification_source, created_at, updated_at, is_deleted)
                                 VALUES (@principalId, @id, @walletId, 'Signing', 'Verified', 'Manual', NOW(), NOW(), false)";

        // Assert: Should fail due to partial unique index constraint (using Raw assertion for direct SQL)
        await AssertPartialUniqueIndexViolationRaw(
            async () => await DbContext.Database.ExecuteSqlRawAsync(
                duplicateInsert,
                new NpgsqlParameter("@principalId", principalB.Id.Value),
                new NpgsqlParameter("@id", Guid.NewGuid()),
                new NpgsqlParameter("@walletId", sharedWallet.Id.Value)),
            "ux_exclusive_signing"
        );
    }

    [Test]
    public async Task Test_EXCLUSIVITY_PARTIAL_UNIQUE_verified_signing_VerifiedSigningAndWatchOnly_ShouldSucceed()
    {
        // Arrange: Create two principals and one shared wallet
        var (principalA, principalB, sharedWallet) = TestDataFixtures.CreateExclusivityScenario();

        // Act: Give first principal verified signing, second principal verified watch-only
        var ownershipSigning = TestDataFixtures.CreateVerifiedSigningOwnership(principalA.Id, sharedWallet.Id);
        var ownershipWatchOnly = TestDataFixtures.CreateVerifiedWatchOnlyOwnership(principalB.Id, sharedWallet.Id);

        principalA.LinkWalletOwnership(ownershipSigning, (_, _, _) => Result.Success<bool, Error>(false));
        principalB.LinkWalletOwnership(ownershipWatchOnly, (_, _, _) => Result.Success<bool, Error>(false));

        await PrincipalRepository.AddAsync(principalA);
        await PrincipalRepository.AddAsync(principalB);
        await WalletRepository.AddAsync(sharedWallet);

        // Assert: Should succeed (partial index only applies to verified+signing)
        await UnitOfWork.SaveChangesAsync(); // Should not throw
    }

    [Test]
    public async Task Test_EXCLUSIVITY_PARTIAL_UNIQUE_verified_signing_ConcurrentVerification_ShouldFail()
    {
        // Arrange: Create two principals and one shared wallet
        var (principalA, principalB, sharedWallet) = TestDataFixtures.CreateExclusivityScenario();

        await PrincipalRepository.AddAsync(principalA);
        await PrincipalRepository.AddAsync(principalB);
        await WalletRepository.AddAsync(sharedWallet);
        await UnitOfWork.SaveChangesAsync();

        // Act: Try to verify same wallet for two principals concurrently via direct SQL
        var (exception1, exception2) = await ExecuteConcurrentOperations(
            async context1 =>
            {
                var insertSql = @"INSERT INTO identity.""WalletOwnership""
                                 (principal_id, id, wallet_id, access_mode, status, verification_source, created_at, updated_at, is_deleted)
                                 VALUES (@principalId, @id, @walletId, 'Signing', 'Verified', 'Manual', NOW(), NOW(), false)";
                await context1.Database.ExecuteSqlRawAsync(
                    insertSql,
                    new NpgsqlParameter("@principalId", principalA.Id.Value),
                    new NpgsqlParameter("@id", Guid.NewGuid()),
                    new NpgsqlParameter("@walletId", sharedWallet.Id.Value));
            },
            async context2 =>
            {
                var insertSql = @"INSERT INTO identity.""WalletOwnership""
                                 (principal_id, id, wallet_id, access_mode, status, verification_source, created_at, updated_at, is_deleted)
                                 VALUES (@principalId, @id, @walletId, 'Signing', 'Verified', 'Manual', NOW(), NOW(), false)";
                await context2.Database.ExecuteSqlRawAsync(
                    insertSql,
                    new NpgsqlParameter("@principalId", principalB.Id.Value),
                    new NpgsqlParameter("@id", Guid.NewGuid()),
                    new NpgsqlParameter("@walletId", sharedWallet.Id.Value));
            });

        // Assert: Exactly one should succeed, the other should fail
        var exceptions = new[] { exception1, exception2 }.Where(e => e != null).ToArray();
        exceptions.Length.ShouldBe(1, "Exactly one concurrent operation should fail");

        var failedException = exceptions.First();
        // Raw SQL operations throw PostgresException directly
        failedException.ShouldBeOfType<PostgresException>();
        var postgresException = (PostgresException)failedException;
        postgresException.SqlState.ShouldBe("23505", "Expected unique constraint violation");
    }

    #endregion

    #region Test 5: DEFAULT_UNIQUE_per_principal_chain

    [Test]
    public async Task Test_DEFAULT_UNIQUE_per_principal_chain_SamePrincipalChain_ShouldFail()
    {
        // Arrange: Create principal and two wallets for same chain
        var principal = TestDataFixtures.CreatePrincipalA();
        var wallet1 = TestDataFixtures.CreateW1Main();
        var wallet2 = TestDataFixtures.CreateW2Main();

        // Create verified signing ownerships for both wallets
        var ownership1 = TestDataFixtures.CreateVerifiedSigningOwnership(principal.Id, wallet1.Id);
        var ownership2 = TestDataFixtures.CreateVerifiedSigningOwnership(principal.Id, wallet2.Id);

        principal.LinkWalletOwnership(ownership1, (_, _, _) => Result.Success<bool, Error>(false));
        principal.LinkWalletOwnership(ownership2, (_, _, _) => Result.Success<bool, Error>(false));

        // Set first wallet as default for mainnet
        principal.ApplyChainDefault(TestDataFixtures.SolanaMainnetChain, wallet1.Id);

        await PrincipalRepository.AddAsync(principal);
        await WalletRepository.AddAsync(wallet1);
        await WalletRepository.AddAsync(wallet2);
        await UnitOfWork.SaveChangesAsync();

        // Act: Try to set second default for same principal/chain via direct SQL
        var duplicateInsert = @"INSERT INTO identity.""PrincipalChainDefault""
                                 (principal_id, id, chain_id, wallet_id, created_at, updated_at, is_deleted)
                                 VALUES (@principalId, @id, @chainId, @walletId, NOW(), NOW(), false)";

        // Assert: Should fail due to unique constraint (using Raw assertion for direct SQL)
        await AssertPostgreSQLConstraintViolationRaw(
            async () => await DbContext.Database.ExecuteSqlRawAsync(
                duplicateInsert,
                new NpgsqlParameter("@principalId", principal.Id.Value),
                new NpgsqlParameter("@id", Guid.NewGuid()),
                new NpgsqlParameter("@chainId", TestDataFixtures.SolanaMainnetChain),
                new NpgsqlParameter("@walletId", wallet2.Id.Value)),
            "ux_chain_default"
        );
    }

    [Test]
    public async Task Test_DEFAULT_UNIQUE_per_principal_chain_DifferentChains_ShouldSucceed()
    {
        // Arrange: Create principal and wallets for different chains
        var principal = TestDataFixtures.CreatePrincipalA();
        var walletMainnet = TestDataFixtures.CreateW1Main();
        var walletDevnet = TestDataFixtures.CreateW1Dev();

        // Create verified signing ownerships
        var ownershipMainnet = TestDataFixtures.CreateVerifiedSigningOwnership(principal.Id, walletMainnet.Id);
        var ownershipDevnet = TestDataFixtures.CreateVerifiedSigningOwnership(principal.Id, walletDevnet.Id);

        principal.LinkWalletOwnership(ownershipMainnet, (_, _, _) => Result.Success<bool, Error>(false));
        principal.LinkWalletOwnership(ownershipDevnet, (_, _, _) => Result.Success<bool, Error>(false));

        // Act: Set defaults for different chains
        principal.ApplyChainDefault(TestDataFixtures.SolanaMainnetChain, walletMainnet.Id);
        principal.ApplyChainDefault(TestDataFixtures.SolanaDevnetChain, walletDevnet.Id);

        await PrincipalRepository.AddAsync(principal);
        await WalletRepository.AddAsync(walletMainnet);
        await WalletRepository.AddAsync(walletDevnet);

        // Assert: Should succeed (different chains)
        await UnitOfWork.SaveChangesAsync(); // Should not throw
    }

    #endregion

    #region Test 6: DEFAULT_GUARD_verified_signing_only

    [Test]
    public async Task Test_DEFAULT_GUARD_verified_signing_only_WatchOnlyWallet_ShouldSucceed()
    {
        // Arrange: Create principal with watch-only wallet
        var principal = TestDataFixtures.CreatePrincipalA();
        var wallet = TestDataFixtures.CreateW1Main();

        // Create watch-only ownership
        var ownership = TestDataFixtures.CreateVerifiedWatchOnlyOwnership(principal.Id, wallet.Id);
        principal.LinkWalletOwnership(ownership, (_, _, _) => Result.Success<bool, Error>(false));

        await PrincipalRepository.AddAsync(principal);
        await WalletRepository.AddAsync(wallet);
        await UnitOfWork.SaveChangesAsync();

        // Act: Try to set watch-only wallet as default via direct SQL
        // This bypasses domain validation to test database constraints
        var insertSql = @"INSERT INTO identity.""PrincipalChainDefault""
                         (principal_id, id, chain_id, wallet_id, created_at, updated_at, is_deleted)
                         VALUES (@principalId, @id, @chainId, @walletId, NOW(), NOW(), false)";

        await DbContext.Database.ExecuteSqlRawAsync(
            insertSql,
            new NpgsqlParameter("@principalId", principal.Id.Value),
            new NpgsqlParameter("@id", Guid.NewGuid()),
            new NpgsqlParameter("@chainId", TestDataFixtures.SolanaMainnetChain),
            new NpgsqlParameter("@walletId", wallet.Id.Value));

        // Assert: Should succeed (check constraint enforcement moved to domain layer)
        await UnitOfWork.SaveChangesAsync(); // Should not throw
    }

    [Test]
    public async Task Test_DEFAULT_GUARD_verified_signing_only_PendingWallet_ShouldSucceed()
    {
        // Arrange: Create principal with pending wallet
        var principal = TestDataFixtures.CreatePrincipalA();
        var wallet = TestDataFixtures.CreateW1Main();

        // Create pending ownership
        var ownership = TestDataFixtures.CreatePendingSigningOwnership(principal.Id, wallet.Id);
        principal.LinkWalletOwnership(ownership, (_, _, _) => Result.Success<bool, Error>(false));

        await PrincipalRepository.AddAsync(principal);
        await WalletRepository.AddAsync(wallet);
        await UnitOfWork.SaveChangesAsync();

        // Act: Try to set pending wallet as default via direct SQL
        // This bypasses domain validation to test database constraints
        var insertSql = @"INSERT INTO identity.""PrincipalChainDefault""
                         (principal_id, id, chain_id, wallet_id, created_at, updated_at, is_deleted)
                         VALUES (@principalId, @id, @chainId, @walletId, NOW(), NOW(), false)";

        await DbContext.Database.ExecuteSqlRawAsync(
            insertSql,
            new NpgsqlParameter("@principalId", principal.Id.Value),
            new NpgsqlParameter("@id", Guid.NewGuid()),
            new NpgsqlParameter("@chainId", TestDataFixtures.SolanaMainnetChain),
            new NpgsqlParameter("@walletId", wallet.Id.Value));

        // Assert: Should succeed (check constraint enforcement moved to domain layer)
        await UnitOfWork.SaveChangesAsync(); // Should not throw
    }

    [Test]
    public async Task Test_DEFAULT_GUARD_verified_signing_only_VerifiedSigningWallet_ShouldSucceed()
    {
        // Arrange: Create principal with verified signing wallet
        var principal = TestDataFixtures.CreatePrincipalA();
        var wallet = TestDataFixtures.CreateW1Main();

        // Create verified signing ownership
        var ownership = TestDataFixtures.CreateVerifiedSigningOwnership(principal.Id, wallet.Id);
        principal.LinkWalletOwnership(ownership, (_, _, _) => Result.Success<bool, Error>(false));

        // Set as default through domain (this validates at domain layer)
        principal.ApplyChainDefault(TestDataFixtures.SolanaMainnetChain, wallet.Id);

        await PrincipalRepository.AddAsync(principal);
        await WalletRepository.AddAsync(wallet);

        // Assert: Should succeed
        await UnitOfWork.SaveChangesAsync(); // Should not throw
    }

    #endregion

    #region Integration Tests

    [Test]
    public async Task Integration_CompleteWalletLifecycle_ShouldRespectAllConstraints()
    {
        // Arrange: Create complete scenario
        var principal = TestDataFixtures.CreatePrincipalA();
        var wallet = TestDataFixtures.CreateW1Main();
        var ownership = TestDataFixtures.CreateVerifiedSigningOwnership(principal.Id, wallet.Id);

        // Act & Assert: Step-by-step validation of all constraints

        // 1. Link ownership to principal
        principal.LinkWalletOwnership(ownership, (_, _, _) => Result.Success<bool, Error>(false));

        // 2. Set as default (should succeed - verified signing)
        principal.ApplyChainDefault(TestDataFixtures.SolanaMainnetChain, wallet.Id);

        // 3. Save principal and wallet
        await PrincipalRepository.AddAsync(principal);
        await WalletRepository.AddAsync(wallet);
        await UnitOfWork.SaveChangesAsync();

        // 4. Verify all constraints are enforced in complete scenario
        ClearChangeTracker(); // Force fresh query
        var loadedPrincipal = await PrincipalRepository.GetByIdAsync(principal.Id);
        loadedPrincipal.ShouldNotBeNull();

        // Check aggregate state
        var activeOwnerships = loadedPrincipal.GetActiveWalletOwnerships().ToList();
        var activeDefaults = loadedPrincipal.GetActivePrincipalChainDefaults().ToList();

        activeOwnerships.Count.ShouldBe(1);
        activeDefaults.Count.ShouldBe(1);
    }

    #endregion
}