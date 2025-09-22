using Axon.Api.Contracts.V1.Auth;
using Axon.Modules.Identity.Application.Commands.ExchangeCredential;
using Axon.Modules.Identity.Application.DTOs.Exchange;
using Axon.Modules.Identity.Application.DTOs.Responses;
using Axon.Modules.Identity.Application.Queries.GetMyPrincipal;
using Mapster;

namespace Axon.Api.Configuration.Mapping;

/// <summary>
/// Mapping profile for Auth module API contracts
/// </summary>
public sealed class AuthMappingProfile : IRegister, IAuthMappingProfile
{
    public string ProfileName => "AuthAPI";

    public void Register(TypeAdapterConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        
        ConfigureRequestMappings(config);
        ConfigureResponseMappings(config);
    }

    private static void ConfigureRequestMappings(TypeAdapterConfig config)
    {
        // Note: Request mappings are handled entirely in the endpoint logic
        // The endpoints construct commands/queries directly using JWT data and claims
        // No mapping configurations needed as endpoints handle construction directly
        _ = config; // Suppress unused parameter warning
    }

    private static void ConfigureResponseMappings(TypeAdapterConfig config)
    {
        // ExchangeOutcome -> ExchangeTokenResponseDto
        config.NewConfig<ExchangeOutcome, ExchangeTokenResponseDto>()
            .Map(dest => dest.AxonUserId, src => src.AxonUserId)
            .Map(dest => dest.WalletsProcessed, src => src.WalletsProcessed)
            .Map(dest => dest.WalletsLinked, src => src.WalletsLinked)
            .Map(dest => dest.DefaultsApplied, src => src.DefaultsApplied)
            .Map(dest => dest.Skipped, src => src.Skipped)
            .Map(dest => dest.Conflicts, src => src.Conflicts);

        // CurrentUserResult -> GetCurrentUserResponseDto
        // Map from new CurrentUserResult structure to existing API contract
        config.NewConfig<CurrentUserResult, GetCurrentUserResponseDto>()
            .Map(dest => dest.AxonUserId, src => src.Profile.AxonUserId)
            .Map(dest => dest.IsAuthenticated, src => true) // Always true if we have a result
            .Map(dest => dest.Environment, src => "mainnet") // TODO: Extract from context
            .Map(dest => dest.RiskPosture, src => src.Profile.RiskTier)
            .Map(dest => dest.Providers, src => new ProviderLinkDto[]
            {
                new("dynamic", "dynamic.xyz", src.Profile.Subject)
            })
            .Map(dest => dest.Wallets, src => src.Wallets.Select(w => new WalletDto(
                w.WalletId,
                w.ChainId,
                w.Address,
                w.IsVerified ? "verified" : "pending",
                w.AccessMode,
                null, // Provider not available in WalletInfo
                null  // DisplayName not available in WalletInfo
            )).ToArray())
            .Map(dest => dest.ChainDefaults, src => src.ChainDefaults.Select(kv =>
                new ChainDefaultDto(kv.Key, kv.Value)).ToArray());
    }
}