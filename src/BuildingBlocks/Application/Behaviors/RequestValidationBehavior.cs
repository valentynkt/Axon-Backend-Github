// /BuildingBlocks/Application/Behaviors/Validation/RequestValidationBehavior.cs
#nullable enable
using System.Diagnostics;
using BuildingBlocks.Application.Validation;
using BuildingBlocks.Core.Abstractions.CQRS;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// Single validation behavior for all requests returning Result&lt;TValue, Error&gt;.
/// Works for queries (Result&lt;TView, Error&gt;) and commands (Result&lt;Unit, Error&gt;).
/// - Skips when [SkipValidation] or request is ISystemCommand
/// - Enriches FluentValidation context with trace/request data if IAxonRequest
/// - Returns Result.Failure&lt;TValue, Error&gt; with an aggregate Error on failure
/// - Adds lightweight Activity tags for observability
/// </summary>
public sealed class RequestValidationBehavior<TRequest, TValue>
    : IPipelineBehavior<TRequest, Result<TValue, Error>>
    where TRequest : IRequest<Result<TValue, Error>>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    private readonly ILogger<RequestValidationBehavior<TRequest, TValue>> _logger;

    public RequestValidationBehavior(
        IEnumerable<IValidator<TRequest>> validators,
        ILogger<RequestValidationBehavior<TRequest, TValue>> logger)
    {
        _validators = validators ?? throw new ArgumentNullException(nameof(validators));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<TValue, Error>> Handle(
        TRequest request,
        RequestHandlerDelegate<Result<TValue, Error>> next,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(next);

        if (_validators is null || !_validators.Any() || ShouldSkipValidation(request))
            return await next();

        var sw = Stopwatch.StartNew();
        _logger.LogDebug("Validating {RequestType}", typeof(TRequest).Name);

        var context = new ValidationContext<TRequest>(request);

        // Add trace/request metadata when available
        if (request is IAxonRequest axon)
        {
            if (axon.TraceId is not null) context.RootContextData["TraceId"] = axon.TraceId;
            if (axon.SpanId  is not null) context.RootContextData["SpanId"]  = axon.SpanId;
            context.RootContextData["RequestId"]   = axon.RequestId;
            context.RootContextData["RequestedAt"] = axon.RequestedAt;
        }

        // Run validators (sequential; parallel rarely pays off and complicates lifetimes)
        var failures = new List<ValidationFailure>(capacity: 8);
        foreach (var v in _validators)
        {
            var res = await v.ValidateAsync(context, ct).ConfigureAwait(false);
            if (!res.IsValid) failures.AddRange(res.Errors);
        }

        sw.Stop();
        EmitMetrics(failures.Count == 0, failures.Count, sw.ElapsedMilliseconds);

        if (failures.Count == 0)
            return await next();

        _logger.LogWarning("{RequestType} validation failed with {Count} error(s)",
            typeof(TRequest).Name, failures.Count);

        var error = ValidationErrorMapper.FromFailures(failures, request);
        return Result.Failure<TValue, Error>(error);
    }

    private static bool ShouldSkipValidation(TRequest request)
    {
        // Attribute-based skip
        if (request.GetType().GetCustomAttributes(typeof(SkipValidationAttribute), inherit: true).Length != 0)
            return true;

        // Trusted/system commands skip
        if (request is ISystemCommand) return true;

        return false;
    }

    private static void EmitMetrics(bool ok, int count, long ms)
    {
        var a = Activity.Current;
        if (a is null) return;
        a.SetTag("axon.validation.outcome", ok ? "success" : "failure");
        a.SetTag("axon.validation.error_count", count);
        a.SetTag("axon.validation.duration_ms", ms);
    }
}
