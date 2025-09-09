using Axon.Modules.Identity.Domain.Enums;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Axon.Modules.Identity.Application.Commands.EnsureWalletLinked;
using Axon.Modules.Identity.Application.Commands.UpdateProfile;
using Axon.Modules.Identity.Application.Commands.UpsertPrincipalFromCredential;
using Axon.Modules.Identity.Application.Commands.UpsertWalletActivity;
using Axon.Modules.Identity.Application.Common.Constants;
using Axon.Modules.Identity.Application.DTOs.Exchange;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Application.Services;

/// <summary>
/// Implementation of Dynamic.xyz user data to Identity commands mapper.
/// 
/// ARCHITECTURAL DECISION: Command Isolation and Phase Separation
/// - UpsertPrincipalFromCredential command NO LONGER includes wallet attachment
/// - Wallet operations are mapped to separate commands (EnsureWalletLinked, UpsertWalletActivity)
/// - This maintains clean separation between identity and wallet concerns per Clean Architecture
/// - Each command has single responsibility and can be tested/evolved independently
/// </summary>
public sealed class DynamicToCommandsMapper : IDynamicToCommandsMapper
{
    private readonly ILogger<DynamicToCommandsMapper> _logger;

    public DynamicToCommandsMapper(ILogger<DynamicToCommandsMapper> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public UpsertPrincipalFromCredentialCommand MapToUpsertPrincipalCommand(
        ExchangeUserData userData, 
        string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(userData);

        var primaryEmailHash = ComputePrimaryEmailHash(userData.Email);

        return new UpsertPrincipalFromCredentialCommand(
            CorrelationId: correlationId,
            ProviderType: DynamicAuthConstants.ProviderType,
            Issuer: $"{DynamicAuthConstants.IssuerPrefix}/{userData.EnvironmentId}",
            Subject: userData.UserId,
            EnvironmentId: userData.EnvironmentId,
            CredentialMetadata: CreateCredentialMetadata(userData),
            PrimaryEmailHash: primaryEmailHash
            // REMOVED AttachWallet parameter - wallet operations now handled separately
            // via EnsureWalletLinked and UpsertWalletActivity commands for clean architecture
        );
    }

    public UpsertWalletActivityCommand MapToUpsertWalletActivityCommand(
        ExchangeWalletData wallet, 
        string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(wallet);

        // Parse chain to string (let domain validate)
        var normalizedChain = NormalizeChain(wallet.Chain);
        if (!ChainId.TryFrom(normalizedChain, out var chainId))
        {
            _logger.LogWarning("Invalid chain format for wallet: {Chain}", wallet.Chain);
            throw new ArgumentException($"Invalid chain format: {wallet.Chain}", nameof(wallet));
        }

        var metaPatch = CreateWalletMetadata(wallet);
        var lastSeenAt = wallet.ConnectedAtUtc ?? DateTimeOffset.UtcNow;

        return new UpsertWalletActivityCommand(
            CorrelationId: correlationId,
            WalletId: null, // Use coordinates instead
            ChainId: chainId,
            RawAddress: wallet.Address,
            ObservedAt: lastSeenAt,
            MetaPatch: metaPatch,
            TagsToAdd: null, // Don't add tags during exchange
            TagsToRemove: null
        );
    }

    public EnsureWalletLinkedCommand MapToEnsureWalletLinkedCommand(
        AxonId axonId,
        ExchangeWalletData wallet,
        bool setAsDefault = false,
        string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(wallet);

        // Parse chain to string (let domain validate) 
        var normalizedChain = NormalizeChain(wallet.Chain);
        if (!ChainId.TryFrom(normalizedChain, out var chainId))
        {
            _logger.LogWarning("Invalid chain format for wallet link: {Chain}", wallet.Chain);
            throw new ArgumentException($"Invalid chain format: {wallet.Chain}", nameof(wallet));
        }

        var label = CreateWalletLabel(wallet);

        return new EnsureWalletLinkedCommand(
            CorrelationId: correlationId,
            AxonId: axonId,
            ChainId: chainId,
            RawAddress: wallet.Address,
            ProofType: DynamicAuthConstants.DynamicVerifiedProofType,
            AccessMode: DynamicAuthConstants.SigningMode, // Dynamic has verified signing capability
            Label: label,
            Verify: true, // Dynamic has already verified wallet ownership via signature
            SetAsDefault: setAsDefault
        );
    }

    public UpdateProfileCommand? MapToUpdateProfileCommand(
        AxonId axonId,
        ExchangeUserData userData,
        string currentLanguage,
        string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(userData);

        // For now, Dynamic doesn't provide language preferences
        // This is a placeholder for future language support
        // Return null = no profile update needed
        return null;
    }

    public string? ComputePrimaryEmailHash(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        // Normalize email before hashing
        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
            return null;

        // Compute SHA256 hash
        var emailBytes = Encoding.UTF8.GetBytes(normalizedEmail);
        var hashBytes = SHA256.HashData(emailBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    #region Private Helpers

    /// <summary>
    /// Creates credential metadata from Dynamic user data
    /// </summary>
    private static Dictionary<string, object>? CreateCredentialMetadata(ExchangeUserData userData)
    {
        var metadata = new Dictionary<string, object>();

        if (userData.FirstVisitUtc.HasValue)
            metadata[DynamicAuthConstants.CredentialMetadataKeys.FirstVisitUtc] = userData.FirstVisitUtc.Value;

        if (userData.LastVisitUtc.HasValue)
            metadata[DynamicAuthConstants.CredentialMetadataKeys.LastVisitUtc] = userData.LastVisitUtc.Value;

        if (userData.AdditionalMetadata?.ContainsKey(DynamicAuthConstants.CredentialMetadataKeys.SessionPublicKey) == true)
            metadata[DynamicAuthConstants.CredentialMetadataKeys.SessionPublicKey] = userData.AdditionalMetadata[DynamicAuthConstants.CredentialMetadataKeys.SessionPublicKey];

        metadata[DynamicAuthConstants.CredentialMetadataKeys.IsNewUser] = userData.IsNewUser;
        metadata[DynamicAuthConstants.CredentialMetadataKeys.WalletCount] = userData.Wallets?.Count ?? 0;

        return metadata.Count > 0 ? metadata : null;
    }

    /// <summary>
    /// Creates wallet metadata from Dynamic wallet data
    /// </summary>
    private static Dictionary<string, object>? CreateWalletMetadata(ExchangeWalletData wallet)
    {
        var metadata = new Dictionary<string, object>();

        // Note: ExchangeWalletData doesn't include dynamicWalletId

        if (!string.IsNullOrWhiteSpace(wallet.WalletName))
            metadata[DynamicAuthConstants.WalletMetadataKeys.WalletName] = wallet.WalletName;

        if (!string.IsNullOrWhiteSpace(wallet.Provider))
            metadata[DynamicAuthConstants.WalletMetadataKeys.Provider] = wallet.Provider;

        return metadata.Count > 0 ? metadata : null;
    }

    /// <summary>
    /// Creates a wallet label from Dynamic wallet data
    /// </summary>
    private static string? CreateWalletLabel(ExchangeWalletData wallet)
    {
        // Use wallet name if available, otherwise provider
        if (!string.IsNullOrWhiteSpace(wallet.WalletName))
            return wallet.WalletName;

        if (!string.IsNullOrWhiteSpace(wallet.Provider))
            return wallet.Provider;

        return null;
    }

    /// <summary>
    /// Normalizes chain identifier to match Axon's format
    /// </summary>
    private static string NormalizeChain(string chain)
    {
        if (string.IsNullOrWhiteSpace(chain))
            return chain;

        // Use predefined normalization mappings
        var normalizedChain = chain.ToLowerInvariant();
        return DynamicAuthConstants.ChainNormalizations.TryGetValue(normalizedChain, out var mapped) 
            ? mapped 
            : normalizedChain; // Pass through as-is for domain validation
    }

    /// <summary>
    /// Normalizes email by lowercasing domain part
    /// </summary>
    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return string.Empty;

        var atIndex = email.LastIndexOf('@');
        if (atIndex <= 0 || atIndex >= email.Length - 1)
            return email; // Invalid format, return as-is

        var localPart = email[..atIndex];
        var domainPart = email[(atIndex + 1)..].ToLowerInvariant();
        return $"{localPart}@{domainPart}";
    }

    #endregion
}