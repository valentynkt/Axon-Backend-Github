using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional.Validation;

namespace BuildingBlocks.Core.Domain.Rules;

/// <summary>
/// Base implementation for business rules
/// </summary>
public abstract record BusinessRule : IBusinessRule
{
    public abstract string Code { get; }
    public abstract string Message { get; }
    public abstract bool IsBroken();
    
    /// <summary>
    /// Convert to Result
    /// </summary>
    public Result<Unit> ToResult()
    {
        return IsBroken() 
            ? Result<Unit>.Failure(Error.BusinessRule(Message, Code))
            : Result<Unit>.Success(Unit.Value);
    }
    
    /// <summary>
    /// Convert to Validation
    /// </summary>
    public Validation<Unit> ToValidation()
    {
        return IsBroken()
            ? Validation<Unit>.Invalid(Error.BusinessRule(Message, Code))
            : Validation<Unit>.Valid(Unit.Value);
    }
}

/// <summary>
/// Simple predicate-based business rule
/// </summary>
public sealed record PredicateRule : BusinessRule
{
    private readonly Func<bool> _predicate;
    
    public PredicateRule(string code, string message, Func<bool> predicate)
    {
        Code = code;
        Message = message;
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    }
    
    public override string Code { get; }
    public override string Message { get; }
    
    public override bool IsBroken() => _predicate();
}

/// <summary>
/// Composite rule for combining multiple rules
/// </summary>
public sealed record CompositeRule : BusinessRule
{
    private readonly IBusinessRule[] _rules;
    private readonly string _code;
    private readonly string _message;
    private readonly CompositeRuleMode _mode;
    
    public CompositeRule(
        string code, 
        string message, 
        CompositeRuleMode mode,
        params IBusinessRule[] rules)
    {
        _code = code;
        _message = message;
        _mode = mode;
        _rules = rules ?? Array.Empty<IBusinessRule>();
    }
    
    public override string Code => _code;
    public override string Message => _message;
    
    public override bool IsBroken()
    {
        return _mode switch
        {
            CompositeRuleMode.All => _rules.All(r => r.IsBroken()),
            CompositeRuleMode.Any => _rules.Any(r => r.IsBroken()),
            CompositeRuleMode.None => !_rules.Any(r => r.IsBroken()),
            _ => throw new InvalidOperationException($"Unsupported composite rule mode: {_mode}")
        };
    }
    
    /// <summary>
    /// Get all broken rules
    /// </summary>
    public IEnumerable<IBusinessRule> GetBrokenRules()
    {
        return _rules.Where(r => r.IsBroken());
    }
    
    /// <summary>
    /// Get all rules with their status
    /// </summary>
    public IEnumerable<(IBusinessRule Rule, bool IsBroken)> GetRulesWithStatus()
    {
        return _rules.Select(r => (r, r.IsBroken()));
    }
}

/// <summary>
/// Composite rule evaluation mode
/// </summary>
public enum CompositeRuleMode
{
    /// <summary>
    /// All sub-rules must be broken for composite to be broken
    /// </summary>
    All,
    
    /// <summary>
    /// Any sub-rule broken makes composite broken
    /// </summary>
    Any,
    
    /// <summary>
    /// No sub-rules should be broken (inverse of Any)
    /// </summary>
    None
}