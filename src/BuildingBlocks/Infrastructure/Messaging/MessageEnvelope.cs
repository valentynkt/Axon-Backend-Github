using System.Collections.ObjectModel;

namespace BuildingBlocks.Infrastructure.Messaging;

/// <summary>
/// Message envelope for wrapping messages with metadata
/// </summary>
public class MessageEnvelope
{
    /// <summary>
    /// Constructor for creating envelope with message
    /// </summary>
    /// <param name="message">The message content</param>
    /// <param name="headers">Optional headers</param>
    public MessageEnvelope(object message, IDictionary<string, object>? headers = null)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Headers = headers?.AsReadOnly() ?? new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());
    }

    /// <summary>
    /// The wrapped message
    /// </summary>
    public object Message { get; }

    /// <summary>
    /// Message headers/metadata
    /// </summary>
    public IReadOnlyDictionary<string, object> Headers { get; }
}