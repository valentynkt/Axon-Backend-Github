using System.Linq.Expressions;
using BuildingBlocks.Core.Domain.CQRS;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Diagnostics.Errors;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// Envelope-only domain validation for commands implementing <see cref="DomainCommandBase"/>.
/// Runs AFTER FluentValidation (ResultValidationBehavior) and BEFORE handler logic.
/// Pure pre-execution checks only (no I/O, no aggregate mutation).
/// Converts failures to the unified Result/Result&lt;T&gt; shape; uses compiled delegates (no reflection on hot path).
/// </summary>
public sealed class DomainValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : IResult
{
    private readonly ILogger<DomainValidationBehavior<TRequest, TResponse>> _logger;

    public DomainValidationBehavior(ILogger<DomainValidationBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Only validate domain commands with pre-execution, envelope-only checks
        if (request is not DomainCommandBase domainCommand)
            return await next(); // MediatR delegate has no token parameter

        var commandType = typeof(TRequest).Name;
        var aggregateType = domainCommand.GetAggregateType().Name;

        _logger.LogDebug("Executing domain validation for {CommandType} on aggregate {AggregateType}",
            commandType, aggregateType);

        // MUST be pure (no DB calls / side effects)
        var validation = domainCommand.ValidateDomainRules();

        if (validation.IsInvalid)
        {
            var errorsArray = validation.Errors.ToArray();

            _logger.LogWarning("Domain validation failed for {CommandType}: {Errors}",
                commandType,
                string.Join(", ", errorsArray.Select(e => $"{e.Code}: {e.Message}")));

            var aggregated = errorsArray.Length == 1
                ? errorsArray[0]
                : Error.Aggregate(errorsArray);

            // Return failed Result/Result<T> via compiled factory (no reflection per call)
            return ResultFailureFactory<TResponse>.FromError(aggregated);
        }

        _logger.LogDebug("Domain validation passed for {CommandType}", commandType);
        return await next(); // MediatR delegate has no token parameter
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

                var e = Expression.Parameter(typeof(Error), "e");
                var call = Expression.Call(method, e);
                var lambda = Expression.Lambda<Func<Error, T>>(call, e);
                return lambda.Compile();
            }

            throw new InvalidOperationException($"DomainValidationBehavior requires TResponse : IResult. Found {typeof(T).Name}");
        }
    }
}

/// <summary>
/// Exception kept for completeness if you ever run non-Result responses outside this behavior.
/// Not used when TResponse : IResult (current pipeline contract).
/// </summary>
public sealed class DomainValidationException : Exception
{
    public Error[] ValidationErrors { get; }

    public DomainValidationException(string message, Error[] validationErrors)
        : base(message) => ValidationErrors = validationErrors;

    public DomainValidationException(string message, Error[] validationErrors, Exception innerException)
        : base(message, innerException) => ValidationErrors = validationErrors;
}
