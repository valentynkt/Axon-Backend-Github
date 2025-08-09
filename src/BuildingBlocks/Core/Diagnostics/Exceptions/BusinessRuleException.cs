using System.Runtime.Serialization;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Diagnostics.Extensions;
using BuildingBlocks.Core.Domain.Rules;

namespace BuildingBlocks.Core.Diagnostics.Exceptions;

/// <summary>
/// Exception for business rule violations with support for IBusinessRule pattern.
/// Each instance always represents one or more *broken* rules.
/// </summary>
[Serializable]
public sealed class BusinessRuleException : DomainException
{
    /// <summary>
    /// Single broken rule (if thrown with a single rule).
    /// </summary>
    public IBusinessRule? BusinessRule { get; }

    /// <summary>
    /// All broken rules (never null; contains 1+ items).
    /// </summary>
    public IReadOnlyList<IBusinessRule> BusinessRules { get; }

    #region Constructors

    public BusinessRuleException(IBusinessRule rule)
        : base(ValidateSingle(rule).ToError())
    {
        BusinessRule = rule;
        BusinessRules = new[] { rule };
        EnrichData(BusinessRules);
    }

    public BusinessRuleException(string message, IBusinessRule rule)
        : base(message, ValidateSingle(rule).ToError())
    {
        BusinessRule = rule;
        BusinessRules = new[] { rule };
        EnrichData(BusinessRules);
    }

    public BusinessRuleException(IEnumerable<IBusinessRule> rules)
        : base(ToErrors(ValidateMany(rules)))
    {
        var list = ValidateMany(rules);
        BusinessRules = list;
        BusinessRule = list.Count == 1 ? list[0] : null;
        EnrichData(BusinessRules);
    }

    public BusinessRuleException(string message, IEnumerable<IBusinessRule> rules)
        : base(message, ToErrors(ValidateMany(rules)))
    {
        var list = ValidateMany(rules);
        BusinessRules = list;
        BusinessRule = list.Count == 1 ? list[0] : null;
        EnrichData(BusinessRules);
    }

    // Serialization ctor
    private BusinessRuleException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        // IBusinessRule instances are typically not serializable; skip persistence.
        BusinessRules = Array.Empty<IBusinessRule>();
    }

    #endregion

    #region Guards (static helpers)

    /// <summary>
    /// Throws if the rule is broken.
    /// </summary>
    public static void Check(IBusinessRule rule, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (rule.IsBroken())
            throw message is null ? new BusinessRuleException(rule) : new BusinessRuleException(message, rule);
    }

    /// <summary>
    /// Throws if any rule is broken.
    /// </summary>
    public static void CheckAll(IEnumerable<IBusinessRule> rules, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var broken = rules.Where(r => r is not null && r.IsBroken()).ToList();
        if (broken.Count > 0)
            throw message is null ? new BusinessRuleException(broken) : new BusinessRuleException(message, broken);
    }

    /// <summary>
    /// Throws if any of the provided rules are broken (params convenience).
    /// </summary>
    public static void CheckRules(params IBusinessRule[] rules) => CheckAll(rules);

    /// <summary>
    /// Synonyms for readability in different domains.
    /// </summary>
    public static void Ensure(params IBusinessRule[] rules) => CheckAll(rules);
    public static void Require(params IBusinessRule[] rules) => CheckAll(rules);

    #endregion

    #region Validation helpers

    private static IBusinessRule ValidateSingle(IBusinessRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (!rule.IsBroken())
            throw new ArgumentException("Provided business rule is not broken.", nameof(rule));
        return rule;
    }

    private static List<IBusinessRule> ValidateMany(IEnumerable<IBusinessRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        var list = rules.Where(r => r is not null).ToList();
        if (list.Count == 0)
            throw new ArgumentException("At least one business rule must be provided.", nameof(rules));

        // Enforce that *all* provided rules are broken to keep the exception semantically precise.
        if (list.Any(r => !r.IsBroken()))
            throw new ArgumentException("All provided business rules must be broken.", nameof(rules));

        return list;
    }

    private static IEnumerable<Error> ToErrors(IEnumerable<IBusinessRule> rules) => rules.Select(r => r.ToError());

    #endregion

    #region Data enrichment

    private void EnrichData(IReadOnlyList<IBusinessRule> brokenRules)
    {
        try
        {
            // Compact diagnostics for logs/telemetry
            Data["BrokenRules.Count"] = brokenRules.Count;
            Data["BrokenRules.Types"] = brokenRules
                .Select(r => r.GetType().FullName ?? r.GetType().Name)
                .ToArray();
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch
        {
            // best effort only
        }
#pragma warning restore CA1031
    }

    #endregion
}
