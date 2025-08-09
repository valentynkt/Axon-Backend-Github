using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Core.Domain.Rules;

/// <summary>
/// A single, focused business rule. Pure domain: no infra concerns.
/// Rules are evaluated and, when violated, produce a typed <see cref="Error"/>
/// that can be aggregated or converted to a Result/Validation upstream.
/// </summary>
public interface IBusinessRule
{
    /// <summary>Stable technical code (machine-friendly, e.g., "CUSTOMER_AGE_TOO_LOW").</summary>
    string Code { get; }

    /// <summary>Human-readable explanation (localized in UI layers if needed).</summary>
    string Message { get; }

    /// <summary>Optional metadata (avoid PII). Useful for diagnostics and UX.</summary>
    IReadOnlyDictionary<string, object>? Metadata => null;

    /// <summary>
    /// True when the rule is violated. Use this for synchronous rules (most common case).
    /// </summary>
    bool IsBroken();

    /// <summary>
    /// True when the rule is violated. Use async only if I/O is unavoidable.
    /// Default implementation calls the synchronous version.
    /// </summary>
    ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) => ValueTask.FromResult(IsBroken());

    /// <summary>
    /// Convert this violation to a domain <see cref="Error"/>.
    /// </summary>
    Error ToError() => Error.BusinessRule(Message, Code, Metadata ?? new Dictionary<string, object>());
}