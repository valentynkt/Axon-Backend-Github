namespace Axon.Api.Contracts.V1.Auth;

/// <summary>
/// Response containing current user information
/// </summary>
public sealed record GetCurrentUserResponseDto(
    string AxonId,
    string Subject,
    bool IsAuthenticated,
    Dictionary<string, object> Claims);