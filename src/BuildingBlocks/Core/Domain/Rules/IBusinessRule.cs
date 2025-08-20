// /Users/valentynkit/Repos/Axon-Backend/src/BuildingBlocks/Core/Domain/Rules/IBusinessRule.cs
#nullable enable
using System.Collections.Generic;

namespace BuildingBlocks.Core.Domain.Rules;

/// <summary>
/// Small DDD contract for domain rules. Keep synchronous and infra-agnostic.
/// </summary>
public interface IBusinessRule
{
    /// <summary>Stable machine-readable code (defaults to rule type name in base class).</summary>
    string Code { get; }

    /// <summary>Human-readable reason when the rule is broken.</summary>
    string Message { get; }

    /// <summary>True when the rule is violated.</summary>
    bool IsBroken();

    /// <summary>Async version of IsBroken. Default implementation calls IsBroken().</summary>
    ValueTask<bool> IsBrokenAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(IsBroken());

    /// <summary>Optional structured context (no PII).</summary>
    IReadOnlyDictionary<string, object>? Metadata => null;
}