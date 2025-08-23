namespace Axon.Api.Configuration.Mapping;

/// <summary>
/// Interface for Chat module mapping profiles
/// </summary>
public interface IChatMappingProfile
{
    /// <summary>
    /// Gets the profile name for identification
    /// </summary>
    string ProfileName { get; }
}