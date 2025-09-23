using Axon.Modules.Identity.Application.DTOs.Exchange;
using Axon.Modules.Identity.Application.Commands.ExchangeCredential;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Infrastructure.Persistence.DbInvariants;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Application;

/// <summary>
/// Extended test fixtures for complex resolution scenarios.
/// Provides specialized test data for resolution algorithm and defaults behavior testing.
/// </summary>
public static class ResolutionTestFixtures
{
    #region Dynamic JWT Test Data

    /// <summary>
    /// Test JWT claims for Dynamic provider.
    /// </summary>
    public static class DynamicJWTClaims
    {
        public const string Issuer = TestDataFixtures.DynamicIssuer;
        public const string Audience = "axon-api";
        public const string SubjectA = TestDataFixtures.DynA_Subject;
        public const string SubjectB = "dyn_user_b_67890";
        public const string UnknownSubject = "unknown_subject_99999";

        /// <summary>
        /// Valid JWT expiration time (1 hour from test timestamp).
        /// </summary>
        public static readonly DateTime ValidExpiration = TestDataFixtures.SignatureTestVectors.TestTimestamp.AddHours(1);

        /// <summary>
        /// Expired JWT expiration time.
        /// </summary>
        public static readonly DateTime ExpiredTime = TestDataFixtures.SignatureTestVectors.TestTimestamp.AddHours(-1);
    }

    #endregion

    #region Wallet Signature Test Data

    /// <summary>
    /// Extended signature test vectors for resolution testing.
    /// </summary>
    public static class ExtendedSignatureVectors
    {
        /// <summary>
        /// Valid signature for W1 mainnet wallet.
        /// </summary>
        public const string W1_Mainnet_Valid_Sig = "0x1234567890abcdef_w1_mainnet_valid";

        /// <summary>
        /// Valid signature for W1 devnet wallet (same address, different environment).
        /// </summary>
        public const string W1_Devnet_Valid_Sig = "0x1234567890abcdef_w1_devnet_valid";

        /// <summary>
        /// Valid signature for W2 mainnet wallet.
        /// </summary>
        public const string W2_Mainnet_Valid_Sig = "0xabcdef1234567890_w2_mainnet_valid";

        /// <summary>
        /// Expired signature (TTL exceeded).
        /// </summary>
        public const string Expired_Signature = "0xexpired_signature_past_ttl";

        /// <summary>
        /// Invalid signature format.
        /// </summary>
        public const string Invalid_Signature = "invalid_signature_format";

        /// <summary>
        /// Reused signature (for replay detection testing).
        /// </summary>
        public const string Reused_Signature = "0xreused_signature_should_fail";
    }

    #endregion

    #region Multi-Principal Resolution Scenarios

    /// <summary>
    /// Creates a complex multi-principal scenario for advanced resolution testing.
    /// Tests scenarios with overlapping credentials and wallet ownerships.
    /// </summary>
    public static class MultiPrincipalScenarios
    {
        /// <summary>
        /// Creates scenario with 3 principals having different credential/wallet combinations.
        /// </summary>
        public static (AxonPrincipal credentialPrincipal, AxonPrincipal walletPrincipal, AxonPrincipal ambiguousPrincipal, Wallet sharedWallet)
            CreateComplexResolutionScenario()
        {
            // Principal 1: Has Dynamic credential only
            var credentialPrincipal = TestDataFixtures.CreatePrincipalA(); // Has Dynamic credential

            // Principal 2: Has wallet ownership only (no credentials)
            var walletPrincipal = AxonPrincipal.CreateHuman();
            var sharedWallet = TestDataFixtures.CreateW1Main();

            var verifiedOwnership = TestDataFixtures.CreateVerifiedSigningOwnership(walletPrincipal.Id, sharedWallet.Id);
            walletPrincipal.LinkWalletOwnership(verifiedOwnership, (_, _, _) => Result.Success<bool, Error>(false));

            // Principal 3: Has both credential and wallet ownership with different wallet
            var ambiguousPrincipal = TestDataFixtures.CreatePrincipalB(); // Has different Dynamic credential
            var anotherWallet = TestDataFixtures.CreateW2Main();

            var anotherOwnership = TestDataFixtures.CreateVerifiedSigningOwnership(ambiguousPrincipal.Id, anotherWallet.Id);
            ambiguousPrincipal.LinkWalletOwnership(anotherOwnership, (_, _, _) => Result.Success<bool, Error>(false));

            return (credentialPrincipal, walletPrincipal, ambiguousPrincipal, sharedWallet);
        }

