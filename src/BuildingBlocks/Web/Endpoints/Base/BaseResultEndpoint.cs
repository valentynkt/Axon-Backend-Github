using BuildingBlocks.Core.Diagnostics.Errors;
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

                // Debug: Check if result.Value is null
                if (result.Value == null)
                {
                    Logger.LogError("CRITICAL: result.Value is NULL despite IsSuccess=true. Type: {Type}",
                        typeof(TResponse).Name);
                    await HttpContext.SendProblemDetailsAsync(
                        Error.Internal("Response is null", "NULL_RESPONSE"), ct);
                    return;
                }

                Logger.LogInformation("Setting Response to value of type {Type}", result.Value.GetType().Name);
                Response = result.Value;
                Logger.LogInformation("Response has been set. Response is null: {IsNull}", Response == null);
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
                Error.Internal(
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
    protected abstract new Task<Result<TResponse, Error>> ExecuteAsync(TRequest request, CancellationToken cancellationToken);
}