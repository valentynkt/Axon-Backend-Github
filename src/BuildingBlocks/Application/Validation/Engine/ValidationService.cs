using BuildingBlocks.Application.Validation;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace BuildingBlocks.Application.Validation;

/// <summary>
/// The single entry point for executing validation over a request/command.
/// Orchestrates all registered validators (FluentValidation + custom app validators)
/// and returns a unified <see cref="ValidationResult"/>.
/// </summary>
public sealed class ValidationService : IValidationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ValidationMetrics? _metrics;

    public ValidationService(IServiceProvider serviceProvider, ValidationMetrics? metrics = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _metrics = metrics; // optional
    }

    public ValidationResult Validate<T>(T instance, IValidationContext? ctx = null)
    {
        var sw = Stopwatch.StartNew();
        var validator = Resolve<T>(out var count);
        var result = validator.Validate(instance!, ctx);
        sw.Stop();

        _metrics?.RecordValidation(
            requestType: typeof(T).Name,
            isValid: result.IsValid,
            validatorCount: count,
            errorCount: result.Errors.Count,
            durationMs: sw.ElapsedMilliseconds,
            result: result);

        return result;
    }

    public async Task<ValidationResult> ValidateAsync<T>(T instance, IValidationContext? ctx = null, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var validator = Resolve<T>(out var count);
        var result = await validator.ValidateAsync(instance!, ctx, ct);
        sw.Stop();

        _metrics?.RecordValidation(
            requestType: typeof(T).Name,
            isValid: result.IsValid,
            validatorCount: count,
            errorCount: result.Errors.Count,
            durationMs: sw.ElapsedMilliseconds,
            result: result);

        return result;
    }

    private IAppValidator<T> Resolve<T>(out int validatorCount)
    {
        // FluentValidation validators -> one adapter
        var fluent = _serviceProvider.GetServices<FluentValidation.IValidator<T>>().ToArray();
        var adapters = new List<IAppValidator<T>>();
        if (fluent.Length > 0)
            adapters.Add(new FluentValidationAdapter<T>(fluent));

        // Custom app validators
        adapters.AddRange(_serviceProvider.GetServices<IAppValidator<T>>());

        validatorCount = adapters.Count;

        return adapters.Count switch
        {
            0 => new NoopValidator<T>(),
            1 => adapters[0],
            _ => new CompositeValidator<T>(adapters, CompositeMode.Sequential, shortCircuitOnError: false)
        };
    }

    private sealed class NoopValidator<T> : IAppValidator<T>
    {
        public ValidationResult Validate(T instance, IValidationContext? context = null) => ValidationResult.Success;
        public Task<ValidationResult> ValidateAsync(T instance, IValidationContext? context = null, CancellationToken ct = default)
            => Task.FromResult(ValidationResult.Success);
    }
}
