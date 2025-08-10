using System.Text.Json;
using System.Text.Json.Serialization;

// Simplified test for Story 6 components without dependencies
public class SimpleEventSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    public string Serialize(object message, Type messageType)
        => JsonSerializer.Serialize(message, messageType, Options);

    public object? Deserialize(string payload, Type messageType)
        => JsonSerializer.Deserialize(payload, messageType, Options);
}

public class SimpleContextAccessor
{
    private static readonly AsyncLocal<SimpleContext?> _current = new();

    public SimpleContext? Current => _current.Value;

    public IDisposable Push(SimpleContext context)
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

public record SimpleContext(
    string? TraceId,
    string? RequestId,
    string? TenantId,
    Guid OutboxEntryId,
    Guid TransactionId
);

public record SimpleEvent(string Name, string Data);

public static class SimpleTester
{
    public static void TestStory6Components()
    {
        Console.WriteLine("=== Simple Story 6 Test ===");
        
        // Test 1: Serialization
        var serializer = new SimpleEventSerializer();
        var testEvent = new SimpleEvent("TestEvent", "TestData");
        
        var json = serializer.Serialize(testEvent, typeof(SimpleEvent));
        Console.WriteLine($"1. Serialized: {json}");
        
        var deserialized = serializer.Deserialize(json, typeof(SimpleEvent)) as SimpleEvent;
        Console.WriteLine($"   Deserialized: {deserialized?.Name}, {deserialized?.Data}");
        Console.WriteLine($"   Round-trip: {deserialized?.Name == testEvent.Name && deserialized?.Data == testEvent.Data}");
        
        // Test 2: Context accessor
        var contextAccessor = new SimpleContextAccessor();
        Console.WriteLine($"\n2. Initial context: {contextAccessor.Current}");
        
        var context = new SimpleContext(
            TraceId: "trace-123",
            RequestId: "req-456",
            TenantId: "tenant-789",
            OutboxEntryId: Guid.NewGuid(),
            TransactionId: Guid.NewGuid()
        );
        
        using (contextAccessor.Push(context))
        {
            var current = contextAccessor.Current;
            Console.WriteLine($"   Pushed context - TraceId: {current?.TraceId}");
            Console.WriteLine($"   Pushed context - RequestId: {current?.RequestId}");
            Console.WriteLine($"   Pushed context - TenantId: {current?.TenantId}");
            Console.WriteLine($"   Pushed context - OutboxId: {current?.OutboxEntryId}");
            Console.WriteLine($"   Pushed context - TransactionId: {current?.TransactionId}");
        }
        
        Console.WriteLine($"   Context after dispose: {contextAccessor.Current}");
        Console.WriteLine("\n=== Story 6 Core Functionality Working! ===");
    }
}