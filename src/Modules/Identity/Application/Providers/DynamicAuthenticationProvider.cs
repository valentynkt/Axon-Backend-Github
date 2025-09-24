namespace Axon.Modules.Identity.Application.Providers;

using System.Security.Claims;
using System.Text.Json;
using Contracts.ExternalServices;
using Axon.Modules.Identity.Application.Contracts.Providers;
using Contracts.Persistence;
using Domain.Aggregates.AxonPrincipal;
using Domain.Aggregates.Wallet;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles Dynamic.xyz JWT token exchange.
/// Extracted from DynamicAuthService to follow single responsibility principle.
/// </summary>
public sealed class DynamicAuthenticationProvider : IAuthenticationProvider
{
    private readonly IDynamicAuthService _dynamicAuthService;
    private readonly IAxonPrincipalWriteRepository _principalRepo;
    private readonly IWalletOwnershipRepository _walletOwnershipRepo;
    private readonly IWalletWriteRepository _walletRepo;
    private readonly UserManager<AxonUserAuth> _userManager;
    private readonly ILogger<DynamicAuthenticationProvider> _logger;

    public DynamicAuthenticationProvider(
        IDynamicAuthService dynamicAuthService,
        IAxonPrincipalWriteRepository principalRepo,
        IWalletOwnershipRepository walletOwnershipRepo,
        IWalletWriteRepository walletRepo,
        UserManager<AxonUserAuth> userManager,
        ILogger<DynamicAuthenticationProvider> logger)
    {
        _dynamicAuthService = dynamicAuthService ?? throw new ArgumentNullException(nameof(dynamicAuthService));
        _principalRepo = principalRepo ?? throw new ArgumentNullException(nameof(principalRepo));
        _walletOwnershipRepo = walletOwnershipRepo ?? throw new ArgumentNullException(nameof(walletOwnershipRepo));
        _walletRepo = walletRepo ?? throw new ArgumentNullException(nameof(walletRepo));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string ProviderType => "dynamic";

    public bool CanHandle(AuthenticationRequest request)
    {
        return request is DynamicExchangeRequest;
    }

    public async Task<Result<AuthenticationData, Error>> AuthenticateAsync(
        AuthenticationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is not DynamicExchangeRequest dynamicRequest)
        {
            return Result.Failure<AuthenticationData, Error>(
                Error.Validation("Invalid request type for Dynamic provider"));
        }

        try
        {
            _logger.LogInformation("Starting Dynamic token exchange");

            // Validate Dynamic JWT using existing service
            var validationResult = await _dynamicAuthService.ValidateTokenAsync(
                dynamicRequest.Token,
                cancellationToken);

            if (validationResult.IsFailure)
            {
                _logger.LogWarning("Dynamic token validation failed: {Error}", validationResult.Error);
                return Result.Failure<AuthenticationData, Error>(validationResult.Error);
            }

            var dynamicUserData = validationResult.Value;

            // Ensure we have at least one wallet
            if (dynamicUserData.Wallets.Count == 0)
            {
                return Result.Failure<AuthenticationData, Error>(
                    Error.Validation("No verified wallets found in Dynamic token"));
            }

            // Process the primary wallet (first verified wallet)
            var primaryWallet = dynamicUserData.Wallets.First();
            var chainId = MapChainToChainId(primaryWallet.Chain);

            // Resolve or create principal for primary wallet
            var principalResult = await ResolveOrCreatePrincipalAsync(
                chainId,
                primaryWallet.Address,
                cancellationToken);

            if (principalResult.IsFailure)
            {
                return Result.Failure<AuthenticationData, Error>(principalResult.Error);
            }

            var principal = principalResult.Value;

            // Get or create Identity user
            var identityUser = await GetOrCreateIdentityUserAsync(
                principal,
                dynamicUserData,
                primaryWallet);

            if (identityUser == null)
            {
                return Result.Failure<AuthenticationData, Error>(
                    Error.Internal("Failed to create Identity user"));
            }

            // Update last authenticated timestamp
            identityUser.UpdateLastAuthenticated();
            await _userManager.UpdateAsync(identityUser);

            // Build additional claims for the response
            var additionalClaims = new Dictionary<string, object>
            {
                ["dynamic_user_id"] = dynamicUserData.AxonUserId,
                ["environment_id"] = dynamicUserData.EnvironmentId,
                ["email"] = dynamicUserData.Email ?? string.Empty,
                ["verified_wallets"] = JsonSerializer.Serialize(dynamicUserData.Wallets),
                ["auth_method"] = "jwt_exchange",
                ["is_new_user"] = dynamicUserData.IsNewUser
            };

            var authData = new AuthenticationData(
                User: identityUser,
                ProviderType: "dynamic",
                AdditionalClaims: additionalClaims);

            _logger.LogInformation("Dynamic authentication successful for user {UserId}",
                dynamicUserData.AxonUserId);

            return Result.Success<AuthenticationData, Error>(authData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dynamic authentication failed unexpectedly");
            return Result.Failure<AuthenticationData, Error>(
                Error.Internal("Dynamic authentication failed"));
        }
    }

    private static string MapChainToChainId(string chain)
    {
        // Map Dynamic.xyz chain names to our internal chain IDs
        return chain.ToLowerInvariant() switch
        {
            "solana" => "solana-mainnet",
            "ethereum" => "ethereum-mainnet",
            "polygon" => "polygon-mainnet",
            "arbitrum" => "arbitrum-mainnet",
            "optimism" => "optimism-mainnet",
            "base" => "base-mainnet",
            _ => $"{chain}-mainnet"
        };
    }

    private async Task<Result<AxonPrincipal, Error>> ResolveOrCreatePrincipalAsync(
        string chainId,
        string address,
        CancellationToken cancellationToken)
    {
        try
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
                    o.Status == OwnershipStatus.Verified &&
                    o.AccessMode == AccessMode.Signing);

                if (verifiedOwnership != null)
                {
                    var principal = await _principalRepo.GetByIdAsync(verifiedOwnership.PrincipalId, cancellationToken);
                    if (principal != null)
                    {
                        return Result.Success<AxonPrincipal, Error>(principal);
                    }
                }
            }

