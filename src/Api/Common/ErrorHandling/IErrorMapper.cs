using Axon.Shared.Common;

namespace Axon.Api.Common.ErrorHandling;

/// <summary>
/// Interface for mapping domain errors to HTTP responses
/// </summary>
public interface IErrorMapper
{
    /// <summary>
    /// Maps a domain error to an HTTP status code
    /// </summary>
    /// <param name="error">Domain error</param>
    /// <returns>HTTP status code</returns>
    int MapToStatusCode(Error error);

    /// <summary>
    /// Maps a domain error to a problem details object
    /// </summary>
    /// <param name="error">Domain error</param>
    /// <returns>Problem details for HTTP response</returns>
    object MapToProblemDetails(Error error);
}