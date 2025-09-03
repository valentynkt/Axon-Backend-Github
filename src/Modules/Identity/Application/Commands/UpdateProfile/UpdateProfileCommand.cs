using Axon.Modules.Identity.Application.Common.Commands;
using Axon.Modules.Identity.Application.DTOs.Responses.CommandResponses;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Application.Commands.UpdateProfile;

/// <summary>
/// Updates language and/or risk tier in one call for a specific principal.
/// Supports partial updates (either or both fields can be provided).
/// </summary>
public sealed record UpdateProfileCommand(
    string? CorrelationId,
    AxonId AxonId,
    string? PreferredLanguage,
    string? RiskTier
) : IdentityBaseCommand<UpdateProfileResponse>;