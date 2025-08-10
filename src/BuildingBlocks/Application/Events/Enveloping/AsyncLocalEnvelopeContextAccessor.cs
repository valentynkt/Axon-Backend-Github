namespace BuildingBlocks.Application.Events.Enveloping;

public sealed class AsyncLocalEnvelopeContextAccessor : IEnvelopeContextAccessor
{
    private static readonly AsyncLocal<IntegrationEnvelopeContext?> _current = new();

    public IntegrationEnvelopeContext? Current => _current.Value;

    public IDisposable Push(IntegrationEnvelopeContext context)
    {
        var prior = _current.Value;
        _current.Value = context;
        return new Scope(() => _current.Value = prior);
    }

    private sealed class Scope(Action onDispose) : IDisposable
    {
        private bool _disposed;
        private readonly Action _onDispose = onDispose;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _onDispose();
        }
    }
}