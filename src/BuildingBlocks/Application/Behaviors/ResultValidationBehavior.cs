using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using BuildingBlocks.Core.Abstractions.CQRS;
using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// Runs all FluentValidation validators for the request and converts failures to the unified Result error shape.
/// - No service locator: validators are injected as IEnumerable<IValidator<TRequest>>.
/// - Groups errors by Property for cleaner client payloads.
/// - Uses compiled delegates to create Result/Result&lt;T&gt; failures (no reflection on hot path).
/// - Calls next() correctly (MediatR delegate has no CancellationToken parameter).
/// </summary>
public sealed class ResultValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class, IAxonRequest<TResponse>
    where TResponse : IResult
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    private readonly ILogger<ResultValidationBehavior<TRequest, TResponse>> _logger;

    public ResultValidationBehavior(
        IEnumerable<IValidator<TRequest>> validators,
        ILogger<ResultValidationBehavior<TRequest, TResponse>> logger)
    {
        _validators = validators ?? [];
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (_validators.Any())
        {
            _logger.LogDebug("Validating {RequestType} with {Count} validators", typeof(TRequest).Name, _validators.Count());
            var context = new ValidationContext<TRequest>(request);
            var failures = new List<FluentValidation.Results.ValidationFailure>();

            foreach (var v in _validators)
            {
                var result = await v.ValidateAsync(context, cancellationToken);
                if (!result.IsValid) failures.AddRange(result.Errors);
            }

            if (failures.Count > 0)
            {
                _logger.LogWarning("{RequestType} validation failed with {ErrorCount} errors", typeof(TRequest).Name, failures.Count);

                // Group by Property for a cleaner client contract and lower payload noise
                var groupedErrors = failures
                    .GroupBy(f => string.IsNullOrWhiteSpace(f.PropertyName) ? "VALIDATION" : f.PropertyName)
                    .Select(g =>
                    {
                        var message = string.Join("; ", g.Select(x => x.ErrorMessage));
                        var codes = g.Select(x => string.IsNullOrWhiteSpace(x.ErrorCode) ? "VALIDATION_ERROR" : x.ErrorCode)
                                     .Distinct()
                                     .ToArray();

                        return Error.Validation(
                            message: message,
                            code: g.Key, // property name or "VALIDATION"
                            metadata: new Dictionary<string, object>
                            {
                                ["Count"] = g.Count(),
                                ["Codes"] = codes,
                                // show a few attempted values for diagnostics (avoid large dumps)
                                ["AttemptedValuesSample"] = g.Select(x => x.AttemptedValue ?? "<null>").Take(3).ToArray()
                            });
                    })
                    .ToArray();

                var aggregated = groupedErrors.Length == 1 ? groupedErrors[0] : Error.Aggregate(groupedErrors);
                return ResultFailureFactory<TResponse>.FromError(aggregated);
            }
        }

        // MediatR delegate has no token parameter
        return await next();
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
