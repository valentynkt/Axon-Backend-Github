namespace Axon.Shared.Common.Abstractions;

/// <summary>
/// Marker interface for requests that return no value
/// </summary>
public interface IRequest
{
}

/// <summary>
/// Marker interface for requests that return a value of type TResponse
/// </summary>
/// <typeparam name="TResponse">The type of the response</typeparam>
public interface IRequest<out TResponse> : IRequest
{
}