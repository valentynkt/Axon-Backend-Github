using BuildingBlocks.Core.Abstractions.Events;
using BuildingBlocks.Core.Domain.Events;
using BuildingBlocks.Application.Events.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace BuildingBlocks.Application.Events.Enveloping;

/// <summary>
/// Default implementation of integration event envelope factory.
/// Creates envelopes with standard headers and metadata using header policy.
/// </summary>
public sealed class DefaultIntegrationEventEnvelopeFactory : IIntegrationEventEnvelopeFactory
{
    private readonly IEventTypeNameResolver _typeResolver;
    private readonly IEnvelopeHeaderPolicy _headerPolicy;
    private readonly IEventSerializer _serializer;

    public DefaultIntegrationEventEnvelopeFactory(
        IEventTypeNameResolver typeResolver,
        IEnvelopeHeaderPolicy headerPolicy,
        IEventSerializer serializer)
    {
        _typeResolver = typeResolver ?? throw new ArgumentNullException(nameof(typeResolver));
        _headerPolicy = headerPolicy ?? throw new ArgumentNullException(nameof(headerPolicy));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    public IReadOnlyList<IntegrationEventEnvelope> Create(
        IDomainEvent domainEvent,
        IEnumerable<IIntegrationEvent> integrationEvents,
        IntegrationEnvelopeContext? ctx)
    {
        var producedAt = DateTime.UtcNow;
        var list = new List<IntegrationEventEnvelope>();

        foreach (var ie in integrationEvents)
        {
            var envelopeId = Guid.NewGuid();
            var eventTypeName = _typeResolver.Resolve(ie.GetType());
            var schemaVersion = (ie as IVersionedEvent)?.Version ?? 1;

            // Create base headers that factory owns
            var baseHeaders = new Dictionary<string, object>
            {
                [IntegrationEventHeaders.IntegrationEventId] = envelopeId,
                [IntegrationEventHeaders.ProducedAt] = producedAt.ToString("O") // ISO-8601
            };

            // Add context metadata if present
            if (ctx?.Metadata is { Count: > 0 })
            {
                foreach (var kv in ctx.Metadata)
                {
                    baseHeaders[kv.Key] = kv.Value;
                }
            }

            // Serialize event data for payload hash computation
            var jsonPayload = _serializer.Serialize(ie);
            
            // Apply header policy to get final headers
            var finalHeaders = _headerPolicy.Apply(
                domainEvent,
                eventTypeName,
                schemaVersion.ToString(),
                ctx,
                baseHeaders,
                payloadHashProvider: () => ComputePayloadHash(jsonPayload)
            );

            list.Add(new IntegrationEventEnvelope(
                EnvelopeId: envelopeId,
                OccurredAtUtc: producedAt,
                Type: eventTypeName,
                SchemaVersion: schemaVersion,
                Data: ie,
                Headers: finalHeaders
            ));
        }

        return list;
    }

    private static string ComputePayloadHash(string jsonPayload)
    {
        try
        {
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(jsonPayload));
            return Convert.ToHexString(hashBytes);
        }
        catch
        {
            // Fallback to a deterministic string if hashing fails
            return $"hash-failed-{jsonPayload.Length}";
        }
    }
}

/// <summary>
/// Optional contracts used by factory when present on events.
/// </summary>
public interface IEventIdentity 
{ 
    Guid EventId { get; } 
}

/// <summary>
/// Optional version contract for events that have schema versions.
/// </summary>
public interface IVersionedEvent 
{ 
    int Version { get; } 
}