        /// <summary>
        /// Creates scenario for testing authority ranking in ambiguous ownership.
        /// </summary>
        public static (AxonPrincipal signingPrincipal, AxonPrincipal watchOnlyPrincipal, AxonPrincipal pendingPrincipal, Wallet contestedWallet)
            CreateAuthorityRankingScenario()
        {
            var signingPrincipal = AxonPrincipal.CreateHuman();
            var watchOnlyPrincipal = AxonPrincipal.CreateHuman();
            var pendingPrincipal = AxonPrincipal.CreateHuman();
            var contestedWallet = TestDataFixtures.CreateW1Main();

            // Create ownerships with different authorities
            var signingOwnership = WalletOwnership.Create(
                signingPrincipal.Id,
                contestedWallet.Id,
                AccessMode.Signing,
                OwnershipStatus.Pending); // Pending signing (higher authority than watch-only)

            var watchOnlyOwnership = WalletOwnership.Create(
                watchOnlyPrincipal.Id,
                contestedWallet.Id,
                AccessMode.WatchOnly,
                OwnershipStatus.Verified); // Verified watch-only (lower authority)

            var pendingOwnership = WalletOwnership.Create(
                pendingPrincipal.Id,
                contestedWallet.Id,
                AccessMode.Signing,
                OwnershipStatus.Pending); // Another pending signing (tie-breaker needed)

            // Link ownerships
            signingPrincipal.LinkWalletOwnership(signingOwnership, (_, _, _) => Result.Success<bool, Error>(false));
            watchOnlyPrincipal.LinkWalletOwnership(watchOnlyOwnership, (_, _, _) => Result.Success<bool, Error>(false));
            pendingPrincipal.LinkWalletOwnership(pendingOwnership, (_, _, _) => Result.Success<bool, Error>(false));

            return (signingPrincipal, watchOnlyPrincipal, pendingPrincipal, contestedWallet);
        }
    }

    #endregion

    #region Exchange Command Builders

    /// <summary>
    /// Builder for creating complex ExchangeCredentialCommand scenarios.
    /// </summary>
    public static class ExchangeCommandBuilder
    {
        /// <summary>
        /// Creates exchange command with Dynamic JWT and multiple wallets.
        /// </summary>
        public static ExchangeCredentialCommand CreateComplexExchangeCommand(
            string dynamicSubject = DynamicJWTClaims.SubjectA,
            string environmentId = TestDataFixtures.MainnetEnvironment,
            params string[] walletAddresses)
        {
            var wallets = walletAddresses.Select(address => new ExchangeWalletData(
                TestDataFixtures.SolanaMainnetChain,
                address)).ToList();

            return new ExchangeCredentialCommand(
                new ExchangeUserData(
                    AxonUserId: dynamicSubject,
                    Email: "test@example.com",
                    DynamicEnvironmentId: environmentId,
                    Wallets: wallets));
        }

        /// <summary>
        /// Creates exchange command for credential-only resolution.
        /// </summary>
        public static ExchangeCredentialCommand CreateCredentialOnlyCommand(string subject = DynamicJWTClaims.SubjectA)
        {
            return new ExchangeCredentialCommand(
                new ExchangeUserData(
                    AxonUserId: subject,
                    Email: "test@example.com",
                    DynamicEnvironmentId: TestDataFixtures.MainnetEnvironment,
                    Wallets: new List<ExchangeWalletData>()));
        }

        /// <summary>
        /// Creates exchange command for wallet-only resolution.
        /// </summary>
        public static ExchangeCredentialCommand CreateWalletOnlyCommand(params string[] walletAddresses)
        {
            return CreateComplexExchangeCommand(
                DynamicJWTClaims.UnknownSubject, // Unknown credential
                TestDataFixtures.MainnetEnvironment,
                walletAddresses);
        }

        /// <summary>
        /// Creates exchange command for environment-specific testing.
        /// </summary>
        public static ExchangeCredentialCommand CreateEnvironmentSpecificCommand(
            string environmentId,
            string chainId,
            params string[] walletAddresses)
        {
            var wallets = walletAddresses.Select(address => new ExchangeWalletData(
                chainId,
                address)).ToList();

            return new ExchangeCredentialCommand(
                new ExchangeUserData(
                    AxonUserId: DynamicJWTClaims.SubjectA,
                    Email: "test@example.com",
                    DynamicEnvironmentId: environmentId,
                    Wallets: wallets));
        }
    }

