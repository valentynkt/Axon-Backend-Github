using Axon.Modules.Identity.Application.DTOs.Exchange;
using Axon.Modules.Identity.Application.Services;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Bridge service that validates Dynamic JWTs and converts to Application-layer DTOs
/// </summary>
public sealed class DynamicJwtBridge : IDynamicJwtBridge
{
    private readonly IDynamicAuthService _dynamicAuthService;
    private readonly ILogger<DynamicJwtBridge> _logger;

    public DynamicJwtBridge(
        IDynamicAuthService dynamicAuthService,
        ILogger<DynamicJwtBridge> logger)
    {
        _dynamicAuthService = dynamicAuthService ?? throw new ArgumentNullException(nameof(dynamicAuthService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<ExchangeUserData, Error>> ValidateAndNormalizeAsync(
        string jwt, 
        CancellationToken cancellationToken = default)
    {
        // Validate using existing infrastructure service
        var validationResult = await _dynamicAuthService.ValidateTokenAsync(jwt, cancellationToken);
        if (validationResult.IsFailure)
        {
            return Result.Failure<ExchangeUserData, Error>(validationResult.Error);
        }

        // Convert Infrastructure DTO to Application DTO
        var infraUserData = validationResult.Value;
        var exchangeUserData = ConvertToExchangeUserData(infraUserData);

        _logger.LogDebug("Converted Dynamic user {UserId} data for Application layer", exchangeUserData.UserId);
        return Result.Success<ExchangeUserData, Error>(exchangeUserData);
    }

    /// <summary>
    /// Converts Infrastructure DynamicUserData to Application ExchangeUserData
    /// </summary>
    private static ExchangeUserData ConvertToExchangeUserData(DynamicUserData infraData)
    {
        var wallets = infraData.Wallets.Select(ConvertToExchangeWalletData).ToList();

        // Create additional metadata from infrastructure data
        var additionalMetadata = new Dictionary<string, object>();
        
        if (!string.IsNullOrWhiteSpace(infraData.SessionPublicKey))
            additionalMetadata["sessionPublicKey"] = infraData.SessionPublicKey;

        if (infraData.VerifiedCredentialsHashes != null && infraData.VerifiedCredentialsHashes.Count > 0)
            additionalMetadata["verifiedCredentialsHashes"] = infraData.VerifiedCredentialsHashes;

        return new ExchangeUserData(
            UserId: infraData.UserId,
            Email: infraData.Email,
            EnvironmentId: infraData.EnvironmentId,
            Wallets: wallets,
            FirstVisitUtc: infraData.FirstVisitUtc,
            LastVisitUtc: infraData.LastVisitUtc,
            IsNewUser: infraData.IsNewUser,
            AdditionalMetadata: additionalMetadata.Count > 0 ? additionalMetadata : null
        );
    }

    /// <summary>
    /// Converts Infrastructure WalletData to Application ExchangeWalletData
    /// </summary>
    private static ExchangeWalletData ConvertToExchangeWalletData(WalletData infraWallet)
    {
        return new ExchangeWalletData(
            Address: infraWallet.Address,
            Chain: infraWallet.Chain,
            WalletName: infraWallet.WalletName,
            Provider: infraWallet.Provider,
            ConnectedAtUtc: infraWallet.ConnectedAtUtc
        );
    }
}