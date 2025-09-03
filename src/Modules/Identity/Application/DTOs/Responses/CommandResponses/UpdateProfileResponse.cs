using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Application.DTOs.Responses.CommandResponses;

/// <summary>
/// Status enumeration for UpdateProfile operation.
/// </summary>
public enum UpdateProfileStatus
{
    Updated,
    NoOp
}

/// <summary>
/// Indicates which fields were changed in the profile update.
/// </summary>
public sealed record ProfileChanges(
    bool Language,
    bool RiskTier
);

/// <summary>
/// Response for UpdateProfile command.
/// </summary>
public sealed record UpdateProfileResponse(
    AxonId AxonId,
    string PreferredLanguage,
    string RiskTier,
    ProfileChanges Changed,
    UpdateProfileStatus Status
);