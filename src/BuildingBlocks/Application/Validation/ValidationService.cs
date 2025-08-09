using BuildingBlocks.Application.Abstractions.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace BuildingBlocks.Application.Validation;

public interface IValidationService
{
    Task<ValidationResult> ValidateAsync<T>(T instance, IValidationContext? ctx = null, CancellationToken ct = default);
    ValidationResult Validate<T>(T instance, IValidationContext? ctx = null);
}

public sealed class ValidationService : IValidationService
{
    private readonly IServiceProvider _sp;
    private readonly ValidationOptions _options;
    private readonly ValidationMetrics? _metrics;

    public ValidationService(
        IServiceProvider sp, 
        IOptions<ValidationOptions> options,
        ValidationMetrics? metrics = null)
    {
        _sp = sp;
        _options = options.Value;
        _metrics = metrics;
    }

    public ValidationResult Validate<T>(T instance, IValidationContext? ctx = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var validator = Resolve<T>(out var validatorCount);
        var result = validator.Validate(instance!, ctx);
        stopwatch.Stop();

        _metrics?.RecordValidation(
            typeof(T).Name,
            result.IsValid,
            validatorCount,
            result.Errors.Count,
            stopwatch.ElapsedMilliseconds,
            result);

        return result;
    }

    public async Task<ValidationResult> ValidateAsync<T>(T instance, IValidationContext? ctx = null, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var validator = Resolve<T>(out var validatorCount);
        var result = await validator.ValidateAsync(instance!, ctx, ct);
        stopwatch.Stop();

        _metrics?.RecordValidation(
            typeof(T).Name,
            result.IsValid,
            validatorCount,
            result.Errors.Count,
            stopwatch.ElapsedMilliseconds,
            result);

        return result;
    }

    private IAppValidator<T> Resolve<T>(out int validatorCount)
    {
        var fluent = _sp.GetServices<FluentValidation.IValidator<T>>();
        var adapters = new List<IAppValidator<T>>();
        if (fluent.Any()) adapters.Add(new FluentValidationAdapter<T>(fluent));

        adapters.AddRange(_sp.GetServices<IAppValidator<T>>());

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