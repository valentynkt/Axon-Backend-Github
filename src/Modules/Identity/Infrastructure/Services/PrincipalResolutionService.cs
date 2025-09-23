using System.Diagnostics;
using System.Globalization;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Infrastructure.Services;

public sealed class PrincipalResolutionService : IPrincipalResolutionService
{
    private static readonly ActivitySource ActivitySource = new("Axon.Identity.PrincipalResolution");

    private readonly IAxonPrincipalReadRepository _principalRepository;
    private readonly IAxonPrincipalWriteRepository _principalWriteRepository;
    private readonly IWalletReadRepository _walletReadRepository;
    private readonly IWalletWriteRepository _walletWriteRepository;
    private readonly IWalletOwnershipRepository _ownershipRepository;
    private readonly ILogger<PrincipalResolutionService> _logger;

    public PrincipalResolutionService(
        IAxonPrincipalReadRepository principalRepository,
        IAxonPrincipalWriteRepository principalWriteRepository,
        IWalletReadRepository walletReadRepository,
        IWalletWriteRepository walletWriteRepository,
        IWalletOwnershipRepository ownershipRepository,
        ILogger<PrincipalResolutionService> logger)
    {
        _principalRepository = principalRepository;
        _principalWriteRepository = principalWriteRepository;
        _walletReadRepository = walletReadRepository;
        _walletWriteRepository = walletWriteRepository;
        _ownershipRepository = ownershipRepository;
        _logger = logger;
    }

