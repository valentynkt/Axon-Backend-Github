using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using NUnit.Framework;

namespace Axon.Modules.Identity.Domain.Tests.TestData;

/// <summary>
/// Factory class providing parameterized test data for Identity Domain tests.
/// Implements shared test data sets following 20/80 principle to reduce duplication.
/// </summary>
public static class TestDataFactory
{
    #region Principal Type Test Cases

    /// <summary>
    /// Provides test cases for different principal types with expected defaults.
    /// </summary>
    public static IEnumerable<TestCaseData> PrincipalTypeTestCases()
    {
        yield return new TestCaseData(PrincipalType.Human, RiskTier.Low)
            .SetName("Human Principal with Low Risk");
        yield return new TestCaseData(PrincipalType.Service, RiskTier.Low)
            .SetName("Service Principal with Low Risk");
    }

    /// <summary>
    /// Provides test cases for risk tier updates with principal type constraints.
    /// </summary>
    public static IEnumerable<TestCaseData> RiskTierUpdateTestCases()
    {
        // Valid updates for Human principals
        yield return new TestCaseData(PrincipalType.Human, RiskTier.Low, RiskTier.Medium, true)
            .SetName("Human: Low to Medium - Should Succeed");
        yield return new TestCaseData(PrincipalType.Human, RiskTier.Medium, RiskTier.High, true)
            .SetName("Human: Medium to High - Should Succeed");
        yield return new TestCaseData(PrincipalType.Human, RiskTier.High, RiskTier.Low, true)
            .SetName("Human: High to Low - Should Succeed");

        // Valid updates for Service principals (only Low allowed)
        yield return new TestCaseData(PrincipalType.Service, RiskTier.Low, RiskTier.Low, true)
            .SetName("Service: Low to Low - Should Succeed (No-op)");

        // Invalid updates for Service principals
        yield return new TestCaseData(PrincipalType.Service, RiskTier.Low, RiskTier.Medium, false)
            .SetName("Service: Low to Medium - Should Fail");
        yield return new TestCaseData(PrincipalType.Service, RiskTier.Low, RiskTier.High, false)
            .SetName("Service: Low to High - Should Fail");
    }

    #endregion

    #region Address Validation Test Cases

    /// <summary>
    /// Provides test cases for valid addresses across different chains.
    /// </summary>
    public static IEnumerable<TestCaseData> ValidAddressTestCases()
    {
        // Solana addresses
        yield return new TestCaseData("9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM", TestConstants.SolanaChain)
            .SetName("Valid Solana Address");
        yield return new TestCaseData("So11111111111111111111111111111111111111112", TestConstants.SolanaChain)
            .SetName("Valid Solana Token Address");
        
        // Ethereum addresses
        yield return new TestCaseData("0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e4d", TestConstants.EthereumChain)
            .SetName("Valid Ethereum Address");
        yield return new TestCaseData("0xA0b86a33E6329C96B5E5a25A1E07c394bc76E5D7", TestConstants.EthereumChain)
            .SetName("Valid Ethereum Contract Address");
    }

    /// <summary>
    /// Provides test cases for invalid addresses that should fail validation.
    /// </summary>
    public static IEnumerable<TestCaseData> InvalidAddressTestCases()
    {
        yield return new TestCaseData("", "Invalid: Empty Address")
            .SetName("Empty Address");
        yield return new TestCaseData("   ", "Invalid: Whitespace Only")
            .SetName("Whitespace Address");
        yield return new TestCaseData("invalid_address", "Invalid: Not Base58 or Hex")
            .SetName("Invalid Format Address");
        yield return new TestCaseData("0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e4", "Invalid: Short Ethereum")
            .SetName("Short Ethereum Address");
        yield return new TestCaseData("9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWW", "Invalid: Short Solana")
            .SetName("Short Solana Address");
    }

    #endregion

    #region Chain ID Validation Test Cases

    /// <summary>
    /// Provides test cases for valid chain IDs.
    /// </summary>
    public static IEnumerable<TestCaseData> ValidChainIdTestCases()
    {
        yield return new TestCaseData("solana-mainnet")
            .SetName("Solana Mainnet");
        yield return new TestCaseData("ethereum-mainnet")
            .SetName("Ethereum Mainnet");
        yield return new TestCaseData("polygon-mainnet")
            .SetName("Polygon Mainnet");
        yield return new TestCaseData("arbitrum-mainnet")
            .SetName("Arbitrum Mainnet");
        yield return new TestCaseData("avalanche-mainnet")
            .SetName("Avalanche Mainnet");
    }

    /// <summary>
    /// Provides test cases for invalid chain IDs.
    /// </summary>
    public static IEnumerable<TestCaseData> InvalidChainIdTestCases()
    {
        yield return new TestCaseData("", "Empty chain ID")
            .SetName("Empty Chain ID");
        yield return new TestCaseData("   ", "Whitespace chain ID")
            .SetName("Whitespace Chain ID");
        yield return new TestCaseData("ETHEREUM-MAINNET", "Uppercase not allowed")
            .SetName("Uppercase Chain ID");
        yield return new TestCaseData("ethereum_mainnet", "Underscore not allowed")
            .SetName("Underscore Chain ID");
    }

