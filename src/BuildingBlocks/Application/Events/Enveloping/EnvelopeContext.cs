using System.Threading;

namespace BuildingBlocks.Application.Events.Enveloping;

/// <summary>
/// Correlation/context that can flow from command handling → outbox → publisher.
/// </summary>
public sealed record IntegrationEnvelopeContext(
    string? TraceId = null,
    Guid? RequestId = null,
    string? TenantId = null,
    IReadOnlyDictionary<string, object>? Metadata = null,
    Guid OutboxEntryId = default,
    Guid TransactionId = default);

/// <summary>
/// Accesses the current envelope context and allows scoped overrides.
/// </summary>
public interface IEnvelopeContextAccessor
{
    IntegrationEnvelopeContext? Current { get; }

    /// <summary>
    /// Push a new context for the current async flow. Returns a scope that restores the prior context on dispose.
    /// </summary>
    IDisposable Push(IntegrationEnvelopeContext context);
}

/// <summary>
/// AsyncLocal-based implementation. Supports nesting; dispose restores prior context.
/// </summary>
public sealed class AsyncLocalEnvelopeContextAccessor : IEnvelopeContextAccessor
{
    private readonly AsyncLocal<IntegrationEnvelopeContext?> _current = new();

    public IntegrationEnvelopeContext? Current => _current.Value;

    public IDisposable Push(IntegrationEnvelopeContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        var prior = _current.Value;
        _current.Value = context;
        return new Scope(this, prior);
    }

    private sealed class Scope : IDisposable
    {
        private readonly AsyncLocalEnvelopeContextAccessor _owner;
        private readonly IntegrationEnvelopeContext? _prior;
        private bool _disposed;

        public Scope(AsyncLocalEnvelopeContextAccessor owner, IntegrationEnvelopeContext? prior)
        {
            _owner = owner; _prior = prior;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _owner._current.Value = _prior;
            _disposed = true;
        }
    }
}
