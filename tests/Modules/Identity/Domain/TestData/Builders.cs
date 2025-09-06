using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Domain.Tests.TestData;

/// <summary>
/// Simplified test data builders focused on what's needed for testing rules.
/// </summary>
public static class Builders
{
    // Simple value object creators for tests
    public static RiskTier LowRiskTier => RiskTier.Low;
    public static RiskTier MediumRiskTier => RiskTier.Medium;
    public static RiskTier HighRiskTier => RiskTier.High;
    
    public static PrincipalType HumanPrincipal => PrincipalType.Human;
    public static PrincipalType ServicePrincipal => PrincipalType.Service;
    
    public static ProviderType DynamicProvider => ProviderType.Dynamic;
    public static ProviderType SiwsProvider => ProviderType.Siws;
    public static ProviderType OidcProvider => ProviderType.Oidc;
    
    public static ProofType UnknownProof => ProofType.From("unknown");
    public static ProofType SignatureProof => ProofType.From("signature");
    
    public static OwnershipState PendingState => OwnershipState.Pending;
    public static OwnershipState VerifiedState => OwnershipState.Verified;
    public static OwnershipState RevokedState => OwnershipState.Revoked;
    
    public static PreferredLanguage EnglishLanguage => PreferredLanguage.English;
    public static PreferredLanguage SpanishLanguage => PreferredLanguage.Spanish;
    
    public static ChainId SolanaChain => ChainId.Solana;
    public static ChainId EthereumChain => ChainId.Ethereum;
    
    public static Address SolanaAddress => Address.From(TestConstants.ValidSolanaAddress);
    public static Address EthereumAddress => Address.From(TestConstants.ValidEthAddress);
    
    public static Tag TestTag => Tag.From("test-tag");
    public static Tag AnotherTag => Tag.From("another-tag");
}