using Axon.Modules.Identity.Application.DTOs.Responses;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;

namespace Axon.Modules.Identity.Application.Common.Mappers;

/// <summary>
/// Pure mapping functions for converting Identity domain entities to DTOs.
/// All methods are static and side-effect free.
/// </summary>
public static class IdentityDtoMapper
{
    /// <summary>
    /// Maps an AxonPrincipal aggregate to PrincipalDto.
    /// Computes active counts and safely exposes profile data.
    /// </summary>
    /// <param name="principal">The principal aggregate to map</param>
    /// <returns>PrincipalDto with computed counts and profile data</returns>
    public static PrincipalDto ToPrincipalDto(AxonPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return new PrincipalDto(
            AxonId: principal.Id.Value.ToString(),
            Type: principal.Type.Value,
            PreferredLanguage: principal.Profile.PreferredLanguage.Value,
            RiskTier: principal.Profile.RiskTier.Value,
            DefaultPerChain: principal.ChainDefaults.ToDictionary(
                cd => cd.ChainId.Value, 
                cd => cd.WalletId.Value.ToString()),
            ActiveWalletCount: principal.WalletOwnerships.Count(w => !w.IsDeleted),
            ActiveCredentialCount: principal.Credentials.Count(c => !c.IsDeleted)
        );
    }

    /// <summary>
    /// Maps an IdentityCredential entity to CredentialDto.
    /// Exposes metadata keys only, no metadata values for privacy.
    /// </summary>
    /// <param name="credential">The credential entity to map</param>
    /// <returns>CredentialDto with metadata keys but no values</returns>
    public static CredentialDto ToCredentialDto(IdentityCredential credential)
    {
        ArgumentNullException.ThrowIfNull(credential);

        return new CredentialDto(
            CredentialId: credential.Id.Value.ToString(),
            ProviderType: credential.ProviderType.Value,
            Issuer: credential.Issuer,
            Subject: credential.Subject,
            EnvironmentId: credential.EnvironmentId,
            VerifiedAt: credential.VerifiedAt,
            LastSeenAt: credential.LastSeenAt,
            MetadataKeys: new[] { "verification_method", "email_hash", "session_public_key", "device_id", "user_agent", "ip_hash" }
        );
    }

    /// <summary>
    /// Maps a Wallet aggregate to WalletDto with tag redaction based on authorization.
    /// Tags are included only when includeTags is true (caller has permission).
    /// </summary>
    /// <param name="wallet">The wallet aggregate to map</param>
    /// <param name="includeTags">Whether to include tags (based on auth context)</param>
    /// <returns>WalletDto with conditionally redacted tags</returns>
    public static WalletDto ToWalletDto(Wallet wallet, bool includeTags = true)
    {
        ArgumentNullException.ThrowIfNull(wallet);

        return new WalletDto(
            WalletId: wallet.Id.Value.ToString(),
            ChainId: wallet.Chain.Value,
            Address: wallet.Address.Value,
            FirstSeenAt: wallet.FirstSeenAt,
            LastSeenAt: wallet.LastSeenAt,
            Tags: includeTags ? wallet.Tags.Select(t => t.Value).ToArray() : Array.Empty<string>(),
            MetadataSizeBytes: wallet.Profile.DisplayName?.Length ?? 0,
            IsActive: !wallet.IsDeleted
        );
    }

    /// <summary>
    /// Maps a WalletOwnership entity to WalletOwnershipDto.
    /// Should only be called for the authenticated caller's ownership.
    /// </summary>
    /// <param name="ownership">The wallet ownership to map</param>
    /// <returns>WalletOwnershipDto with primitive types</returns>
    public static WalletOwnershipDto ToWalletOwnershipDto(WalletOwnership ownership)
    {
        ArgumentNullException.ThrowIfNull(ownership);

        return new WalletOwnershipDto(
            OwnershipId: ownership.Id.Value.ToString(),
            AxonId: ownership.AxonId.Value.ToString(),
            WalletId: ownership.WalletId.Value.ToString(),
            ChainId: ownership.ChainId.Value,
            ProofType: ownership.ProofType.Value,
            AccessMode: ownership.AccessMode.Value,
            State: ownership.State.Value,
            FirstLinkedAt: ownership.FirstLinkedAt,
            LastVerifiedAt: ownership.LastVerifiedAt,
            Label: ownership.Label
        );
    }

    /// <summary>
    /// Maps a Wallet aggregate to slim WalletSearchResultDto for search/autocomplete.
    /// Contains only essential fields for UI lookup scenarios.
    /// </summary>
    /// <param name="wallet">The wallet aggregate to map</param>
    /// <returns>WalletSearchResultDto with minimal data</returns>
    public static WalletSearchResultDto ToWalletSearchResultDto(Wallet wallet)
    {
        ArgumentNullException.ThrowIfNull(wallet);

        return new WalletSearchResultDto(
            WalletId: wallet.Id.Value.ToString(),
            ChainId: wallet.Chain.Value,
            Address: wallet.Address.Value,
            LastSeenAt: wallet.LastSeenAt
        );
    }
}