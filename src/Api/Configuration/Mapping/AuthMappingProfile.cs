using Axon.Api.Contracts.V1.Auth;
using Axon.Modules.Identity.Application.Commands.ExchangeToken;
using Axon.Modules.Identity.Application.DTOs.Exchange;
using Axon.Modules.Identity.Application.Queries.GetCurrentUser;
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
        // ExchangeTokenRequestDto -> ExchangeTokenCommand
        // Note: The JWT comes from the header, so it's handled in the endpoint itself
        config.NewConfig<ExchangeTokenRequestDto, ExchangeTokenCommand>()
            .ConstructUsing(src => new ExchangeTokenCommand(string.Empty)) // JWT will be set in endpoint
            .IgnoreNonMapped(true);

        // GetCurrentUserRequestDto -> GetCurrentUserQuery  
        // Note: The Principal comes from HttpContext, so it's handled in the endpoint itself
        config.NewConfig<GetCurrentUserRequestDto, GetCurrentUserQuery>()
            .ConstructUsing(src => new GetCurrentUserQuery(new System.Security.Claims.ClaimsPrincipal())) // Principal will be set in endpoint
            .IgnoreNonMapped(true);
    }

    private static void ConfigureResponseMappings(TypeAdapterConfig config)
    {
        // ExchangeOutcome -> ExchangeTokenResponseDto
        config.NewConfig<ExchangeOutcome, ExchangeTokenResponseDto>()
            .Map(dest => dest.AxonId, src => src.AxonId)
            .Map(dest => dest.WalletsProcessed, src => src.WalletsProcessed)
            .Map(dest => dest.WalletsLinked, src => src.WalletsLinked)
            .Map(dest => dest.DefaultsApplied, src => src.DefaultsApplied)
            .Map(dest => dest.Skipped, src => src.Skipped)
            .Map(dest => dest.Conflicts, src => src.Conflicts);

        // CurrentUserInfo -> GetCurrentUserResponseDto
        config.NewConfig<CurrentUserInfo, GetCurrentUserResponseDto>()
            .Map(dest => dest.AxonId, src => src.AxonId)
            .Map(dest => dest.Subject, src => src.Subject)
            .Map(dest => dest.IsAuthenticated, src => src.IsAuthenticated)
            .Map(dest => dest.Claims, src => src.Claims);
    }
}