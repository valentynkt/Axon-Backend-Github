using Axon.Modules.Identity.Application.Commands.EnsureWalletLinked;
using Axon.Modules.Identity.Application.Commands.UpdateProfile;
using Axon.Modules.Identity.Application.Commands.UpsertPrincipalFromCredential;
using Axon.Modules.Identity.Application.Commands.UpsertWalletActivity;
using Axon.Modules.Identity.Application.DTOs.Exchange;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Application.Services;

/// <summary>
/// Maps Dynamic.xyz user data to Identity command parameters with normalization.
/// Handles chain normalization, email hashing, and metadata extraction from Dynamic JWT claims.
/// </summary>
public interface IDynamicToCommandsMapper
{
    /// <summary>
    /// Maps Dynamic user data to UpsertPrincipalFromCredential command.
    /// Computes email hash, extracts metadata, and sets proper issuer format.
    /// </summary>
    /// <param name="userData">The validated user data from Dynamic JWT</param>
    /// <param name="correlationId">Optional correlation ID for request tracking</param>
    /// <returns>A command ready for MediatR dispatch</returns>
    UpsertPrincipalFromCredentialCommand MapToUpsertPrincipalCommand(
        ExchangeUserData userData, 
        string? correlationId = null);

    /// <summary>
    /// Maps wallet data to UpsertWalletActivity command.
    /// Normalizes chain format and validates address format for the specified chain.
    /// </summary>
    /// <param name="wallet">The wallet data from Dynamic JWT</param>
    /// <param name="correlationId">Optional correlation ID for request tracking</param>
    /// <returns>A command ready for MediatR dispatch</returns>
    /// <exception cref="ArgumentException">Thrown when chain format is invalid</exception>
    UpsertWalletActivityCommand MapToUpsertWalletActivityCommand(
        ExchangeWalletData wallet, 
        string? correlationId = null);

    /// <summary>
    /// Maps wallet data to EnsureWalletLinked command with default-per-chain logic.
    /// Applies OIDC proof type and unknown access mode for Dynamic wallets.
    /// </summary>
    /// <param name="axonId">The principal's Axon ID</param>
    /// <param name="wallet">The wallet data from Dynamic JWT</param>
    /// <param name="setAsDefault">Whether to set this wallet as default for its chain</param>
    /// <param name="correlationId">Optional correlation ID for request tracking</param>
    /// <returns>A command ready for MediatR dispatch</returns>
    /// <exception cref="ArgumentException">Thrown when chain format is invalid</exception>
    EnsureWalletLinkedCommand MapToEnsureWalletLinkedCommand(
        AxonId axonId,
        ExchangeWalletData wallet,
        bool setAsDefault = false,
        string? correlationId = null);

    /// <summary>
    /// Maps user data to UpdateProfile command if language differs from current.
    /// Currently returns null as Dynamic doesn't provide language preferences.
    /// </summary>
    /// <param name="axonId">The principal's Axon ID</param>
    /// <param name="userData">The validated user data from Dynamic JWT</param>
    /// <param name="currentLanguage">The principal's current preferred language</param>
    /// <param name="correlationId">Optional correlation ID for request tracking</param>
    /// <returns>A command ready for MediatR dispatch, or null if no update needed</returns>
    UpdateProfileCommand? MapToUpdateProfileCommand(
        AxonId axonId,
        ExchangeUserData userData,
        string currentLanguage,
        string? correlationId = null);

    /// <summary>
    /// Computes SHA256 hash of normalized email for PrimaryEmailHash storage.
    /// Normalizes email by lowercasing the domain part before hashing.
    /// </summary>
    /// <param name="email">The email address to hash</param>
    /// <returns>Lowercase hexadecimal SHA256 hash, or null if email is null/empty</returns>
    string? ComputePrimaryEmailHash(string? email);
}