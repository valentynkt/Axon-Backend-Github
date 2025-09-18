namespace Axon.Api.Contracts.V1.Auth;

/// <summary>
/// Response containing current user information
/// </summary>
public sealed record GetCurrentUserResponseDto(
    string AxonUserId,
    string Subject,
    bool IsAuthenticated,
    Dictionary<string, object> Claims);