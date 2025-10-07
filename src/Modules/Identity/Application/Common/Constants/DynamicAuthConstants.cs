using Axon.Modules.Identity.Domain.Enums;
namespace Axon.Modules.Identity.Application.Common.Constants;

/// <summary>
/// Constants for Dynamic.xyz authentication and JWT exchange operations
/// </summary>
public static class DynamicAuthConstants
{
    /// <summary>
    /// Provider type identifier for Dynamic.xyz credentials
    /// </summary>
    public const string ProviderType = "dynamic";
    
    /// <summary>
    /// Issuer prefix for Dynamic.xyz JWTs
    /// </summary>
    public const string IssuerPrefix = "app.dynamicauth.com";
    
    /// <summary>
    /// Proof type for Dynamic.xyz verified wallet ownership
    /// </summary>
    public const string DynamicVerifiedProofType = "dynamic_verified";
    
    /// <summary>
    /// Access mode for Dynamic.xyz wallets (signing capability verified by Dynamic)
    /// </summary>
    public const string SigningMode = "signing";
    
    /// <summary>
    /// Chain normalization mappings from Dynamic format to Axon format
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> ChainNormalizations = new Dictionary<string, string>
    {
        ["eth"] = "ethereum",
        ["ethereum"] = "ethereum",
        ["polygon"] = "polygon",
        ["matic"] = "polygon",
        ["bsc"] = "bsc",
        ["binance"] = "bsc",
        ["arbitrum"] = "arbitrum",
        ["arb"] = "arbitrum",
        ["optimism"] = "optimism",
        ["opt"] = "optimism",
        ["avalanche"] = "avalanche",
        ["avax"] = "avalanche",
        ["solana"] = "solana",
        ["sol"] = "solana"
    };
    
    /// <summary>
    /// Error codes for Dynamic authentication operations
    /// </summary>
    public static class ErrorCodes
    {
        public const string TokenRequired = "AUTH.TOKEN_REQUIRED";
        public const string TokenExpired = "AUTH.TOKEN_EXPIRED";
        public const string InvalidSignature = "AUTH.INVALID_SIGNATURE";
        public const string ValidationFailed = "AUTH.VALIDATION_FAILED";
        public const string PrincipalConflict = "AUTH.PRINCIPAL_CONFLICT";
        public const string ExchangeError = "AUTH.EXCHANGE_ERROR";
        public const string InvalidTokenFormat = "AUTH.INVALID_TOKEN_FORMAT";
        public const string InvalidChain = "WALLET.INVALID_CHAIN";
        public const string InvalidAddress = "WALLET.INVALID_ADDRESS";
    }
    
    /// <summary>
    /// Metadata keys for credential storage
    /// </summary>
    public static class CredentialMetadataKeys
    {
        public const string FirstVisitUtc = "firstVisitUtc";
        public const string LastVisitUtc = "lastVisitUtc";
        public const string SessionPublicKey = "sessionPublicKey";
        public const string IsNewUser = "isNewUser";
        public const string WalletCount = "walletCount";
        public const string VerifiedCredentialsHashes = "verifiedCredentialsHashes";
    }
    
    /// <summary>
    /// Metadata keys for wallet storage
    /// </summary>
    public static class WalletMetadataKeys
    {
        public const string WalletName = "walletName";
        public const string Provider = "provider";
        public const string DynamicWalletId = "dynamicWalletId";
    }
}