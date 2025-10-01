using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Web.Extensions;
using BuildingBlocks.Web.Endpoints.Base;
using CSharpFunctionalExtensions;
using Mapster;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Web.Endpoints.Base;

/// <summary>
/// Base endpoint that provides convenient mapping operations with Result pattern
/// </summary>
/// <typeparam name="TRequest">The HTTP request type</typeparam>
/// <typeparam name="TResponse">The HTTP response type</typeparam>
public abstract class BaseMappedEndpoint<TRequest, TResponse> : BaseResultEndpoint<TRequest, TResponse>
    where TRequest : notnull
{
    protected BaseMappedEndpoint(ILogger logger) : base(logger) { }

    /// <summary>
    /// Maps request to command/query type with error handling
    /// </summary>
    /// <typeparam name="TCommand">The command/query type</typeparam>
    /// <param name="request">The request to map</param>
    /// <returns>Result containing mapped command or error</returns>
    protected Result<TCommand, Error> MapRequest<TCommand>(TRequest request)
        where TCommand : notnull
    {
        return request.AdaptSafely<TCommand>();
    }

    /// <summary>
    /// Maps domain result to response type with error handling
    /// </summary>
    /// <typeparam name="TDomainResult">The domain result type</typeparam>
    /// <param name="domainResult">The domain result to map</param>
    /// <returns>Result containing mapped response or error</returns>
    protected Result<TResponse, Error> MapResponse<TDomainResult>(TDomainResult domainResult)
        where TDomainResult : notnull
    {
        return domainResult.AdaptSafely<TResponse>();
    }

    /// <summary>
    /// Maps request to command, executes operation, and maps response in a single fluent chain
    /// </summary>
    /// <typeparam name="TCommand">The command type</typeparam>
    /// <typeparam name="TDomainResult">The domain result type</typeparam>
    /// <param name="request">The request to process</param>
    /// <param name="executeOperation">The operation to execute with the command</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing final response or error</returns>
    protected async Task<Result<TResponse, Error>> MapExecuteMap<TCommand, TDomainResult>(
        TRequest request,
        Func<TCommand, CancellationToken, Task<Result<TDomainResult, Error>>> executeOperation,
        CancellationToken cancellationToken)
        where TCommand : notnull
        where TDomainResult : notnull
    {
        var commandResult = MapRequest<TCommand>(request);
        if (commandResult.IsFailure)
            return Result.Failure<TResponse, Error>(commandResult.Error);

        var executionResult = await executeOperation(commandResult.Value, cancellationToken);
        if (executionResult.IsFailure)
            return Result.Failure<TResponse, Error>(executionResult.Error);

        // Debug: Check if execution result value is null despite success
        if (executionResult.Value == null)
        {
            Logger.LogError("CRITICAL: ExecutionResult.Value is NULL despite IsSuccess=true. Type: {Type}",
                typeof(TDomainResult).Name);
            return Result.Failure<TResponse, Error>(
                Error.Internal(
                    $"Command execution returned null {typeof(TDomainResult).Name} despite success", 
                    "NULL_EXECUTION_RESULT"));
        }

        return MapResponse<TDomainResult>(executionResult.Value);
    }

    /// <summary>
    /// Logs mapping operations for debugging
    /// </summary>
    /// <typeparam name="TSource">Source type</typeparam>
    /// <typeparam name="TDest">Destination type</typeparam>
    /// <param name="source">Source object</param>
    /// <returns>Mapped object</returns>
    protected TDest MapWithLogging<TSource, TDest>(TSource source)
        where TSource : notnull
    {
        Logger.LogDebug("Mapping from {SourceType} to {DestType}", typeof(TSource).Name, typeof(TDest).Name);
        
        var result = source.Adapt<TDest>();
        
        Logger.LogDebug("Successfully mapped from {SourceType} to {DestType}", typeof(TSource).Name, typeof(TDest).Name);
        
        return result;
    }
}