using System.Runtime.Serialization;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Diagnostics.Extensions;
using BuildingBlocks.Core.Domain.Rules;

namespace BuildingBlocks.Core.Diagnostics.Exceptions;

/// <summary>
/// Exception for business rule violations with support for IBusinessRule pattern
/// </summary>
[Serializable]
public sealed class BusinessRuleException : DomainException
{
    public IBusinessRule? BusinessRule { get; }
    public IReadOnlyList<IBusinessRule>? BusinessRules { get; }
    
    public BusinessRuleException(IBusinessRule rule)
        : base(rule.ToError())
    {
        BusinessRule = rule;
    }
    
    public BusinessRuleException(string message, IBusinessRule rule)
        : base(message, rule.ToError())
    {
        BusinessRule = rule;
    }
    
    public BusinessRuleException(IEnumerable<IBusinessRule> rules)
        : base(rules.Select(r => r.ToError()))
    {
        var ruleList = rules.ToList();
        BusinessRules = ruleList;
        
        // Check all rules are actually broken
        var brokenRules = ruleList.Where(r => r.IsBroken()).ToList();
        if (brokenRules.Count != ruleList.Count)
        {
            throw new ArgumentException("All business rules must be broken", nameof(rules));
        }
    }
    
    /// <summary>
    /// Static helper for checking and throwing
    /// </summary>
    public static void CheckRule(IBusinessRule rule)
    {
        if (rule.IsBroken())
        {
            throw new BusinessRuleException(rule);
        }
    }
    
    public static void CheckRules(params IBusinessRule[] rules)
    {
        var brokenRules = rules.Where(r => r.IsBroken()).ToList();
        if (brokenRules.Count != 0)
        {
            throw new BusinessRuleException(brokenRules);
        }
    }
    
    // Serialization constructor
    private BusinessRuleException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        // Note: IBusinessRule instances may not be serializable, so we don't serialize them
    }
}