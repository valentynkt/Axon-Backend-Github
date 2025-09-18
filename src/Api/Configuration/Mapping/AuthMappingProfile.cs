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
            .Map(dest => dest.Subject, src => src.Profile.Subject) // Use actual subject from JWT
            .Map(dest => dest.IsAuthenticated, src => true) // Always true if we have a result
            .Map(dest => dest.Claims, src => new Dictionary<string, object>
            {
                { "risk_tier", src.Profile.RiskTier },
                { "wallet_count", src.Wallets.Count },
                { "chain_defaults", src.ChainDefaults }
            });
    }
}