using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Core.Domain.Rules;

/// <summary>
/// Convenience base for implementing rules with consistent error shape.
/// Implement <see cref="IsBrokenAsync"/> in derived classes; message/code/metadata are provided via ctor.
/// Prefer <see cref="BusinessRule.Create"/> / <see cref="CreateAsync"/> factories for ad-hoc rules.
/// </summary>
public abstract class BusinessRule : IBusinessRule
{
    protected BusinessRule(string message, string code, IReadOnlyDictionary<string, object>? metadata = null)
    {
        Message = string.IsNullOrWhiteSpace(message) ? "Business rule violated." : message;
        Code    = string.IsNullOrWhiteSpace(code)    ? "BUSINESS_RULE_VIOLATION" : code;
        Metadata = metadata;
    }

    public string Code { get; }
    public string Message { get; }
    public IReadOnlyDictionary<string, object>? Metadata { get; }

    /// <summary>
    /// Default implementation for synchronous rules. Override for sync-only rules for better performance.
    /// </summary>
    public virtual bool IsBroken() => IsBroken() ;

    public abstract ValueTask<bool> IsBrokenAsync(CancellationToken ct = default);

    public Error ToError() => Error.BusinessRule(Message, Code, Metadata ?? new Dictionary<string, object>());

    // ---------- Factories for ad-hoc rules (allocation-minimal) ----------

    public static IBusinessRule Create(string message, string code, Func<bool> isBroken,
        IReadOnlyDictionary<string, object>? metadata = null)
        => new SyncBusinessRule(message, code, isBroken, metadata);

    public static IBusinessRule CreateAsync(string message, string code,
        Func<CancellationToken, ValueTask<bool>> isBrokenAsync,
        IReadOnlyDictionary<string, object>? metadata = null)
        => new AsyncBusinessRule(message, code, isBrokenAsync, metadata);

    private sealed class SyncBusinessRule : BusinessRule
    {
        private readonly Func<bool> _isBroken;
        public SyncBusinessRule(string message, string code, Func<bool> isBroken, IReadOnlyDictionary<string, object>? metadata)
            : base(message, code, metadata) => _isBroken = isBroken ?? throw new ArgumentNullException(nameof(isBroken));
        
        public override bool IsBroken() => _isBroken();
        public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) => ValueTask.FromResult(_isBroken());
    }

    private sealed class AsyncBusinessRule : BusinessRule
    {
        private readonly Func<CancellationToken, ValueTask<bool>> _isBrokenAsync;
        public AsyncBusinessRule(string message, string code, Func<CancellationToken, ValueTask<bool>> isBrokenAsync, IReadOnlyDictionary<string, object>? metadata)
            : base(message, code, metadata) => _isBrokenAsync = isBrokenAsync ?? throw new ArgumentNullException(nameof(isBrokenAsync));
        public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) => _isBrokenAsync(ct);
    }
}
