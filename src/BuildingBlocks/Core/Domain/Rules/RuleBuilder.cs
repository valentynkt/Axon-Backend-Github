using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional.Validation;

namespace BuildingBlocks.Core.Domain.Rules;

/// <summary>
/// Allocation-friendly aggregator for domain rules.
/// Turns many rule checks into a single <see cref="Validation{T}"/> (domain layer),
/// or a <see cref="Result{T}"/> when needed.
/// </summary>
public sealed class RuleBuilder
{
    private readonly List<Error> _errors = new();

    private RuleBuilder() { }
    public static RuleBuilder Start() => new();

    /// <summary>Evaluate a rule synchronously and collect its error if broken.</summary>
    public RuleBuilder Check(IBusinessRule rule)
    {
        if (rule is null) return this;
        if (rule.IsBroken())
            _errors.Add(rule.ToError());
        return this;
    }

    /// <summary>Evaluate a rule and collect its error if broken.</summary>
    public async Task<RuleBuilder> CheckAsync(IBusinessRule rule, CancellationToken ct = default)
    {
        if (rule is null) return this;
        if (await rule.IsBrokenAsync(ct).ConfigureAwait(false))
            _errors.Add(rule.ToError());
        return this;
    }

    /// <summary>Evaluate many rules synchronously, collecting all violations (no short-circuit).</summary>
    public RuleBuilder CheckAll(IEnumerable<IBusinessRule> rules)
    {
        if (rules is null) return this;
        foreach (var r in rules)
            Check(r);
        return this;
    }

    /// <summary>Evaluate many rules, collecting all violations (no short-circuit).</summary>
    public async Task<RuleBuilder> CheckAllAsync(IEnumerable<IBusinessRule> rules, CancellationToken ct = default)
    {
        if (rules is null) return this;
        foreach (var r in rules)
            await CheckAsync(r, ct).ConfigureAwait(false);
        return this;
    }

    /// <summary>Synchronous predicate convenience.</summary>
    public RuleBuilder Check(bool isBroken, string message, string code = "BUSINESS_RULE_VIOLATION",
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        if (isBroken)
            _errors.Add(Error.BusinessRule(message, code, metadata ?? new Dictionary<string, object>()));
        return this;
    }

    /// <summary>Async predicate convenience.</summary>
    public async Task<RuleBuilder> CheckAsync(Func<CancellationToken, Task<bool>> isBrokenAsync, string message,
        string code = "BUSINESS_RULE_VIOLATION",
        IReadOnlyDictionary<string, object>? metadata = null,
        CancellationToken ct = default)
    {
        if (isBrokenAsync is null) return this;
        if (await isBrokenAsync(ct).ConfigureAwait(false))
            _errors.Add(Error.BusinessRule(message, code, metadata ?? new Dictionary<string, object>()));
        return this;
    }

    /// <summary>Merges another builder's errors into this one.</summary>
    public RuleBuilder Merge(RuleBuilder other)
    {
        if (other is null) return this;
        if (other._errors.Count != 0) _errors.AddRange(other._errors);
        return this;
    }

    /// <summary>Build a Validation result (Valid when no errors, otherwise Invalid with all errors).</summary>
    public ValidationResult<Unit> Build()
        => _errors.Count == 0 ? ValidationResult<Unit>.CreateValid(Unit.Value) : ValidationResult<Unit>.CreateInvalid(_errors);

    /// <summary>Build a Result (returns first error or success).</summary>
    public Result<Unit> BuildResult()
        => _errors.Count == 0 ? Result<Unit>.Success(Unit.Value) : Result<Unit>.Failure(_errors[0]);

    /// <summary>Throwing variant for the rare case you must enforce invariants via exceptions.</summary>
    public void Ensure()
    {
        if (_errors.Count != 0)
            throw new DomainRuleViolationException(_errors.ToArray());
    }
}

/// <summary>Optional exception for invariants that must never pass a boundary.</summary>
public sealed class DomainRuleViolationException : Exception
{
    public Error[] Errors { get; }

    public DomainRuleViolationException(Error[] errors)
        : base(errors.Length == 1 ? errors[0].Message : "Multiple business rule violations.")
        => Errors = errors;
}

/// <summary>Fluent helpers for common checks.</summary>
public static class RuleBuilderExtensions
{
    public static RuleBuilder MustNotBeEmpty(this RuleBuilder b, string? value, string field)
        => b.Check(string.IsNullOrWhiteSpace(value), $"{field} cannot be empty", $"{field.ToUpperInvariant()}_EMPTY");

    public static RuleBuilder MustNotBeNull<T>(this RuleBuilder b, T? value, string field) where T : class
        => b.Check(value is null, $"{field} cannot be null", $"{field.ToUpperInvariant()}_NULL");

    public static RuleBuilder MustHaveItems<T>(this RuleBuilder b, IEnumerable<T>? collection, string field)
        => b.Check(collection?.Any() != true, $"{field} must contain at least one item", $"{field.ToUpperInvariant()}_EMPTY");

    public static RuleBuilder MustBeInRange<T>(this RuleBuilder b, T value, T min, T max, string field) where T : IComparable<T>
        => b.Check(value.CompareTo(min) < 0 || value.CompareTo(max) > 0,
                   $"{field} must be between {min} and {max}",
                   $"{field.ToUpperInvariant()}_OUT_OF_RANGE",
                   new Dictionary<string, object> { ["min"] = min!, ["max"] = max!, ["actual"] = value! });

    public static RuleBuilder MustNotExceedLength(this RuleBuilder b, string? value, int max, string field)
        => b.Check(value?.Length > max, $"{field} must not exceed {max} characters",
                   $"{field.ToUpperInvariant()}_TOO_LONG",
                   new Dictionary<string, object> { ["maxLength"] = max, ["actualLength"] = value?.Length ?? 0 });

    public static RuleBuilder MustHaveMinimumLength(this RuleBuilder b, string? value, int min, string field)
        => b.Check(value?.Length < min, $"{field} must be at least {min} characters",
                   $"{field.ToUpperInvariant()}_TOO_SHORT",
                   new Dictionary<string, object> { ["minLength"] = min, ["actualLength"] = value?.Length ?? 0 });

    public static RuleBuilder Must<T>(this RuleBuilder b, T value, Func<T, bool> predicate, string message, string code)
        => b.Check(!predicate(value), message, code);

    public static Task<RuleBuilder> MustAsync<T>(this RuleBuilder b, T value, Func<T, CancellationToken, Task<bool>> predicate,
        string message, string code, CancellationToken ct = default)
        => b.CheckAsync(async token => !await predicate(value, token).ConfigureAwait(false), message, code, null, ct);
}