    #endregion

    #region Concurrency Test Scenarios

    /// <summary>
    /// Test scenarios for concurrent resolution operations.
    /// </summary>
    public static class ConcurrencyScenarios
    {
        /// <summary>
        /// Creates scenario for testing concurrent wallet verification.
        /// </summary>
        public static (AxonPrincipal principalA, AxonPrincipal principalB, Wallet sharedWallet, ExchangeCredentialCommand commandA, ExchangeCredentialCommand commandB)
            CreateConcurrentVerificationScenario()
        {
            var principalA = AxonPrincipal.CreateHuman();
            var principalB = AxonPrincipal.CreateHuman();
            var sharedWallet = TestDataFixtures.CreateW1Main();

            // Create commands that would both try to verify the same wallet
            var commandA = ExchangeCommandBuilder.CreateWalletOnlyCommand(TestDataFixtures.W1MainAddress);
            var commandB = ExchangeCommandBuilder.CreateWalletOnlyCommand(TestDataFixtures.W1MainAddress);

            return (principalA, principalB, sharedWallet, commandA, commandB);
        }

        /// <summary>
        /// Creates scenario for testing concurrent credential addition.
        /// </summary>
        public static (AxonPrincipal existingPrincipal, ExchangeCredentialCommand duplicateCommand)
            CreateConcurrentCredentialScenario()
        {
            var existingPrincipal = TestDataFixtures.CreatePrincipalA(); // Has Dynamic credential

            // Create command with same credential (should be idempotent)
            var duplicateCommand = ExchangeCommandBuilder.CreateCredentialOnlyCommand(DynamicJWTClaims.SubjectA);

            return (existingPrincipal, duplicateCommand);
        }
    }

    #endregion

    #region Environment Separation Test Data

    /// <summary>
    /// Test data for environment separation validation.
    /// </summary>
    public static class EnvironmentSeparation
    {
        /// <summary>
        /// Creates cross-environment scenario (same address, different environments).
        /// </summary>
        public static (Wallet mainnetWallet, Wallet devnetWallet, ExchangeCredentialCommand mainnetCommand, ExchangeCredentialCommand devnetCommand)
            CreateCrossEnvironmentScenario()
        {
            var mainnetWallet = TestDataFixtures.CreateW1Main();
            var devnetWallet = TestDataFixtures.CreateW1Dev(); // Same address, different environment

            var mainnetCommand = ExchangeCommandBuilder.CreateEnvironmentSpecificCommand(
                TestDataFixtures.MainnetEnvironment,
                TestDataFixtures.SolanaMainnetChain,
                TestDataFixtures.W1MainAddress);

            var devnetCommand = ExchangeCommandBuilder.CreateEnvironmentSpecificCommand(
                TestDataFixtures.DevnetEnvironment,
                TestDataFixtures.SolanaDevnetChain,
                TestDataFixtures.W1DevAddress);

            return (mainnetWallet, devnetWallet, mainnetCommand, devnetCommand);
        }

        /// <summary>
        /// Creates mixed environment command (should be invalid).
        /// </summary>
        public static ExchangeCredentialCommand CreateMixedEnvironmentCommand()
        {
            // Invalid: mainnet environment with devnet wallet
            return ExchangeCommandBuilder.CreateEnvironmentSpecificCommand(
                TestDataFixtures.MainnetEnvironment, // Mainnet environment
                TestDataFixtures.SolanaDevnetChain,  // But devnet chain
                TestDataFixtures.W1DevAddress);      // And devnet address
        }
    }

    #endregion

    #region Performance Test Data

