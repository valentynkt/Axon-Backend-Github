Love it — let’s turn the integration adapters doc into the “single source of truth” your Infra teams can ship from. Below is a **fully upgraded**, **brownfield-aware**, and **story-grounded** guide that matches everything we’ve designed/implemented across Stories 2 → 10.

**Path:** `src/BuildingBlocks/Application/docs/INTEGRATION_ADAPTERS.md`

````md
# Integration Adapters (Infrastructure)
> How Infrastructure wires brokers/transports to the Application eventing pipeline — without leaking transport concerns into Application.

This document is the definitive playbook for building transport adapters (Kafka, RabbitMQ, Azure Service Bus, MassTransit, HTTP/webhooks, etc.) for **Axon Backend**.

It is grounded in the final Application design we created in Phase 1 (Stories 2–10):

- **Two-lane eventing model**
  - **Lane A (In-Process):** `IDomainEvent` → `DomainEventNotification<T>` → MediatR (post-commit only)
  - **Lane B (Cross-Boundary):** `IDomainEvent` → Outbox → Mapping → **IntegrationEventEnvelope** → Transport
- **Outbound:** Envelopes, header policy, idempotency keys, backoff policy, metrics (Stories 5–8)
- **Inbound (Inbox):** Idempotent consumption, type resolution, retry/dead-letter policy, metrics (Stories 9–10)
- **Application purity:** No transport references in Application

---

## 0) Quick Map — What you implement vs. what you call

**Outbound (publish)**
- You **implement** (Infra):
  - `IIntegrationEventPublisher.PublishAsync(IReadOnlyList<IntegrationEventEnvelope>)`
- Application **gives** you:
  - `IntegrationEventEnvelope` (`Id`, `TypeName`, `SchemaVersion`, `ProducedAtUtc`, `Data` **(raw bytes)**, `Headers`)
  - `IEventSerializer` (for payload bytes) — already used before you get the envelope, but also available if your transport needs typed messages

**Inbound (consume)**
- You **call** (from Infra):
  - `IInboundIntegrationEventDispatcher.DispatchAsync(IntegrationEventEnvelope, ct)`
- You **build**:
  - `IntegrationEventEnvelope` from **transport message** body + headers
- You **act** on result:
  - `ACK` / `NACK with delay` / `ACK + DLQ` (dead-letter) — already decided by Application policy

> 🔒 **Separation of concerns:** Application owns mapping, envelopes, header policy, idempotency semantics, retry math, error taxonomy. Infrastructure just **adapts** to the broker.

---

## 1) Canonical envelope & headers (Lane B)

### IntegrationEventEnvelope
Application produces/consumes this immutable record:
- `Id` (GUID)
- `TypeName` (stable, resolver-backed; see `IntegrationEventNameAttribute` + `IEventTypeNameResolver`)
- `SchemaVersion` (default `1`)
- `ProducedAtUtc` (UTC)
- `Data` (**raw bytes**; JSON by default via `IEventSerializer`)
- `Headers` (`IDictionary<string, object?>`, **case-insensitive**)
- (Optional) `DotnetType` — helpful for strongly typed transports, otherwise unused

> Infra adapters **must not reshape** `Data`. Forward as-is (bytes). Headers are canonical and **must be preserved**.

### Canonical header names (kebab-case)
| Header | Meaning | Typical Source |
|---|---|---|
| `integration-event-id` | Envelope GUID | Envelope.Id |
| `integration-event-type` | Stable name | Type resolver |
| `integration-schema-version` | Payload schema version | Envelope |
| `produced-at` | UTC production time | Envelope |
| `idempotency-key` | **Stable key** (prevents duplicates) | Policy: prefer `outbox-entry-id`, fallback `domain-event-id`, last-resort hash |
| `trace-id` / `span-id` | W3C trace | Envelope context |
| `request-id` | Caller request id | Envelope context |
| `causation-id` | Upstream event id | Envelope context |
| `tenant-id` | Tenant | Envelope context |
| `transaction-id` | DB transaction | Outbox/Context |
| `outbox-entry-id` | Outbox record id | Outbox |
| `delivery-attempt` | Inbound delivery counter | Transport |

