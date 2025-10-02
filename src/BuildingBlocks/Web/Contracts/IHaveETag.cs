namespace BuildingBlocks.Web.Contracts;

/// <summary>
/// Marker interface for responses that support ETag caching
/// Responses implementing this interface will automatically have ETag headers added
/// </summary>
public interface IHaveETag
{
    /// <summary>
    /// The ETag value representing the current version of this resource
    /// </summary>
    string ETag { get; }
}
