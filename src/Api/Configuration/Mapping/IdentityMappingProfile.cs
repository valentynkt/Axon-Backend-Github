using Axon.Api.Contracts.V1.Identity.Authentication;
using Axon.Api.Contracts.V1.Identity.Common;
using Axon.Api.Endpoints.V1.Auth;
using Axon.Modules.Identity.Application.Queries.GetCurrentUser;
using Mapster;

namespace Axon.Api.Configuration.Mapping;

/// <summary>
/// Configures mapping between Identity domain models and DTOs
/// </summary>
public class IdentityMappingProfile : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        // Map CurrentUserResult to CurrentUserResponseDto
        config.NewConfig<CurrentUserResult, CurrentUserResponseDto>()
            .Map(dest => dest.User, src => src.User)
            .Map(dest => dest.Wallets, src => src.Wallets)
            .Map(dest => dest.SyncedAt, src => src.SyncedAt)
            .Map(dest => dest.SyncStatus, src => src.SyncStatus);
            
        // Map UserProfile to UserProfileDto
        config.NewConfig<UserProfile, UserProfileDto>()
            .Map(dest => dest.Id, src => src.Id)
            .Map(dest => dest.DynamicUserId, src => src.DynamicUserId)
            .Map(dest => dest.Email, src => src.Email)
            .Map(dest => dest.DisplayName, src => src.DisplayName)
            .Map(dest => dest.Username, src => src.Username)
            .Map(dest => dest.FirstVisit, src => src.FirstVisit)
            .Map(dest => dest.LastVisit, src => src.LastVisit)
            .Map(dest => dest.Metadata, src => src.Metadata);
            
        // Map WalletData to WalletInfo
        config.NewConfig<WalletData, WalletInfo>()
            .Map(dest => dest.Id, src => src.Id)
            .Map(dest => dest.Address, src => src.Address)
            .Map(dest => dest.Chain, src => src.Chain)
            .Map(dest => dest.Provider, src => src.Provider)
            .Map(dest => dest.WalletName, src => src.WalletName)
            .Map(dest => dest.ConnectedAt, src => src.ConnectedAt);
    }
}