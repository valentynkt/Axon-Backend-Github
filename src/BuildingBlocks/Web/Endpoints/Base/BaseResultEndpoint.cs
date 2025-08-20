using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Web.Extensions;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Web.Endpoints.Base;

/// <summary>
/// Base endpoint that integrates with the Result pattern for consistent error handling.
/// </summary>
/// <typeparam name="TRequest">The request type</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
public abstract class BaseResultEndpoint<TRequest, TResponse> : BaseEndpoint<TRequest, TResponse>
    where TRequest : notnull
{
    protected BaseResultEndpoint(ILogger logger) : base(logger)
    {
    }

    public override async Task HandleAsync(TRequest req, CancellationToken ct)
    {
        LogRequestReceived();

        try
        {
            var result = await ExecuteAsync(req, ct);
            
            if (result.IsSuccess)
            {
                LogRequestCompleted();
                Response = result.Value;
            }
            else
            {
                Logger.LogWarning(
                    "Request failed with domain error: {ErrorCode} - {ErrorMessage} (traceId={TraceId})",
                    result.Error.Code,
                    result.Error.Message,
                    HttpContext.TraceIdentifier);
                    
                await HttpContext.SendProblemDetailsAsync(result.Error, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            LogRequestCancelled();
            throw;
        }
        catch (Exception ex)
        {
            LogRequestFailed(ex);
            await HttpContext.SendProblemDetailsAsync(
                BuildingBlocks.Core.Diagnostics.Errors.Error.Internal(
                    $"An unexpected error occurred: {ex.Message}",
                    "UNEXPECTED_ERROR",
                    ex),
                ct);
        }
    }

    /// <summary>
    /// Executes the business logic and returns a Result.
    /// </summary>
    /// <param name="request">The incoming request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A Result containing the response or an error</returns>
    protected abstract Task<Result<TResponse>> ExecuteAsync(TRequest request, CancellationToken cancellationToken);
}