> **Story 7** introduced `IEnvelopeHeaderPolicy`: all headers come from one place → consistent, deterministic, and **transport-neutral**.

---

## 2) Outbound adapter (publisher) — responsibilities

**Your job** (Infrastructure) for outbound:
1) **Serialize** payload bytes (**already JSON** via Application; use as-is).
2) **Copy headers** 1:1 to the transport’s metadata system (stringify if needed).
3) **Choose a routing/partition key**:
   - Prefer `idempotency-key`
   - Else `outbox-entry-id`
   - Else `integration-event-id`
4) **Publish efficiently** (batch where possible).
5) **Expose OTEL spans/metrics** at the transport layer.

### Reference skeleton (Kafka example)
```csharp
public sealed class KafkaIntegrationEventPublisher : IIntegrationEventPublisher
{
    private readonly ILogger<KafkaIntegrationEventPublisher> _logger;
    private readonly IProducer<string, byte[]> _producer;

    public KafkaIntegrationEventPublisher(ILogger<KafkaIntegrationEventPublisher> logger,
                                          IProducer<string, byte[]> producer)
    {
        _logger = logger;
        _producer = producer;
    }

    public async Task PublishAsync(IReadOnlyList<IntegrationEventEnvelope> envelopes, CancellationToken ct = default)
    {
        foreach (var env in envelopes)
        {
            var key = TryHeader(env, "idempotency-key")
                   ?? TryHeader(env, "outbox-entry-id")
                   ?? env.Id.ToString();

            var headers = new Headers();
            foreach (var (k, v) in env.Headers)
            {
                if (v is null) continue;
                headers.Add(k, System.Text.Encoding.UTF8.GetBytes(v.ToString()!));
            }

            var message = new Message<string, byte[]>
            {
                Key = key,
                Value = env.Data,   // **bytes from Application**
                Headers = headers
            };

            var topic = ResolveTopic(env.TypeName);
            var dr = await _producer.ProduceAsync(topic, message, ct);
            _logger.LogDebug("Published {Type} to {Topic} @ {Partition}:{Offset}",
                             env.TypeName, topic, dr.Partition, dr.Offset);
        }
    }

    private static string? TryHeader(IntegrationEventEnvelope e, string key)
        => e.Headers.TryGetValue(key, out var v) ? v?.ToString() : null;

    private static string ResolveTopic(string typeName) => $"integration.{typeName}".ToLowerInvariant();
}
````

> **RabbitMQ/Azure SB/MassTransit**: identical steps — set headers, send bytes, pick key/routing, **do not** deserialize/rewrite payloads.

---

## 3) Inbound adapter (consumer) — responsibilities

**Your job** (Infrastructure) for inbound:

1. Read **raw body bytes** + headers from broker.
2. Build `IntegrationEventEnvelope`:

    * `TypeName` = `integration-event-type` header
    * `Data` = **raw bytes** from the transport
    * `Headers` = all headers (lowercase keys recommended)
    * `Id` = from `integration-event-id` or new GUID for broken inputs
    * `SchemaVersion` = from header or `"1"`
    * `ProducedAtUtc` = from header or `UtcNow` (fallback)
3. Call `IInboundIntegrationEventDispatcher.DispatchAsync(envelope)`.
4. Apply result → `ACK / NACK with delay / DLQ`.

### Reference skeleton (Kafka background consumer)

```csharp
public sealed class KafkaInboundConsumer : BackgroundService
{
    private readonly IInboundIntegrationEventDispatcher _dispatcher;
    private readonly IConsumer<string, byte[]> _consumer;
    private readonly ILogger<KafkaInboundConsumer> _logger;

