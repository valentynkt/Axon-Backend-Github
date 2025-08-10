# Application Eventing Guide (Outbound + Inbound)

> Transport-neutral, Application-pure eventing for a Modular Monolith (DDD + CQRS).  
> This guide documents the **two-lane model**, envelopes, headers, outbox/inbox,
> DI, configuration, testing, and operational guidance.

---

## 1) Mental Model: Two Lanes

**Lane A — In-Process Domain Notifications**
- Purpose: local policies (same module/bounded context) after commit
- Flow: `IDomainEvent` → `DomainEventNotification<T>` → MediatR handlers
- Never crosses boundaries, never serialized

**Lane B — Cross-Boundary Integration**
- Purpose: publish/consume between modules/bounded contexts
- Outbound Flow: `IDomainEvent` → Outbox → `IEventMapper` / `IHaveIntegrationEvent` → `IntegrationEventEnvelope` → `IIntegrationEventPublisher` (transport adapter)
- Inbound Flow: `IntegrationEventEnvelope` → Idempotent Inbox → Handlers → Retry/Dead-letter policy

Both lanes are **Application-pure** and **transport-neutral**.

---

## 2) Core Types (Cheat-Sheet)

### Domain (Core)
- `IDomainEvent` – domain event contract
- `IHaveIntegrationEvent` – opt-in shortcut for mapping (domain → integration)

### Application / Events / Enveloping
- `IntegrationEventEnvelope` – `{ Id, Type, SchemaVersion, ProducedAt, Data, Headers }`
- `IntegrationEventHeaders` – canonical header keys (kebab-case)
- `IntegrationEventNameAttribute` – stable name for event types
- `IEventTypeNameResolver` / `DefaultEventTypeNameResolver`
- `IEventSerializer` / `SystemTextJsonEventSerializer`
- `IEnvelopeContextAccessor` / `AsyncLocalEnvelopeContextAccessor`
- `IEnvelopeHeaderPolicy` / `DefaultEnvelopeHeaderPolicy` (correlation, causation, idempotency)

### Application / Events / Dispatching (Outbound)
- `IIntegrationEventPublisher` (port) + `NoOpIntegrationEventPublisher` (dev)
- `IntegrationEventDispatcher` – orchestrates mapping + enveloping + publishing

### Application / Events / Notifications (Lane A)
- `DomainEventNotification<T>` – MediatR wrapper for in-process notifications

### Application / Events / Consumption (Inbound)
- `IInboundIntegrationEventDispatcher` – single entry for transports
- `IIntegrationEventHandler<T>` – typed inbound handler interface
- `IIntegrationEventHandlerRegistry` – DI-backed handler lookup
- `IInboxStore` – idempotent “inbox” store (NoOp impl for dev)
- Retry & Dead-letter:
    - `IInboundErrorClassifier` / `DefaultInboundErrorClassifier`
    - `IInboxRetryPolicy` / `DefaultInboxRetryPolicy`
    - `IInboxDeadLetterStore` / `NoOpInboxDeadLetterStore`

### Application / Outbox (Outbound storage/processing)
- `IOutboxService`, `IOutboxProcessor`, `IOutboxRepository` (port), `OutboxOptions`
- Backoff & Metrics (Story 8): `IOutboxBackoffPolicy`, `IOutboxMetrics`, etc.

---

## 3) Headers (Lane B)

Canonical keys (kebab-case). Policy merges, dedupes, and removes nulls.

- `integration-event-id` — GUID for the integration event (envelope.Id)
- `integration-event-type` — stable name (via `IntegrationEventNameAttribute` or resolver)
- `integration-schema-version` — default `1`
- `produced-at` — UTC timestamp
- `idempotency-key` — **stable key** (pref: OutboxEntryId → DomainEventId → payload hash)
- Correlation/Causation:
    - `trace-id`, `span-id`, `request-id`, `causation-id`
- Tenancy/Context:
    - `tenant-id`, `transaction-id`, `outbox-entry-id`
- (Back-compat legacy keys are marked `[Obsolete]` in code)

**Guarantee:** Same outbox entry → same `idempotency-key`.

---

## 4) Outbound: From Domain Event to Envelope

### Typical Aggregate
```csharp
// Domain model
order.RaiseDomainEvent(new OrderCreatedDomainEvent(order.Id, order.Total));

// Optional shortcut (domain exposes integration event directly)
public sealed record PaymentSucceeded(...) : IDomainEvent, IHaveIntegrationEvent
{
    public IEnumerable<IIntegrationEvent> GetIntegrationEvents()
        => new[] { new PaymentSucceededIntegrationEvent(/*...*/ ) };
}
