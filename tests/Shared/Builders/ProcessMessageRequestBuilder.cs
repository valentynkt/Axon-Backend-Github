using System.Text.Json;

namespace Axon.Tests.Shared.Builders;

/// <summary>
/// Builder for creating ProcessMessage HTTP request data for API testing.
/// Focuses on FastEndpoints scenarios with 10K character limits.
/// </summary>
public class ProcessMessageRequestBuilder
{
    private string _message = "Default test message";
    private string? _conversationId;
    private Dictionary<string, object> _additionalProperties = new();

    public static ProcessMessageRequestBuilder Create() => new();

    /// <summary>
    /// Sets the message content.
    /// </summary>
    public ProcessMessageRequestBuilder WithMessage(string message)
    {
        _message = message;
        return this;
    }

    /// <summary>
    /// Sets the conversation ID.
    /// </summary>
    public ProcessMessageRequestBuilder WithConversationId(string conversationId)
    {
        _conversationId = conversationId;
        return this;
    }

    /// <summary>
    /// Creates a message with specific length for boundary testing.
    /// </summary>
    public ProcessMessageRequestBuilder WithMessageOfLength(int length, char character = 'a')
    {
        _message = new string(character, length);
        return this;
    }

    /// <summary>
    /// Creates a message that exceeds API limits (>10K characters).
    /// </summary>
    public ProcessMessageRequestBuilder WithOversizedMessage()
    {
        return WithMessageOfLength(10001, 'x');
    }

    /// <summary>
    /// Creates a message at API boundary (exactly 10K characters).
    /// </summary>
    public ProcessMessageRequestBuilder WithMaxLengthMessage()
    {
        return WithMessageOfLength(10000, 'a');
    }

    /// <summary>
    /// Creates an empty message for validation testing.
    /// </summary>
    public ProcessMessageRequestBuilder WithEmptyMessage()
    {
        _message = string.Empty;
        return this;
    }

    /// <summary>
    /// Creates a null message for validation testing.
    /// </summary>
    public ProcessMessageRequestBuilder WithNullMessage()
    {
        _message = null!;
        return this;
    }

    /// <summary>
    /// Adds additional property for extensibility testing.
    /// </summary>
    public ProcessMessageRequestBuilder WithProperty(string key, object value)
    {
        _additionalProperties[key] = value;
        return this;
    }

    /// <summary>
    /// Builds the request as a JSON object for HTTP API testing.
    /// </summary>
    public object BuildForApi()
    {
        var request = new Dictionary<string, object>
        {
            ["message"] = _message
        };

        if (_conversationId != null)
        {
            request["conversationId"] = _conversationId;
        }

        foreach (var prop in _additionalProperties)
        {
            request[prop.Key] = prop.Value;
        }

        return request;
    }

    /// <summary>
    /// Builds the request as JSON string for HTTP testing.
    /// </summary>
    public string BuildAsJson()
    {
        return JsonSerializer.Serialize(BuildForApi(), new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    /// <summary>
    /// Builds multiple requests for batch testing scenarios.
    /// </summary>
    public static List<object> BuildBatch(int count, Func<int, ProcessMessageRequestBuilder>? configurator = null)
    {
        var requests = new List<object>();
        
        for (int i = 0; i < count; i++)
        {
            var builder = Create().WithMessage($"Test message {i + 1}");
            
            if (configurator != null)
            {
                builder = configurator(i);
            }
            
            requests.Add(builder.BuildForApi());
        }
        
        return requests;
    }

    /// <summary>
    /// Common test scenarios for API validation testing.
    /// </summary>
    public static class CommonScenarios
    {
        public static ProcessMessageRequestBuilder ValidMessage() 
            => Create().WithMessage("This is a valid test message");

        public static ProcessMessageRequestBuilder EmptyMessage() 
            => Create().WithEmptyMessage();

        public static ProcessMessageRequestBuilder NullMessage() 
            => Create().WithNullMessage();

        public static ProcessMessageRequestBuilder OversizedMessage() 
            => Create().WithOversizedMessage();

        public static ProcessMessageRequestBuilder BoundaryMessage() 
            => Create().WithMaxLengthMessage();

        public static ProcessMessageRequestBuilder WithConversation() 
            => Create()
                .WithMessage("Test message with conversation")
                .WithConversationId(Guid.NewGuid().ToString());
    }
}