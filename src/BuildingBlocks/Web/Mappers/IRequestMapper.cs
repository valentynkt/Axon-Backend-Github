using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;

namespace BuildingBlocks.Web.Mappers;

/// <summary>
/// Maps HTTP requests to domain commands or queries.
/// </summary>
/// <typeparam name="TRequest">The HTTP request type</typeparam>
/// <typeparam name="TCommand">The domain command/query type</typeparam>
public interface IRequestMapper<in TRequest, TCommand>
{
    /// <summary>
    /// Maps an HTTP request to a domain command/query.
    /// </summary>
    /// <param name="request">The HTTP request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A Result containing the mapped command or validation errors</returns>
    Task<Result<TCommand, Error>> MapAsync(TRequest request, CancellationToken cancellationToken = default);
}