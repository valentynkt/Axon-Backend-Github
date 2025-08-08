using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional.Validation;

namespace BuildingBlocks.Core.Domain.Rules;

/// <summary>
/// Fluent builder for creating and composing business rules
/// </summary>
public class RuleBuilder
{
    private readonly List<IBusinessRule> _rules = new();
    
    /// <summary>
    /// Add a rule that must be true (condition must be true, or rule is broken)
    /// </summary>
    public RuleBuilder Must(bool condition, string code, string message)
    {
        _rules.Add(new PredicateRule(code, message, () => !condition));
        return this;
    }
    
    /// <summary>
    /// Add a rule with predicate that must be true
    /// </summary>
    public RuleBuilder Must(Func<bool> predicate, string code, string message)
    {
        _rules.Add(new PredicateRule(code, message, () => !predicate()));
        return this;
    }
    
    /// <summary>
    /// Add a rule that must not be true (condition must be false, or rule is broken)
    /// </summary>
    public RuleBuilder MustNot(bool condition, string code, string message)
    {
        _rules.Add(new PredicateRule(code, message, () => condition));
        return this;
    }
    
    /// <summary>
    /// Add a rule with predicate that must not be true
    /// </summary>
    public RuleBuilder MustNot(Func<bool> predicate, string code, string message)
    {
        _rules.Add(new PredicateRule(code, message, predicate));
        return this;
    }
    
    /// <summary>
    /// Add an existing business rule
    /// </summary>
    public RuleBuilder AddRule(IBusinessRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _rules.Add(rule);
        return this;
    }
    
    /// <summary>
    /// Add multiple existing business rules
    /// </summary>
    public RuleBuilder AddRules(params IBusinessRule[] rules)
    {
        if (rules != null)
        {
            _rules.AddRange(rules);
        }
        return this;
    }
    
    /// <summary>
    /// Build and evaluate rules, returning Result
    /// </summary>
    public Result<Unit> Build()
    {
        var brokenRules = _rules.Where(r => r.IsBroken()).ToList();
        
        if (brokenRules.Count != 0)
        {
            var errors = brokenRules.Select(r => 
                Error.BusinessRule(r.Message, r.Code)).ToArray();
            return Result<Unit>.Failure(Error.Aggregate(errors));
        }
        
        return Result<Unit>.Success(Unit.Value);
    }
    
    /// <summary>
    /// Build and evaluate rules, returning Validation (accumulates all errors)
    /// </summary>
    public Validation<Unit> BuildValidation()
    {
        var brokenRules = _rules.Where(r => r.IsBroken()).ToList();
        
        if (brokenRules.Count != 0)
        {
            var errors = brokenRules.Select(r => 
                Error.BusinessRule(r.Message, r.Code)).ToArray();
            return Validation<Unit>.Invalid(errors);
        }
        
        return Validation<Unit>.Valid(Unit.Value);
    }
    
    /// <summary>
    /// Get all rules (for inspection/debugging)
    /// </summary>
    public IReadOnlyList<IBusinessRule> GetRules() => _rules.AsReadOnly();
    
    /// <summary>
    /// Clear all rules
    /// </summary>
    public RuleBuilder Clear()
    {
        _rules.Clear();
        return this;
    }
}

/// <summary>
/// Extension methods for fluent rule composition
/// </summary>
public static class RuleBuilderExtensions
{
    /// <summary>
    /// Create a new rule builder
    /// </summary>
    public static RuleBuilder Rules() => new();
    
    /// <summary>
    /// Create a rule builder with initial rule
    /// </summary>
    public static RuleBuilder Rules(IBusinessRule initialRule)
    {
        return new RuleBuilder().AddRule(initialRule);
    }
    
    /// <summary>
    /// Validate that a value is not null
    /// </summary>
    public static RuleBuilder NotNull<T>(this RuleBuilder builder, T? value, string propertyName)
    {
        return builder.Must(
            value != null, 
            $"{propertyName.ToUpperInvariant()}_NULL", 
            $"{propertyName} cannot be null");
    }
    
    /// <summary>
    /// Validate that a string is not empty
    /// </summary>
    public static RuleBuilder NotEmpty(this RuleBuilder builder, string? value, string propertyName)
    {
        return builder.Must(
            !string.IsNullOrWhiteSpace(value), 
            $"{propertyName.ToUpperInvariant()}_EMPTY", 
            $"{propertyName} cannot be empty");
    }
    
    /// <summary>
    /// Validate that a collection is not empty
    /// </summary>
    public static RuleBuilder NotEmpty<T>(this RuleBuilder builder, IEnumerable<T>? collection, string propertyName)
    {
        return builder.Must(
            collection?.Any() == true, 
            $"{propertyName.ToUpperInvariant()}_EMPTY", 
            $"{propertyName} cannot be empty");
    }
    
    /// <summary>
    /// Validate numeric range
    /// </summary>
    public static RuleBuilder InRange<T>(this RuleBuilder builder, T value, T min, T max, string propertyName)
        where T : IComparable<T>
    {
        return builder.Must(
            value.CompareTo(min) >= 0 && value.CompareTo(max) <= 0,
            $"{propertyName.ToUpperInvariant()}_OUT_OF_RANGE",
            $"{propertyName} must be between {min} and {max}");
    }
    
    /// <summary>
    /// Validate string length
    /// </summary>
    public static RuleBuilder MaxLength(this RuleBuilder builder, string? value, int maxLength, string propertyName)
    {
        return builder.Must(
            value?.Length <= maxLength,
            $"{propertyName.ToUpperInvariant()}_TOO_LONG",
            $"{propertyName} cannot exceed {maxLength} characters");
    }
}