    public KafkaInboundConsumer(IInboundIntegrationEventDispatcher dispatcher,
                                IConsumer<string, byte[]> consumer,
                                ILogger<KafkaInboundConsumer> logger)
    {
        _dispatcher = dispatcher;
        _consumer = consumer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe("integration.*");

        while (!stoppingToken.IsCancellationRequested)
        {
            var cr = _consumer.Consume(stoppingToken);
            if (cr is null) continue;

            var headers = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var h in cr.Message.Headers)
                headers[h.Key] = System.Text.Encoding.UTF8.GetString(h.GetValueBytes());

            var env = new IntegrationEventEnvelope(
                id: ParseGuid(headers, "integration-event-id") ?? Guid.NewGuid(),
                typeName: headers.TryGetValue("integration-event-type", out var t) ? t?.ToString() ?? "unknown" : "unknown",
                schemaVersion: headers.TryGetValue("integration-schema-version", out var sv) ? sv?.ToString() ?? "1" : "1",
                producedAtUtc: ParseDate(headers, "produced-at") ?? DateTime.UtcNow,
                data: cr.Message.Value,      // **raw bytes**
                dotnetType: null,            // optional; resolver can infer in App
                headers: headers);

            var result = await _dispatcher.DispatchAsync(env, stoppingToken);

            if (result.ShouldAck)
            {
                _consumer.Commit(cr); // ACK
            }
            else if (result.RetryAfter is { } delay)
            {
                // Kafka retry options:
                // - Backoff loop: leave uncommitted + pause/resume
                // - Retry topics: publish to T.retry.Xmin with delay
                _logger.LogWarning("Transient failure. Should retry after {Delay}.", delay);
            }
            else if (result.DeadLettered)
            {
                // Optionally mirror Application DLQ to broker DLQ
                _consumer.Commit(cr); // ACK to avoid looping
            }
        }
    }

    private static Guid? ParseGuid(IDictionary<string, object?> h, string k)
        => h.TryGetValue(k, out var v) && Guid.TryParse(v?.ToString(), out var g) ? g : null;

    private static DateTime? ParseDate(IDictionary<string, object?> h, string k)
        => h.TryGetValue(k, out var v) && DateTime.TryParse(v?.ToString(), out var d) ? d : null;
}
```

---

## 4) Two-lane model — sequence (ASCII)

### Outbound (Lane B)

```
Domain Aggregate --raises--> IDomainEvent
             \__ Post-Commit (Lane A) --> DomainEventNotification<T> (MediatR)
              \_ Outbox store (same TX)
                     |
            Outbox Processor (batch, backoff, metrics)
                     |
            IntegrationEventDispatcher
                     |
            EnvelopeFactory + HeaderPolicy + Serializer
                     |
            IIntegrationEventPublisher (Infra adapter)
                     |
                   Broker
```

### Inbound (Inbox)

```
Transport --> Adapter reads bytes+headers --> IntegrationEventEnvelope
      |
      v
IInboundIntegrationEventDispatcher
  |  - Idempotency via IInboxStore (Story 9)
  |  - Type resolution & handler routing
  |  - Error taxonomy & retry policy (Story 10)
  v
Handlers (IIntegrationEventHandler<TEvent>)
  |
