using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;

namespace BuildingBlocks.Web.Mappers;

/// <summary>
/// Maps domain results to HTTP responses.
/// </summary>
/// <typeparam name="TDomainResult">The domain result type</typeparam>
/// <typeparam name="TResponse">The HTTP response type</typeparam>
public interface IResponseMapper<in TDomainResult, TResponse>
{
    /// <summary>
    /// Maps a domain result to an HTTP response.
    /// </summary>
    /// <param name="domainResult">The domain result</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A Result containing the mapped response</returns>
    Task<Result<TResponse, Error>> MapAsync(TDomainResult domainResult, CancellationToken cancellationToken = default);
}