            // Create new principal and wallet
            var newPrincipalId = new AxonUserId(Guid.CreateVersion7());
            var newPrincipal = AxonPrincipal.CreateHuman(newPrincipalId);
            await _principalRepo.AddAsync(newPrincipal, cancellationToken);

            // Create wallet if it doesn't exist
            if (existingWallet == null)
            {
                existingWallet = Wallet.Create(
                    null, // Will generate a new ID
                    chainId, // Pass the original string
                    addressResult.Value);

                await _walletRepo.AddAsync(existingWallet, cancellationToken);
            }

            // Create wallet ownership
            var ownership = await _walletOwnershipRepo.CreateOwnershipAsync(
                newPrincipalId,
                existingWallet.Id,
                AccessMode.Signing,
                OwnershipStatus.Verified,
                VerificationSource.DynamicAttested,
                cancellationToken);

            _logger.LogInformation("Created new principal {PrincipalId} for Dynamic wallet {Address} on {ChainId}",
                newPrincipalId.Value, MaskAddress(address), chainId);

            return Result.Success<AxonPrincipal, Error>(newPrincipal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve or create principal");
            return Result.Failure<AxonPrincipal, Error>(
                Error.Internal("Failed to resolve user principal"));
        }
    }

    private async Task<AxonUserAuth?> GetOrCreateIdentityUserAsync(
        AxonPrincipal principal,
        DynamicUserData dynamicUserData,
        WalletData primaryWallet)
    {
        try
        {
            // Try to find existing Identity user by Dynamic user ID
            var existingUsers = await _userManager.GetUsersForClaimAsync(
                new Claim("dynamic_user_id", dynamicUserData.AxonUserId));

            var existingUser = existingUsers.FirstOrDefault();

            if (existingUser == null)
            {
                // Try to find by principal ID as fallback
                existingUsers = await _userManager.GetUsersForClaimAsync(
                    new Claim("axon_principal_id", principal.Id.Value.ToString()));
                existingUser = existingUsers.FirstOrDefault();
            }

            if (existingUser != null)
            {
                // Update last authenticated for existing user
                existingUser.UpdateLastAuthenticated();
                await _userManager.UpdateAsync(existingUser);
                return existingUser;
            }

            // Create new Identity user
            var newUser = AxonUserAuth.Create(
                principalId: principal.Id,
                providerType: "dynamic",
                issuer: "https://app.dynamic.xyz",
                subject: dynamicUserData.AxonUserId,
                dynamicEnvironmentId: dynamicUserData.EnvironmentId,
                dynamicUserId: dynamicUserData.AxonUserId);

            // Set primary wallet info
            var chainId = MapChainToChainId(primaryWallet.Chain);
            newUser.SetPrimaryWallet(chainId, primaryWallet.Address);

            // Set email if available
            if (!string.IsNullOrEmpty(dynamicUserData.Email))
            {
                newUser.Email = dynamicUserData.Email;
                newUser.NormalizedEmail = dynamicUserData.Email.ToUpperInvariant();
            }

            var createResult = await _userManager.CreateAsync(newUser);

            if (!createResult.Succeeded)
            {
                _logger.LogError("Failed to create Identity user: {Errors}",
                    string.Join(", ", createResult.Errors.Select(e => e.Description)));
                return null;
            }

            // Add claims
            await _userManager.AddClaimAsync(newUser,
                new Claim("axon_principal_id", principal.Id.Value.ToString()));
            await _userManager.AddClaimAsync(newUser,
                new Claim("dynamic_user_id", dynamicUserData.AxonUserId));
            await _userManager.AddClaimAsync(newUser,
                new Claim("wallet_address", primaryWallet.Address));
            await _userManager.AddClaimAsync(newUser,
                new Claim("chain_id", chainId));
            await _userManager.AddClaimAsync(newUser,
                new Claim("dynamic_environment_id", dynamicUserData.EnvironmentId));

            _logger.LogInformation("Created new Identity user {UserId} for Dynamic user {DynamicUserId}",
                newUser.Id, dynamicUserData.AxonUserId);

            return newUser;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get or create Identity user");
            return null;
        }
    }

    private static string MaskAddress(string address)
    {
        return address.Length > 8
            ? $"{address[..4]}...{address[^4..]}"
            : address;
    }
}