    /// <summary>
    /// Test data for performance and stress testing.
    /// </summary>
    public static class PerformanceTestData
    {
        /// <summary>
        /// Creates scenario with many wallets for batch operation testing.
        /// </summary>
        public static (AxonPrincipal principal, List<Wallet> wallets, ExchangeCredentialCommand command)
            CreateBatchWalletScenario(int walletCount = 10)
        {
            var principal = AxonPrincipal.CreateHuman();
            var wallets = new List<Wallet>();
            var walletAddresses = new List<string>();

            for (int i = 0; i < walletCount; i++)
            {
                // Generate valid Solana Base58 address (32-44 chars, no 0/O/I/l)
                var address = GenerateValidSolanaAddress(i);
                var wallet = TestDataFixtures.CreateCustomWallet(TestDataFixtures.SolanaMainnetChain, address);
                wallets.Add(wallet);
                walletAddresses.Add(address);

                // Link wallet to principal
                var ownership = TestDataFixtures.CreateVerifiedSigningOwnership(principal.Id, wallet.Id);
                principal.LinkWalletOwnership(ownership, (_, _, _) => Result.Success<bool, Error>(false));
            }

            var command = ExchangeCommandBuilder.CreateWalletOnlyCommand(walletAddresses.ToArray());

            return (principal, wallets, command);
        }

        /// <summary>
        /// Generates a valid Solana Base58 address for testing.
        /// Base58 excludes: 0 (zero), O (capital o), I (capital i), l (lowercase L)
        /// </summary>
        public static string GenerateValidSolanaAddress(int index)
        {
            // Valid Base58 characters for Solana addresses
            const string base58Chars = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz";

            // Create deterministic address based on index for test consistency
            var random = new Random(index + 12345); // Seed for deterministic generation
            var length = 44; // Standard Solana address length
            var chars = new char[length];

            for (int i = 0; i < length; i++)
            {
                chars[i] = base58Chars[random.Next(base58Chars.Length)];
            }

            return new string(chars);
        }

        /// <summary>
        /// Creates scenario with edge case wallet addresses.
        /// </summary>
        public static ExchangeCredentialCommand CreateEdgeCaseAddressCommand()
        {
            var edgeCaseAddresses = new[]
            {
                "A".PadRight(44, '1'), // Minimum valid length
                "Z".PadRight(44, '9'), // Maximum valid characters
                new string('x', 44),   // All same character
                "1111111111111111111111111111111111111111111", // Mostly 1s
                "9999999999999999999999999999999999999999999"  // Mostly 9s
            };

            return ExchangeCommandBuilder.CreateWalletOnlyCommand(edgeCaseAddresses);
        }
    }

    #endregion

    #region Error Scenario Test Data

    /// <summary>
    /// Test data for error scenarios and edge cases.
    /// </summary>
    public static class ErrorScenarios
    {
        /// <summary>
        /// Creates command with invalid wallet addresses.
        /// </summary>
        public static ExchangeCredentialCommand CreateInvalidAddressCommand()
        {
            var invalidAddresses = new[]
            {
                "", // Empty address
                "too_short", // Too short
                new string('a', 100), // Too long
                "invalid!@#$%^&*()characters", // Invalid characters
                null // Null address (if allowed by DTO)
            };

            return ExchangeCommandBuilder.CreateWalletOnlyCommand(
                invalidAddresses.Where(a => a != null).ToArray()!);
        }

        /// <summary>
        /// Creates command with conflicting wallet ownership.
        /// </summary>
        public static (AxonPrincipal ownerPrincipal, Wallet ownedWallet, ExchangeCredentialCommand conflictCommand)
            CreateOwnershipConflictScenario()
        {
            var ownerPrincipal = AxonPrincipal.CreateHuman();
            var ownedWallet = TestDataFixtures.CreateW1Main();

            // Link wallet to owner
            var ownership = TestDataFixtures.CreateVerifiedSigningOwnership(ownerPrincipal.Id, ownedWallet.Id);
            ownerPrincipal.LinkWalletOwnership(ownership, (_, _, _) => Result.Success<bool, Error>(false));

            // Create command from different user trying to claim same wallet
            var conflictCommand = ExchangeCommandBuilder.CreateWalletOnlyCommand(TestDataFixtures.W1MainAddress);

            return (ownerPrincipal, ownedWallet, conflictCommand);
        }

        /// <summary>
        /// Creates command exceeding wallet limits.
        /// </summary>
        public static ExchangeCredentialCommand CreateWalletLimitExceededCommand()
        {
            // Create command with more wallets than allowed (assume 10 is the limit)
            var manyAddresses = Enumerable.Range(1, 15)
                .Select(i => PerformanceTestData.GenerateValidSolanaAddress(i + 1000)) // Use +1000 to avoid collision with batch scenario
                .ToArray();

            return ExchangeCommandBuilder.CreateWalletOnlyCommand(manyAddresses);
        }
    }

    #endregion
}