Result --> Adapter maps to ACK / NACK(delay) / DLQ
```

---

## 5) Idempotency & attempts (must-do)

* **Outbound duplicates** are prevented by **Outbox** and the **stable `idempotency-key`**.
* **Inbound duplicates** are prevented by **Inbox idempotency**:

    * Adapter must carry `idempotency-key` intact.
    * Adapter should include a `delivery-attempt` header **if** the transport exposes it (e.g., AMQP x-death, Kafka retry topic counters, Azure SB `DeliveryCount`).
    * Application uses `IInboxStore` for *atomic* “first win” processing.

**Rule:** never attempt “exactly-once” at the broker layer. We do **at-least-once** + idempotent handlers.

---

## 6) Retry & dead-letter (inbound) — mapping to adapter behavior

Application returns a decision (via `DispatchAsync` result):

* `ShouldAck = true` → **ACK** the message
* `RetryAfter = TimeSpan` → **NACK with delay** (or re-route to a retry queue/topic with that delay)
* `DeadLettered = true` → **ACK** (Application already recorded DLQ forensics; adapter may also forward to broker DLQ)

**Transport notes**

* **Kafka**: use retry topics (e.g., `topic.retry.5m`) or pause/resume with backoff.
* **RabbitMQ**: use delayed exchange / `x-death` to compute attempts; `NACK` with requeue=false and route to delayed or DLX.
* **Azure SB**: schedule messages for future delivery using `ScheduledEnqueueTimeUtc`; use Queue DLQ for dead-letter.

---

## 7) OTEL & Metrics (what adapters should add)

Application already provides:

* Outbox/Inbox metrics hooks (`IOutboxMetrics` extended with inbound hooks, Stories 8–10)
* Envelope context propagation through `IEnvelopeContextAccessor`

Your adapter should add **transport** spans/metrics:

* Spans: publish, batch publish, consume, handler dispatch
* Attributes: topic/queue, partition, offset/seqNo, attempt, payload size
* Metrics:

    * Outbound: publish count, batch size, latency, error count
    * Inbound: consumed, handled, retried (with delay buckets), dead-lettered

**Trace propagation**

* On consume, if `trace-id` exists, start span with `traceparent` root if your SDK supports it; otherwise tag the span.

---

## 8) Transport header mapping quick reference

| Canonical (Application)                             | Kafka                             | RabbitMQ (AMQP)                                    | Azure Service Bus                               |
| --------------------------------------------------- | --------------------------------- | -------------------------------------------------- | ----------------------------------------------- |
| `integration-event-id`                              | `Headers["integration-event-id"]` | `IBasicProperties.Headers["integration-event-id"]` | `ApplicationProperties["integration-event-id"]` |
| `integration-event-type`                            | same                              | same                                               | same                                            |
| `integration-schema-version`                        | same                              | same                                               | same                                            |
| `produced-at`                                       | same                              | same                                               | same                                            |
| `idempotency-key`                                   | same                              | same                                               | same                                            |
| `trace-id`, `span-id`, `request-id`, `causation-id` | same                              | same                                               | same                                            |
| `tenant-id`, `transaction-id`, `outbox-entry-id`    | same                              | same                                               | same                                            |
| `delivery-attempt`                                  | you compute/propagate             | derive from `x-death`/headers                      | from `DeliveryCount` (add to headers)           |

> If your transport limits header value types, **stringify**. Keep keys **lowercase** for consistency.

---

## 9) Topics/Queues — naming guidance

* **Producer (outbound)**:

    * Default topic/queue: `integration.{integration-event-type}` (kebab/lowercase)
    * Allow config overrides per module/event
* **Retry**:

    * Kafka: `integration.{type}.retry.{delay}`, e.g., `integration.order-created.retry.5m`
    * Rabbit: one per delay via delayed exchange; route by header
    * Azure SB: schedule to same queue or dedicated retry queue with delays
* **DLQ**:

    * Use broker DLQ **and** keep Application DLQ forensics (story 10); they’re complementary.

---

## 10) Security, tenancy, and PII

* Treat all inbound headers as **untrusted**. Application handlers must validate tenant and authorization.
* Do not place PII in headers; keep correlation-only keys.
* Use transport-level encryption/authz as standard.

---

## 11) Versioning & compatibility

* `integration-event-type`: **stable**. Version by schema, not by name.
* `integration-schema-version`: bump on breaking payload changes.
* For upgrades: publish **both** v1 and v2 for a window; consumers bind to the version they support.
* Application’s `IEventTypeNameResolver` and `IntegrationEventNameAttribute` ensure stable names.

---

## 12) Adapter acceptance checklist (per transport)

**Outbound**

* [ ] Publishes **bytes** from `IntegrationEventEnvelope.Data`
* [ ] Copies canonical headers **verbatim**
* [ ] Uses stable partition/routing key (`idempotency-key` → `outbox-entry-id` → `integration-event-id`)
* [ ] Batches efficiently; resilient producer config (retries/acks)
* [ ] Emits transport spans/metrics

**Inbound**

* [ ] Builds `IntegrationEventEnvelope` with **raw bytes** + headers
* [ ] Sets `integration-event-type` (required) and `integration-event-id`
* [ ] Propagates/derives `delivery-attempt`
* [ ] Calls `_dispatcher.DispatchAsync(envelope)`
* [ ] Maps result → ACK/NACK(delay)/DLQ correctly
* [ ] Emits transport spans/metrics

**Operational**

* [ ] Retry topology set (retry topics/queues or schedule)
* [ ] DLQ routing configured, with headers preserved
* [ ] Configuration documented (topics, exchanges, policies)
* [ ] Load/perf tested with representative batch sizes

---

## 13) Brownfield notes (migrating existing adapters)

* If you used `EventDispatcher` with MassTransit/ASP.NET deps, **retire it**.
* Replace direct publishes with `IIntegrationEventPublisher`.
* For inbound consumers, stop deserializing to typed .NET DTOs — **hand bytes** to Application and let it resolve types/deserialize (Story 9).
* Preserve all headers; Application now depends on `idempotency-key`, correlation, and causation.

---

## 14) Example: MassTransit notes

**Outbound**: publish a message with a **byte\[] body** and set headers; MT supports custom serializers and headers. Don’t publish strong typed “contracts” at the Application boundary — use the envelope.

**Inbound**: MT consumer converts MT `ConsumeContext` → `IntegrationEventEnvelope` (copy headers, body bytes), then call `_inboundDispatcher.DispatchAsync(envelope)` and map result to `context` ACK/retry/DLQ.

---

## 15) Local development & testing strategy

* Start with `NoOpIntegrationEventPublisher` + `NoOpInboxStore` (already provided) to verify handlers and end-to-end logic without a broker.
* Switch to in-memory broker or embedded Kafka/Rabbit containers in CI.
* Contract tests:

    * Ensure outbound headers include `idempotency-key`, `integration-event-type`, `produced-at`.
    * Ensure inbound adapter forwards headers unchanged and never mutates payload JSON.
* Chaos: inject transient failures to confirm retry/delay mapping works.

---

## 16) Advanced performance tips

* Reuse producer/connection instances (singleton) with backpressure control.
* Avoid per-message allocations when mapping headers (pre-encode UTF-8).
* Batch sends where possible; flush on interval/size thresholds.
* For Kafka: tune linger.ms, batch.size, compression.type; for Rabbit: publisher confirms; for Azure SB: prefetch/settlement tuning.
* Keep envelope `Data` small and focused; large blobs belong in object storage with pointers in payload.

---

## 17) DI wiring (Infra)

**Outbound**

```csharp
// Infrastructure module
services.AddSingleton<IIntegrationEventPublisher, KafkaIntegrationEventPublisher>();
// Application side already: services.AddApplicationEventing() / AddIntegrationEventPipeline()
```

**Inbound**

```csharp
// Infrastructure module
services.AddHostedService<KafkaInboundConsumer>();
// It will get IInboundIntegrationEventDispatcher from Application DI
```

---

## 18) FAQ

**Q: Can we enrich headers at the adapter?**
A: Only if you **add** (never overwrite) and never conflict with canonical keys. Prefer adding transport-specific keys with a distinct prefix (e.g., `kafka-partition`).

**Q: Do we ever deserialize to typed messages in Infra?**
A: No. Treat payload as opaque bytes and forward to Application. Mapping/typed deserialization belongs inside Application.

**Q: Where do we compute idempotency?**
A: Outbound: Application’s Outbox + stable `idempotency-key`. Inbound: Application’s Inbox via `IInboxStore`. The adapter just preserves the key.

---

## 19) TL;DR

* **Adapter rule #1:** Don’t think — **forward**: payload bytes + headers, unchanged.
* **Outbound:** Implement `IIntegrationEventPublisher`.
* **Inbound:** Call `IInboundIntegrationEventDispatcher`.
* **Application owns** headers, idempotency, mapping, retry, DLQ, metrics, and serialization.
* **You own** the wire.

This guarantees transport neutrality, repeatable behavior across environments, and easy broker swaps with zero changes to the Application layer.

```

If you want, I can add per-transport starter kits (Kafka/Rabbit/Azure SB/MassTransit) as separate `docs/adapters/*.md` with ready-to-paste code and recommended client configs.
```
