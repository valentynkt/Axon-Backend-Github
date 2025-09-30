namespace Axon.Modules.Identity.Application.Providers;

using Axon.Modules.Identity.Application.Contracts.Providers;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles wallet-based signature authentication.
/// Extracted from AuthenticationService to follow single responsibility principle.
/// </summary>
public sealed class WalletAuthenticationProvider : IAuthenticationProvider
{
    private readonly IWalletSignatureVerifier _signatureVerifier;
    private readonly IAxonPrincipalWriteRepository _principalRepo;
    private readonly IWalletOwnershipRepository _walletOwnershipRepo;
    private readonly IWalletWriteRepository _walletRepo;
    private readonly UserManager<AxonUserAuth> _userManager;
    private readonly IChallengeValidationService _challengeValidationService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WalletAuthenticationProvider> _logger;

    public WalletAuthenticationProvider(
        IWalletSignatureVerifier signatureVerifier,
        IAxonPrincipalWriteRepository principalRepo,
        IWalletOwnershipRepository walletOwnershipRepo,
        IWalletWriteRepository walletRepo,
        UserManager<AxonUserAuth> userManager,
        IChallengeValidationService challengeValidationService,
        TimeProvider timeProvider,
        ILogger<WalletAuthenticationProvider> logger)
    {
        _signatureVerifier = signatureVerifier ?? throw new ArgumentNullException(nameof(signatureVerifier));
        _principalRepo = principalRepo ?? throw new ArgumentNullException(nameof(principalRepo));
        _walletOwnershipRepo = walletOwnershipRepo ?? throw new ArgumentNullException(nameof(walletOwnershipRepo));
        _walletRepo = walletRepo ?? throw new ArgumentNullException(nameof(walletRepo));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _challengeValidationService = challengeValidationService ?? throw new ArgumentNullException(nameof(challengeValidationService));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string ProviderType => "wallet";

    public bool CanHandle(AuthenticationRequest request)
    {
        return request is WalletAuthenticationRequest;
    }

    public async Task<Result<AuthenticationData, Error>> AuthenticateAsync(
        AuthenticationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is not WalletAuthenticationRequest walletRequest)
        {
            return Result.Failure<AuthenticationData, Error>(
                Error.Validation("Invalid request type for wallet provider"));
        }

        try
        {
            _logger.LogInformation("Starting wallet authentication for chain {ChainId}, address {Address}",
                walletRequest.ChainId, MaskAddress(walletRequest.Address));

            // Validate challenge message structure, MAC, TTL, and replay protection
            var challengeValidation = await _challengeValidationService.ValidateWalletChallengeAsync(
                walletRequest.SignedMessage,
                walletRequest.Mkv,
                cancellationToken);

            if (challengeValidation.IsFailure)
            {
                _logger.LogWarning("Challenge validation failed: {Error}", challengeValidation.Error);
                return Result.Failure<AuthenticationData, Error>(challengeValidation.Error);
            }

            // Verify wallet signature
            var messageString = walletRequest.SignedMessage;
            var signatureString = walletRequest.Signature;

            var signatureResult = _signatureVerifier.VerifySignature(
                walletRequest.ChainId,
                walletRequest.Address,
                messageString,
                signatureString);

            if (signatureResult.IsFailure || !signatureResult.Value)
            {
                _logger.LogWarning("Signature verification failed for address {Address}", MaskAddress(walletRequest.Address));
                return Result.Failure<AuthenticationData, Error>(
                    Error.Unauthorized("Invalid wallet signature"));
            }

            // Resolve or create principal and wallet ownership
            var principalResult = await ResolveOrCreatePrincipalAsync(
                walletRequest.ChainId,
                walletRequest.Address,
                cancellationToken);

            if (principalResult.IsFailure)
            {
                return Result.Failure<AuthenticationData, Error>(principalResult.Error);
            }

            var principal = principalResult.Value;

            // Get or create Identity user
            var identityUser = await GetOrCreateIdentityUserAsync(
                principal,
                walletRequest);

            if (identityUser == null)
            {
                return Result.Failure<AuthenticationData, Error>(
                    Error.Internal("Failed to create Identity user"));
            }

            // Update last authenticated timestamp
            identityUser.UpdateLastAuthenticated();
            await _userManager.UpdateAsync(identityUser);

            var authData = new AuthenticationData(
                User: identityUser,
                ProviderType: "wallet",
                AdditionalClaims: new Dictionary<string, object>
                {
                    ["chain_id"] = walletRequest.ChainId,
                    ["wallet_address"] = walletRequest.Address,
                    ["auth_method"] = "signature"
                });

            _logger.LogInformation("Wallet authentication successful for {Address} on {ChainId}",
                MaskAddress(walletRequest.Address), walletRequest.ChainId);

            return Result.Success<AuthenticationData, Error>(authData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wallet authentication failed unexpectedly");
            return Result.Failure<AuthenticationData, Error>(
                Error.Internal("Wallet authentication failed"));
        }
    }

    private async Task<Result<AxonPrincipal, Error>> ResolveOrCreatePrincipalAsync(
        string chainId,
        string address,
        CancellationToken cancellationToken)
    {
        // Create value objects
        var chainIdResult = ChainId.Create(chainId);
        if (chainIdResult.IsFailure)
        {
            return Result.Failure<AxonPrincipal, Error>(
                Error.Validation($"Invalid chain ID: {chainIdResult.Error}"));
        }

        var addressResult = Address.Create(address);
        if (addressResult.IsFailure)
        {
            return Result.Failure<AxonPrincipal, Error>(
                Error.Validation($"Invalid address: {addressResult.Error}"));
        }

        // First check if wallet already exists
        var existingWallet = await _walletRepo.GetByChainAndAddressAsync(
            chainIdResult.Value,
            addressResult.Value,
            cancellationToken);

        if (existingWallet != null)
        {
            // Find ownership for this wallet
            var ownerships = await _walletOwnershipRepo.FindActiveOwnershipsByWalletAsync(
                existingWallet.Id,
                cancellationToken);

            var verifiedOwnership = ownerships.FirstOrDefault(o =>
                o.Ownership.Status == OwnershipStatus.Verified &&
                o.Ownership.AccessMode == AccessMode.Signing);

            if (verifiedOwnership != null)
            {
                var principal = await _principalRepo.GetByIdAsync(verifiedOwnership.Ownership.PrincipalId, cancellationToken);
                if (principal != null)
                {
                    return Result.Success<AxonPrincipal, Error>(principal);
                }
            }
        }

        // Create new principal and wallet
        var newPrincipalId = new AxonUserId(Guid.CreateVersion7());
        var newPrincipal = AxonPrincipal.CreateHuman(newPrincipalId);
        // Create wallet if it doesn't exist
        if (existingWallet == null)
        {
            existingWallet = Wallet.Create(
                null, // Will generate a new ID
                chainId, // Pass the original string
                addressResult.Value);

            await _walletRepo.AddAsync(existingWallet, cancellationToken);
        }

        // Create wallet ownership and link through aggregate
        var ownership = WalletOwnership.Create(
            newPrincipalId,
            existingWallet.Id,
            AccessMode.Signing,
            OwnershipStatus.Verified,
            VerificationSource.DirectSignatureMsg);

        // Define the uniqueness check function for wallet ownership
        Func<WalletId, AccessMode, OwnershipStatus, Result<bool, Error>> checkExistingOwnership =
            (walletId, accessMode, status) =>
            {
                // For this creation scenario, we expect no conflicting verified+signing ownerships
                return Result.Success<bool, Error>(false);
            };

        var linkResult = newPrincipal.LinkWalletOwnership(ownership, checkExistingOwnership, _timeProvider);
        if (linkResult.IsFailure)
        {
            return Result.Failure<AxonPrincipal, Error>(linkResult.Error);
        }

        await _principalRepo.AddAsync(newPrincipal, cancellationToken);

        _logger.LogInformation("Created new principal {PrincipalId} for wallet {Address} on {ChainId}",
            newPrincipalId.Value, MaskAddress(address), chainId);

        return Result.Success<AxonPrincipal, Error>(newPrincipal);
    }

    private async Task<AxonUserAuth?> GetOrCreateIdentityUserAsync(
        AxonPrincipal principal,
        WalletAuthenticationRequest request)
    {
        // Try to find existing Identity user
        var existingUsers = await _userManager.GetUsersForClaimAsync(
            new System.Security.Claims.Claim("axon_principal_id", principal.Id.Value.ToString()));

        var existingUser = existingUsers.FirstOrDefault();

        if (existingUser != null)
        {
            return existingUser;
        }

        // Create new Identity user
        var newUser = AxonUserAuth.Create(
            principalId: principal.Id,
            providerType: "wallet",
            issuer: "axon",
            subject: request.Address,
            dynamicEnvironmentId: null,
            dynamicUserId: null);

        // Set primary wallet info
        newUser.SetPrimaryWallet(request.ChainId, request.Address);

        var createResult = await _userManager.CreateAsync(newUser);

        if (!createResult.Succeeded)
        {
            _logger.LogError("Failed to create Identity user: {Errors}",
                string.Join(", ", createResult.Errors.Select(e => e.Description)));
            return null;
        }

        // Add claims
        await _userManager.AddClaimAsync(newUser,
            new System.Security.Claims.Claim("axon_principal_id", principal.Id.Value.ToString()));
        await _userManager.AddClaimAsync(newUser,
            new System.Security.Claims.Claim("wallet_address", request.Address));
        await _userManager.AddClaimAsync(newUser,
            new System.Security.Claims.Claim("chain_id", request.ChainId));

        _logger.LogInformation("Created new Identity user {UserId} for principal {PrincipalId}",
            newUser.Id, principal.Id.Value);

        return newUser;
    }

    private static string MaskAddress(string address)
    {
        return address.Length > 8
            ? $"{address[..4]}...{address[^4..]}"
            : address;
    }

}