    #endregion

    #region Wallet Ownership Test Cases

    /// <summary>
    /// Provides test cases for wallet ownership scenarios.
    /// </summary>
    public static IEnumerable<TestCaseData> OwnershipScenarios()
    {
        yield return new TestCaseData(AccessMode.Signing, OwnershipStatus.Pending, true)
            .SetName("Signing + Pending - Valid");
        yield return new TestCaseData(AccessMode.Signing, OwnershipStatus.Verified, true)
            .SetName("Signing + Verified - Valid");
        yield return new TestCaseData(AccessMode.Signing, OwnershipStatus.Revoked, true)
            .SetName("Signing + Revoked - Valid");
            
        yield return new TestCaseData(AccessMode.WatchOnly, OwnershipStatus.Pending, true)
            .SetName("WatchOnly + Pending - Valid");
        yield return new TestCaseData(AccessMode.WatchOnly, OwnershipStatus.Verified, true)
            .SetName("WatchOnly + Verified - Valid");
        yield return new TestCaseData(AccessMode.WatchOnly, OwnershipStatus.Revoked, true)
            .SetName("WatchOnly + Revoked - Valid");
    }

    /// <summary>
    /// Provides test cases for ownership conflict scenarios.
    /// </summary>
    public static IEnumerable<TestCaseData> OwnershipConflictScenarios()
    {
        // Scenarios that should create conflicts (single verified signing owner rule)
        yield return new TestCaseData(AccessMode.Signing, OwnershipStatus.Verified, AccessMode.Signing, OwnershipStatus.Verified, true)
            .SetName("Two Verified Signing - Should Conflict");
            
        // Scenarios that should NOT create conflicts
        yield return new TestCaseData(AccessMode.Signing, OwnershipStatus.Verified, AccessMode.WatchOnly, OwnershipStatus.Verified, false)
            .SetName("Verified Signing + Verified WatchOnly - No Conflict");
        yield return new TestCaseData(AccessMode.WatchOnly, OwnershipStatus.Verified, AccessMode.WatchOnly, OwnershipStatus.Verified, false)
            .SetName("Two Verified WatchOnly - No Conflict");
        yield return new TestCaseData(AccessMode.Signing, OwnershipStatus.Verified, AccessMode.Signing, OwnershipStatus.Pending, false)
            .SetName("Verified Signing + Pending Signing - No Conflict");
        yield return new TestCaseData(AccessMode.Signing, OwnershipStatus.Pending, AccessMode.Signing, OwnershipStatus.Pending, false)
            .SetName("Two Pending Signing - No Conflict");
    }

    #endregion

    #region Provider and Credential Test Cases

    /// <summary>
    /// Provides test cases for credential providers.
    /// </summary>
    public static IEnumerable<TestCaseData> CredentialProviderTestCases()
    {
        yield return new TestCaseData("dynamic", "issuer1", "subject1")
            .SetName("Dynamic Provider");
        yield return new TestCaseData("auth0", "issuer2", "subject2")
            .SetName("Auth0 Provider");
        yield return new TestCaseData("okta", "issuer3", "subject3")
            .SetName("Okta Provider");
    }

    /// <summary>
    /// Provides test cases for invalid credential data.
    /// </summary>
    public static IEnumerable<TestCaseData> InvalidCredentialTestCases()
    {
        yield return new TestCaseData("", "issuer", "subject", "Empty provider")
            .SetName("Empty Provider");
        yield return new TestCaseData("provider", "", "subject", "Empty issuer")
            .SetName("Empty Issuer");
        yield return new TestCaseData("provider", "issuer", "", "Empty subject")
            .SetName("Empty Subject");
        yield return new TestCaseData(null, "issuer", "subject", "Null provider")
            .SetName("Null Provider");
    }

    #endregion

    #region Time-based Test Cases

    /// <summary>
    /// Provides test cases for time-based operations.
    /// </summary>
    public static IEnumerable<TestCaseData> TimeComparisonTestCases()
    {
        var now = DateTime.UtcNow;
        
        yield return new TestCaseData(now, now.AddHours(1), true)
            .SetName("Current time vs Future - Should Update");
        yield return new TestCaseData(now, now.AddHours(-1), false)
            .SetName("Current time vs Past - Should Not Update");
        yield return new TestCaseData(now, now, false)
            .SetName("Current time vs Same - Should Not Update");
    }

    #endregion

    #region Combined Scenario Test Cases

    /// <summary>
    /// Provides complex test scenarios combining multiple domain concepts.
    /// </summary>
    public static IEnumerable<TestCaseData> ComplexScenarioTestCases()
    {
        yield return new TestCaseData(
            PrincipalType.Human, 
            RiskTier.Medium, 
            new[] { AccessMode.Signing, AccessMode.WatchOnly }, 
            new[] { OwnershipStatus.Verified, OwnershipStatus.Verified }
        ).SetName("Human with Medium Risk + Mixed Ownerships");
        
        yield return new TestCaseData(
            PrincipalType.Service, 
            RiskTier.Low, 
            new[] { AccessMode.WatchOnly }, 
            new[] { OwnershipStatus.Verified }
        ).SetName("Service with WatchOnly Access");
    }

    #endregion
}