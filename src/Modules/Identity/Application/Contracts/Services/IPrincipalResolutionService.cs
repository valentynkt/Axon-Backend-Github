using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Application.Contracts.Services;

/// <summary>
/// Service for deterministic principal resolution with consistent tie-breaking rules.
/// Implements 2-step resolution: credential-first → wallet-fallback → create-new.
/// </summary>
public interface IPrincipalResolutionService
{
    /// <summary>
    /// Resolves a principal using the deterministic 2-step resolution algorithm.
    /// ChainId must be in compound format (e.g., "solana-mainnet") containing all network information.
    /// </summary>
    /// <param name="provider">The identity provider type</param>
    /// <param name="issuer">The issuer of the credential</param>
    /// <param name="subject">The subject identifier from the credential</param>
    /// <param name="chainId">The blockchain chain identifier in compound format</param>
    /// <param name="address">The wallet address</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Resolution result containing the principal and resolution path</returns>
    Task<Result<PrincipalResolutionResult, Error>> ResolveAsync(
        ProviderType provider,
        string issuer,
        string subject,
        ChainId chainId,
        Address address,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of principal resolution containing the resolved principal and metadata.
/// </summary>
public sealed record PrincipalResolutionResult(
    AxonPrincipal Principal,
    ResolutionPath Path,
    bool WasAutoLinked);

/// <summary>
/// The resolution path taken to resolve the principal.
/// </summary>
public enum ResolutionPath
{
    /// <summary>
    /// Principal was resolved via credential matching.
    /// </summary>
    Credential,

    /// <summary>
    /// Principal was resolved via wallet ownership with tie-breaking.
    /// </summary>
    Wallet,

    /// <summary>
    /// New principal was created.
    /// </summary>
    Created
}