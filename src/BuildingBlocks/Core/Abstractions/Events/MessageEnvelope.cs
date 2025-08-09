using Google.Protobuf;

namespace BuildingBlocks.Core.Abstractions.Events;

/// <summary>
/// Generic message envelope for wrapping messages with metadata headers.
/// Provides a consistent way to attach context and routing information to messages.
/// </summary>
public class MessageEnvelope
{
    /// <summary>
    /// Initializes a new message envelope with optional headers.
    /// </summary>
    /// <param name="message">The message payload</param>
    /// <param name="headers">Optional metadata headers</param>
    public MessageEnvelope(object? message, IDictionary<string, object?>? headers = null)
    {
        Message = message;
        Headers = headers ?? new();
    }

    /// <summary>
    /// The message payload.
    /// </summary>
    public object? Message { get; init; }
    
    /// <summary>
    /// Message metadata and routing headers.
    /// </summary>
    public IDictionary<string, object?> Headers { get; init; }
}

/// <summary>
/// Strongly-typed message envelope for Protocol Buffers messages.
/// Ensures type safety while maintaining envelope functionality.
/// </summary>
/// <typeparam name="TMessage">The type of Protocol Buffers message</typeparam>
public class MessageEnvelope<TMessage> : MessageEnvelope
    where TMessage : class, IMessage
{
    /// <summary>
    /// Initializes a new strongly-typed message envelope.
    /// </summary>
    /// <param name="message">The typed message payload</param>
    /// <param name="headers">Message metadata headers</param>
    public MessageEnvelope(TMessage message, IDictionary<string, object?> headers) : base(message, headers)
    {
        Message = message;
    }

    /// <summary>
    /// The strongly-typed message payload.
    /// </summary>
    public new TMessage? Message { get; }
}