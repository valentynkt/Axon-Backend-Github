// /Users/valentynkit/Repos/Axon-Backend/src/BuildingBlocks/Core/Domain/Rules/DomainRules.cs
#nullable enable
using System.Collections.Generic;
using BuildingBlocks.Core.Diagnostics.Exceptions;

namespace BuildingBlocks.Core.Domain.Rules;

/// <summary>
/// Helpers to enforce rules uniformly from aggregates/entities.
/// </summary>
public static class DomainRules
{
    /// <summary>Throw BusinessRuleException if rule is broken.</summary>
    public static void Check(IBusinessRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (rule.IsBroken()) throw new BusinessRuleException(rule);
    }

    /// <summary>Enforce many rules in order; throws on the first violation.</summary>
    public static void Require(params IBusinessRule[] rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        foreach (var r in rules) Check(r);
    }

    /// <summary>Enforce many rules in order; throws on the first violation.</summary>
    public static void Require(IEnumerable<IBusinessRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        foreach (var r in rules) Check(r);
    }

    /// <summary>Throw BusinessRuleException if rule is broken (async version).</summary>
    public static async ValueTask CheckAsync(IBusinessRule rule, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (await rule.IsBrokenAsync(cancellationToken)) throw new BusinessRuleException(rule);
    }

    /// <summary>Enforce many rules in order; throws on the first violation (async version).</summary>
    public static async ValueTask RequireAsync(CancellationToken cancellationToken = default, params IBusinessRule[] rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        foreach (var r in rules) await CheckAsync(r, cancellationToken);
    }

    /// <summary>Enforce many rules in order; throws on the first violation (async version).</summary>
    public static async ValueTask RequireAsync(IEnumerable<IBusinessRule> rules, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rules);
        foreach (var r in rules) await CheckAsync(r, cancellationToken);
    }
}