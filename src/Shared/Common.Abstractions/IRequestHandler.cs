namespace Axon.Shared.Common.Abstractions;

/// <summary>
/// Handler for requests that return no value
/// </summary>
/// <typeparam name="TRequest">The type of request being handled</typeparam>
public interface IRequestHandler<in TRequest>
    where TRequest : IRequest
{
    /// <summary>
    /// Handle the request
    /// </summary>
    /// <param name="request">The request to handle</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task HandleAsync(TRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Handler for requests that return a value
/// </summary>
/// <typeparam name="TRequest">The type of request being handled</typeparam>
/// <typeparam name="TResponse">The type of response being returned</typeparam>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Handle the request and return a response
    /// </summary>
    /// <param name="request">The request to handle</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task containing the response</returns>
    Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken);
}