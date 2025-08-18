using System.Text.Json;
using Axon.Modules.Chat.Infrastructure.Ai.Abstractions;

namespace Axon.Modules.Chat.Infrastructure.Ai.Services;

/// <summary>
/// Simple payload serializer implementation
/// </summary>
public sealed class PayloadSerializer : IPayloadSerializer
{
    private readonly JsonSerializerOptions _options;
    
    public PayloadSerializer()
    {
        _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }
    
    public string Serialize(object payload)
    {
        return JsonSerializer.Serialize(payload, _options);
    }
}