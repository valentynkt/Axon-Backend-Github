using BuildingBlocks.Application.Validation.Core;

namespace BuildingBlocks.Application.Validation.Engine;

/// <summary>
/// Internal helper that composes multiple validators into one, supporting sequential or parallel execution.
/// </summary>
internal enum CompositeMode
{
    Sequential,
    Parallel
}

internal sealed class CompositeValidator<T> : IAppValidator<T>
{
    private readonly IAppValidator<T>[] _validators;
    private readonly CompositeMode _mode;
    private readonly bool _shortCircuitOnError;

    public CompositeValidator(
        IEnumerable<IAppValidator<T>> validators,
        CompositeMode mode = CompositeMode.Sequential,
        bool shortCircuitOnError = false)
    {
        _validators = validators?.ToArray() ?? Array.Empty<IAppValidator<T>>();
        _mode = mode;
        _shortCircuitOnError = shortCircuitOnError;
    }

    public ValidationResult Validate(T instance, IValidationContext? context = null)
    {
        if (_mode == CompositeMode.Parallel) return ValidateAsync(instance, context).GetAwaiter().GetResult();

        var current = ValidationResult.Success;
        foreach (var v in _validators)
        {
            var r = v.Validate(instance!, context);
            current = ValidationResult.Combine(current, r);
            if (_shortCircuitOnError && !r.IsValid) break;
        }
        return current.GroupByField();
    }

    public async Task<ValidationResult> ValidateAsync(T instance, IValidationContext? context = null, CancellationToken ct = default)
    {
        if (_validators.Length == 0) return ValidationResult.Success;

        if (_mode == CompositeMode.Sequential)
        {
            var current = ValidationResult.Success;
            foreach (var v in _validators)
            {
                var r = await v.ValidateAsync(instance!, context, ct);
                current = ValidationResult.Combine(current, r);
                if (_shortCircuitOnError && !r.IsValid) break;
            }
            return current.GroupByField();
        }

        var tasks = _validators.Select(v => v.ValidateAsync(instance!, context, ct));
        var results = await Task.WhenAll(tasks);
        return ValidationResult.Combine(results).GroupByField();
    }
}
