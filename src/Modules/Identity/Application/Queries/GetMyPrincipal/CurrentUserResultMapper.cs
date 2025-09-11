using Axon.Modules.Identity.Application.DTOs.Responses;
using Axon.Modules.Identity.Domain.Enums;

namespace Axon.Modules.Identity.Application.Queries.GetMyPrincipal;

/// <summary>
/// Mapper for converting domain models to CurrentUserResult wire format.
/// Provides consistent mapping logic for the /auth/me endpoint.
/// </summary>
public static class CurrentUserResultMapper
{
    /// <summary>
    /// Maps domain RiskTier enum to wire format string.
    /// </summary>
    public static string MapRiskTierToWire(RiskTier riskTier) => riskTier switch
    {
        RiskTier.Low => "low",
        RiskTier.Medium => "medium", 
        RiskTier.High => "high",
        _ => "low"
    };

    /// <summary>
    /// Maps domain AccessMode enum to wire format string.
    /// </summary>
    public static string MapAccessModeToWire(AccessMode accessMode) => accessMode switch
    {
        AccessMode.WatchOnly => "readonly",
        AccessMode.Signing => "signing",
        _ => "readonly"
    };
}