using System.Security.Cryptography;
using System.Text;
using BuildingBlocks.Core.Domain.Events;

namespace BuildingBlocks.Application.Events.Enveloping;

/// <summary>
/// Default implementation of envelope header policy.
/// Applies standard headers for correlation, causation, idempotency, and metadata.
/// </summary>
public sealed class DefaultEnvelopeHeaderPolicy : IEnvelopeHeaderPolicy
{
    public IReadOnlyDictionary<string, object> Apply(
        IDomainEvent? domainEvent,
        string eventTypeName,
        string schemaVersion,
        IntegrationEnvelopeContext? context,
        IReadOnlyDictionary<string, object>? existingHeaders,
        Func<string> payloadHashProvider)
    {
        var headers = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        
        // Start with existing headers (preserving non-null values)
        if (existingHeaders is not null)
        {
            foreach (var kv in existingHeaders)
            {
                if (kv.Value is not null && !IsEmptyValue(kv.Value))
                {
                    headers[kv.Key] = kv.Value;
                }
            }
        }

        // Apply required integration metadata
        headers[IntegrationEventHeaders.IntegrationEventType] = eventTypeName;
        headers[IntegrationEventHeaders.IntegrationSchemaVersion] = schemaVersion;

        // Apply correlation tracking (prioritize TraceId, fallback to RequestId)
        if (!string.IsNullOrWhiteSpace(context?.TraceId))
        {
            headers[IntegrationEventHeaders.CorrelationId] = context!.TraceId!;
        }
        else if (context?.RequestId is string requestId && !string.IsNullOrWhiteSpace(requestId))
        {
            headers[IntegrationEventHeaders.CorrelationId] = requestId;
        }

        // Apply causation tracking (domain event that caused this integration event)
        if (domainEvent is not null)
        {
            headers[IntegrationEventHeaders.CausationId] = domainEvent.EventId.ToString("D");
        }

        // Apply tenant context
        if (!string.IsNullOrWhiteSpace(context?.TenantId))
        {
            headers[IntegrationEventHeaders.TenantId] = context!.TenantId!;
        }

        // Apply outbox tracking
        if (context?.OutboxEntryId is Guid outboxId && outboxId != Guid.Empty)
        {
            headers[IntegrationEventHeaders.OutboxEntryId] = outboxId.ToString("D");
        }

        // Apply transaction tracking  
        if (context?.TransactionId is Guid transactionId && transactionId != Guid.Empty)
        {
            headers[IntegrationEventHeaders.TransactionId] = transactionId.ToString("D");
        }

        // Compute stable idempotency key for retry safety
        var idempotencyKey = ComputeIdempotencyKey(domainEvent, context, eventTypeName, payloadHashProvider);
        headers[IntegrationEventHeaders.IdempotencyKey] = idempotencyKey;

        return headers;
    }

    private static string ComputeIdempotencyKey(
        IDomainEvent? domainEvent, 
        IntegrationEnvelopeContext? context,
        string eventTypeName,
        Func<string> payloadHashProvider)
    {
        // Strategy 1: Use OutboxEntryId if available (stable across retries)
        if (context?.OutboxEntryId is Guid outboxId && outboxId != Guid.Empty)
        {
            return outboxId.ToString("D");
        }

        // Strategy 2: Use DomainEvent.EventId if available
        if (domainEvent is not null)
        {
            return domainEvent.EventId.ToString("D");
        }

        // Strategy 3: Fallback to deterministic hash of event type + payload
        return ComputeFallbackIdempotencyKey(eventTypeName, payloadHashProvider);
    }

    private static string ComputeFallbackIdempotencyKey(string eventTypeName, Func<string> payloadHashProvider)
    {
        try
        {
            // Get payload hash (SHA256 of serialized JSON)
            var payloadHash = payloadHashProvider();
            
            // Create deterministic key from type + payload
            var keyInput = $"{eventTypeName}|{payloadHash}";
            
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(keyInput));
            
            // Use first 16 bytes (128-bit) as hex string for reasonable length
            return Convert.ToHexString(hashBytes[..16]);
        }
        catch
        {
            // Last resort: use type name + timestamp (not ideal but prevents failure)
            return $"{eventTypeName}-{DateTimeOffset.UtcNow.Ticks:X}";
        }
    }

    private static bool IsEmptyValue(object value)
    {
        return value switch
        {
            string s => string.IsNullOrWhiteSpace(s),
            Guid g => g == Guid.Empty,
            _ => false
        };
    }
}