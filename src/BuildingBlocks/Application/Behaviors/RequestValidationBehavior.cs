using MediatR;
using Microsoft.Extensions.Logging;
using BuildingBlocks.Core.Abstractions.CQRS;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Application.Validation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// Runs validation for the request using FluentValidation and converts failures to the unified Result error shape.
/// - Uses FluentValidation IValidator directly for validation
/// - Groups errors by Field for cleaner client payloads
/// - Uses compiled delegates to create Result/Result&lt;T&gt; failures (no reflection on hot path)
/// - Emits OpenTelemetry metrics and traces
/// - Supports skip attributes (SkipValidation, ISystemCommand)
/// </summary>
public sealed class RequestValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class, IAxonRequest<TResponse>
    where TResponse : IResult
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    private readonly ILogger<RequestValidationBehavior<TRequest, TResponse>> _logger;

    public RequestValidationBehavior(
        IEnumerable<IValidator<TRequest>> validators,
        ILogger<RequestValidationBehavior<TRequest, TResponse>> logger)
    {
        _validators = validators ?? throw new ArgumentNullException(nameof(validators));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Check skip conditions
        if (ShouldSkipValidation(request))
        {
            _logger.LogDebug("Skipping validation for {RequestType}", typeof(TRequest).Name);
            return await next();
        }

        var stopwatch = Stopwatch.StartNew();
        var requestType = typeof(TRequest).Name;

        _logger.LogDebug("Validating {RequestType}", requestType);

        // Run FluentValidation in parallel for better performance
        var validationTasks = _validators.Select(async validator =>
        {
            var context = new ValidationContext<TRequest>(request);
            
            // Add metadata if available from the request
            if (request.TraceId != null)
                context.RootContextData["TraceId"] = request.TraceId;
            if (request.SpanId != null)
                context.RootContextData["SpanId"] = request.SpanId;
            context.RootContextData["RequestId"] = request.RequestId;
            context.RootContextData["RequestedAt"] = request.RequestedAt;
            
            return await validator.ValidateAsync(context, cancellationToken);
        });

        var validationResults = await Task.WhenAll(validationTasks);
        var validationFailures = validationResults.SelectMany(r => r.Errors).ToList();

        stopwatch.Stop();

        // Emit metrics
        EmitMetrics(validationFailures.Count == 0, validationFailures.Count, stopwatch.ElapsedMilliseconds);

        if (validationFailures.Count > 0)
        {
            _logger.LogWarning("{RequestType} validation failed with {ErrorCount} errors", requestType, validationFailures.Count);

            // Convert FluentValidation failures to Error objects
            var errors = validationFailures
                .GroupBy(e => e.PropertyName ?? "_global")
                .Select(g =>
                {
                    var message = string.Join("; ", g.Select(x => x.ErrorMessage));
                    var codes = g.Select(x => x.ErrorCode).Where(c => !string.IsNullOrEmpty(c)).Distinct().ToArray();

                    return Error.Validation(
                        message: message,
                        code: codes.FirstOrDefault() ?? "VAL.GENERIC",
                        metadata: new Dictionary<string, object>
                        {
                            ["Field"] = g.Key,
                            ["Count"] = g.Count(),
                            ["Codes"] = codes,
                            ["Severity"] = g.Max(x => x.Severity).ToString(),
                            ["AttemptedValuesSample"] = g.Select(x => x.AttemptedValue ?? "<null>").Take(3).ToArray()
                        });
                })
                .ToArray();

            var aggregated = errors.Length == 1 ? errors[0] : Error.Aggregate(errors);
            return ResultFailureFactory<TResponse>.FromError(aggregated);
        }

        return await next();
    }

    private static bool ShouldSkipValidation(TRequest request)
    {
        // Check for SkipValidation attribute
        var skipAttribute = request.GetType().GetCustomAttributes(typeof(SkipValidationAttribute), false).Length != 0;
        if (skipAttribute) return true;

        // Check if request implements ISystemCommand (trusted path)
        if (request is ISystemCommand) return true;

        return false;
    }

    private static void EmitMetrics(bool isValid, int errorCount, long elapsedMs)
    {
        var activity = Activity.Current;
        if (activity != null)
        {
            activity.SetTag("axon.validation.outcome", isValid ? "success" : "failure");
            activity.SetTag("axon.validation.error_count", errorCount);
            activity.SetTag("axon.validation.duration_ms", elapsedMs);
        }
    }

    private static class ResultFailureFactory<T>
        where T : IResult
    {
        public static readonly Func<Error, T> FromError = Build();

        private static Func<Error, T> Build()
        {
            if (typeof(T) == typeof(Result))
            {
                return e => (T)(object)Result.Failure(e);
            }

            if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(Result<>))
            {
                var method = typeof(T).GetMethod("Failure", new[] { typeof(Error) })
                             ?? throw new InvalidOperationException($"{typeof(T).Name}.Failure(Error) missing");
                var e = System.Linq.Expressions.Expression.Parameter(typeof(Error), "e");
                var call = System.Linq.Expressions.Expression.Call(method, e);
                var lambda = System.Linq.Expressions.Expression.Lambda<Func<Error, T>>(call, e);
                return lambda.Compile();
            }

            throw new InvalidOperationException($"ResultValidationBehavior requires TResponse : IResult. Found {typeof(T).Name}");
        }
    }
}