    public async Task<Result<PrincipalResolutionResult, Error>> ResolveAsync(
        ProviderType provider,
        string issuer,
        string subject,
        NetworkEnvironment networkEnvironment,
        ChainId chainId,
        Address address,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("PrincipalResolution.Resolve");
        activity?.SetTag("provider", provider.ToString(CultureInfo.InvariantCulture));
        activity?.SetTag("network_environment", networkEnvironment.Value);
        activity?.SetTag("chain_id", chainId.Value);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Step 1: Credential-first resolution
            var credentialResult = await ResolveByCredentialAsync(
                provider, issuer, subject, networkEnvironment, chainId, address, cancellationToken);

            if (credentialResult.IsSuccess)
            {
                LogResolution("credential", networkEnvironment, chainId, address, stopwatch.ElapsedMilliseconds);
                return credentialResult;
            }

            // Step 2: Wallet-fallback resolution with tie-breaking
            var walletResult = await ResolveByWalletAsync(
                networkEnvironment, chainId, address, cancellationToken);

            if (walletResult.IsSuccess)
            {
                LogResolution("wallet", networkEnvironment, chainId, address, stopwatch.ElapsedMilliseconds);
                return walletResult;
            }

            // Step 3: Create new principal with race protection
            var createResult = await CreateNewPrincipalWithRaceProtectionAsync(
                networkEnvironment, chainId, address, cancellationToken);

            LogResolution("created", networkEnvironment, chainId, address, stopwatch.ElapsedMilliseconds);
            return createResult;
        }
        finally
        {
            activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);
        }
    }

    private async Task<Result<PrincipalResolutionResult, Error>> ResolveByCredentialAsync(
        ProviderType provider,
        string issuer,
        string subject,
        NetworkEnvironment networkEnvironment,
        ChainId chainId,
        Address address,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("PrincipalResolution.CredentialFirst");

        var principal = await _principalRepository.FindByCredentialAsync(
            provider, issuer, subject, cancellationToken);

        if (principal is null)
        {
            return Result.Failure<PrincipalResolutionResult, Error>(
                Error.NotFound("No principal found with given credentials"));
        }

        // Check for cross-environment conflict
        var crossEnvOwnership = await _ownershipRepository
            .FindVerifiedSigningOwnershipAcrossEnvironmentsAsync(chainId.Value, address, cancellationToken);

        if (crossEnvOwnership is not null)
        {
            var principalB = crossEnvOwnership.Principal;

            if (principal.Id == principalB.Id)
            {
                // A == B: Same principal - auto-link wallet for current network_env
                _logger.LogInformation(
                    "Auto-linking wallet for principal {PrincipalId} to network environment {NetworkEnvironment}",
                    principal.Id, networkEnvironment.Value);

                var wallet = await _walletWriteRepository.UpsertWalletAsync(
                    networkEnvironment, chainId, address, cancellationToken);

                await _ownershipRepository.CreateOwnershipAsync(
                    principal.Id, wallet.Id, AccessMode.Signing, OwnershipStatus.Verified,
                    VerificationSource.DynamicAttested, cancellationToken);

                return Result.Success<PrincipalResolutionResult, Error>(new PrincipalResolutionResult(
                    principal, ResolutionPath.Credential, true));
            }
            else
            {
                // A != B: Different principals - 409 Conflict
                _logger.LogWarning(
                    "Cross-environment verified+signing conflict: Principal {PrincipalA} != {PrincipalB} for {ChainId}:{Address}",
                    principal.Id, principalB.Id, chainId.Value, address.Value);

                return Result.Failure<PrincipalResolutionResult, Error>(
                    Error.Conflict($"Cross-environment verified+signing conflict: manual resolution required. " +
                                   $"Current principal: {principal.Id}, Cross-env principal: {principalB.Id}"));
            }
        }

        return Result.Success<PrincipalResolutionResult, Error>(new PrincipalResolutionResult(
            principal, ResolutionPath.Credential, false));
    }

    private async Task<Result<PrincipalResolutionResult, Error>> ResolveByWalletAsync(
        NetworkEnvironment networkEnvironment,
        ChainId chainId,
        Address address,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("PrincipalResolution.WalletFallback");

        // MUST use triple-key lookup
        var wallet = await _walletReadRepository.FindWalletAsync(
            networkEnvironment, chainId, address, cancellationToken);

        if (wallet is null)
        {
            // Check for cross-env attach to existing verified owner
            var crossEnvOwnership = await _ownershipRepository
                .FindVerifiedSigningOwnershipAcrossEnvironmentsAsync(chainId.Value, address, cancellationToken);

            if (crossEnvOwnership is not null)
            {
                _logger.LogInformation(
                    "Attaching to existing cross-env principal {PrincipalId} and creating network-scoped wallet",
                    crossEnvOwnership.PrincipalId);

                var newWallet = await _walletWriteRepository.UpsertWalletAsync(
                    networkEnvironment, chainId, address, cancellationToken);

                await _ownershipRepository.CreateOwnershipAsync(
                    crossEnvOwnership.PrincipalId, newWallet.Id, AccessMode.Signing, OwnershipStatus.Verified,
                    VerificationSource.DynamicAttested, cancellationToken);

                return Result.Success<PrincipalResolutionResult, Error>(new PrincipalResolutionResult(
                    crossEnvOwnership.Principal, ResolutionPath.Wallet, WasAutoLinked: true));
            }

            return Result.Failure<PrincipalResolutionResult, Error>(
                Error.NotFound("No wallet found for resolution"));
        }

        // Get active ownerships (status != revoked) from wallet candidates
        var ownerships = await _ownershipRepository.FindActiveOwnershipsByWalletAsync(
            wallet.Id, cancellationToken);

        if (!ownerships.Any())
        {
            return Result.Failure<PrincipalResolutionResult, Error>(
                Error.NotFound("No active ownerships for wallet"));
        }

        // Check if verified+signing exists
        var verifiedSigning = ownerships
            .FirstOrDefault(o => o.Status == OwnershipStatus.Verified && o.AccessMode == AccessMode.Signing);

        if (verifiedSigning is not null)
        {
            return Result.Success<PrincipalResolutionResult, Error>(new PrincipalResolutionResult(
                verifiedSigning.Principal, ResolutionPath.Wallet, WasAutoLinked: false));
        }

        // Apply tie-break rules ONLY when no verified+signing exists
        var winner = ApplyTieBreakRules(ownerships);

        // Auto-revoke losers
        foreach (var ownership in ownerships.Where(o => o.Id != winner.Id))
        {
            _logger.LogInformation(
                "Auto-revoking ownership {OwnershipId} reason=conflict_lost for {NetworkEnvironment}:{ChainId}:{Address}",
                ownership.Id, networkEnvironment.Value, chainId.Value, address.Value);

            await _ownershipRepository.RevokeOwnershipAsync(ownership.Id, "conflict_lost", cancellationToken);
        }

        return Result.Success<PrincipalResolutionResult, Error>(new PrincipalResolutionResult(
            winner.Principal, ResolutionPath.Wallet, WasAutoLinked: false));
    }

    private static WalletOwnership ApplyTieBreakRules(IEnumerable<WalletOwnership> ownerships)
    {
        return ownerships
            .OrderBy(o => GetVerificationSourceRank(o.VerificationSource))
            .ThenBy(o => o.Principal.CreatedAt)
            .First();
    }

    private static int GetVerificationSourceRank(VerificationSource source) => source switch
    {
        VerificationSource.DynamicAttested => 1,
        VerificationSource.DirectSignatureMsg => 2,
        VerificationSource.DirectSignatureTx => 3,
        VerificationSource.WatchOnly => 4,
        _ => 99
    };

    private async Task<Result<PrincipalResolutionResult, Error>> CreateNewPrincipalWithRaceProtectionAsync(
        NetworkEnvironment networkEnvironment,
        ChainId chainId,
        Address address,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("PrincipalResolution.CreateNew");

        // Upsert wallet first to prevent race conditions
        var wallet = await _walletWriteRepository.UpsertWalletAsync(
            networkEnvironment, chainId, address, cancellationToken);

        // Re-read wallet and re-resolve ownerships
        var rereadWallet = await _walletReadRepository.FindWalletAsync(
            networkEnvironment, chainId, address, cancellationToken);

        if (rereadWallet is null)
        {
            return Result.Failure<PrincipalResolutionResult, Error>(
                Error.Internal("Failed to create or find wallet after upsert"));
        }

        var ownerships = await _ownershipRepository.FindActiveOwnershipsByWalletAsync(
            rereadWallet.Id, cancellationToken);

        // Check if another request already created principal during race
        if (ownerships.Any())
        {
            var winner = ownerships[0];
            _logger.LogInformation(
                "Race condition detected: Using existing principal {PrincipalId} created by concurrent request",
                winner.PrincipalId);

            return Result.Success<PrincipalResolutionResult, Error>(new PrincipalResolutionResult(
                winner.Principal, ResolutionPath.Wallet, WasAutoLinked: false));
        }

        // Safe to create new principal
        var principal = AxonPrincipal.CreateHuman();
        await _principalWriteRepository.AddAsync(principal, cancellationToken);
        await _principalWriteRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        // Link wallet to new principal
        await _ownershipRepository.CreateOwnershipAsync(
            principal.Id, wallet.Id, AccessMode.Signing, OwnershipStatus.Verified,
            VerificationSource.DynamicAttested, cancellationToken);

        return Result.Success<PrincipalResolutionResult, Error>(new PrincipalResolutionResult(
            principal, ResolutionPath.Created, WasAutoLinked: false));
    }

    private void LogResolution(
        string path,
        NetworkEnvironment networkEnvironment,
        ChainId chainId,
        Address address,
        long elapsedMs)
    {
        _logger.LogInformation(
            "Principal resolution completed: resolution_path={ResolutionPath} network_environment={NetworkEnvironment} " +
            "chain_id={ChainId} address={Address} duration_ms={Duration}",
            path, networkEnvironment.Value, chainId.Value, address.Value, elapsedMs);
    }
}