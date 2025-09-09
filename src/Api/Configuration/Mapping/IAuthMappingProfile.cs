namespace Axon.Api.Configuration.Mapping;

/// <summary>
/// Marker interface for Auth mapping profile
/// </summary>
public interface IAuthMappingProfile
{
    /// <summary>
    /// Gets the profile name for debugging
    /// </summary>
    string ProfileName { get; }
}