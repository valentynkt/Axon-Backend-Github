awesome — locking 01\_\* and moving on.

# 02\_EVENT CATALOG & SEMANTICS (MVP)

## Purpose

Define the **domain events** emitted by the Chat bounded context (MVP), their exact semantics, payloads, ordering, and invariants. This is **domain-layer only** (pure in-process domain events). Integration/outbox mapping is noted but specified elsewhere.

---

## Cross-cutting constraints

* **Event style**: past-tense facts; immutable; small payloads (IDs + essentials).
* **When emitted**: *after* successful state change inside the same unit of work.
* **Ordering**: per-aggregate (Conversation) events are emitted **in the same order** as state transitions.
* **Idempotency keys**: `EventId` (Guid) + `ConversationId` + `AggregateVersion` (if needed by handlers).
* **Serialization**: `System.Text.Json` + StrongId JSON converters (domain doesn’t care about formats, but our canonical shapes are shown for clarity).
* **Privacy**: do **not** emit full message content in domain events (MVP). Use `contentPreview` (<=100 chars) + `contentLength` if needed.
* **Turn-taking**: “no consecutive Assistant messages” is enforced **before** emit; violations produce **no** event.
* **Status gates**: appends & renames only when `Conversation.Status == Active`; completion requires `MessageCount ≥ 1`.

---

## Event list (MVP)

1. **ConversationStarted**
2. **MessageAppended**
3. **ConversationTitleUpdated**
4. **ConversationCompleted**

> We intentionally **unify** user/assistant append into a single `MessageAppended` event (was split in legacy). Fewer event types, same signal power.

---

## Common envelope (conceptual)

All domain events carry (at minimum):

* `eventId` (Guid) — unique per event instance
* `occurredAt` (UTC DateTime) — emission time
* `conversationId` (StrongId)

> In code, these may live on a base `DomainEvent`/`DomainEventBase`. Domain layer does not require correlation/causation; application may enrich when publishing.

---

## Event specs

### 1) ConversationStarted

**Why**: Announces a new conversation exists and is `Active`.
**Emitted**: After `Conversation.StartNewConversation(...)` succeeds.
**Never when**: Title validation fails; ownership invalid.

**Payload**

* `conversationId` (ConversationId)
* `ownerId` (UserId)
* `title` (string, trimmed, 1–200) — may be defaulted to “New chat {shortId}”
* `isDefaultTitle` (bool)
* `startedAt` (UTC)

**Invariants at emit**

* Status == `Active`
* MessageCount == 0

**Ordering/Idempotency**

* First event for a given conversation.
* Handlers may treat (`conversationId`,`eventId`) as idempotency pair.

**Example**

```json
{
  "eventName": "ConversationStarted",
  "eventId": "7e2a3a92-0b01-4e2a-9e0a-0b2f1a5b0c77",
  "occurredAt": "2025-08-10T12:00:00Z",
  "conversationId": "6f2a84b5-2c3a-4e8b-9f6e-bc9d3a1c3f74",
  "ownerId": "c9c6f9e3-4b40-4f7f-9b14-1cfccb5a9c10",
  "title": "New chat 6f2a84b5",
  "isDefaultTitle": true,
  "startedAt": "2025-08-10T12:00:00Z"
}
```

---

### 2) MessageAppended

**Why**: A message (User or Assistant) has been added to the transcript.
**Emitted**: After `AppendUserMessage` or `AppendAssistantMessage` succeeds.
**Never when**: Conversation not `Active`; content invalid; limit exceeded; **Assistant twice in a row** (turn-taking).

**Payload**

* `conversationId` (ConversationId)
* `messageId` (MessageId)
* `role` (“user” | “assistant”) — MVP roles only
* `sequence` (int, ≥1, contiguous)
* `contentLength` (int) — character count
* `contentPreview` (string, ≤100 chars; safe snippet, trimmed)
* `appendedAt` (UTC)

> **No full content** in domain event (privacy & minimalism). Handlers that need full content must load it via repository.

**Invariants at emit**

* `sequence == MessageCount after append`
* If `role == assistant`: previous message’s role != assistant
* MessageCount ≤ 10\_000

**Ordering/Idempotency**

* Events strictly increase by `sequence`.
* Handlers can dedupe by (`conversationId`, `messageId`) or (`conversationId`, `sequence`).

**Example**

```json
{
  "eventName": "MessageAppended",
  "eventId": "b5a2c97e-9a1e-4c0d-9f2b-7b3a6f5d2a11",
  "occurredAt": "2025-08-10T12:02:00Z",
  "conversationId": "6f2a84b5-2c3a-4e8b-9f6e-bc9d3a1c3f74",
  "messageId": "0b1e9f3a-2d44-49f0-8ade-1f4d0e2a6c55",
  "role": "user",
  "sequence": 1,
  "contentLength": 57,
  "contentPreview": "how do I write a minimal chat domain for an MVP?",
  "appendedAt": "2025-08-10T12:02:00Z"
}
```

---

### 3) ConversationTitleUpdated

**Why**: The title changed (renaming).
**Emitted**: After `UpdateTitle` succeeds.
**Never when**: Conversation not `Active` (MVP rule).

**Payload**

* `conversationId` (ConversationId)
* `oldTitle` (string)
* `newTitle` (string, 1–200, trimmed)
* `updatedAt` (UTC)

**Invariants at emit**

* Status == `Active`
* `oldTitle != newTitle`

**Ordering/Idempotency**

* Multiple renames possible; order preserved.
* Handlers dedupe with (`conversationId`, `newTitle`, `updatedAt`) if needed.

**Example**

```json
{
  "eventName": "ConversationTitleUpdated",
  "eventId": "6b0a5b6b-1f09-4d1f-9c83-4e871b8d7f0a",
  "occurredAt": "2025-08-10T12:03:00Z",
  "conversationId": "6f2a84b5-2c3a-4e8b-9f6e-bc9d3a1c3f74",
  "oldTitle": "New chat 6f2a84b5",
  "newTitle": "Chat Domain MVP",
  "updatedAt": "2025-08-10T12:03:00Z"
}
```

---

### 4) ConversationCompleted

**Why**: The conversation moves to terminal `Completed`.
**Emitted**: After `Complete` succeeds.
**Never when**: `MessageCount == 0` or already completed.

**Payload**

* `conversationId` (ConversationId)
* `ownerId` (UserId)
* `messageCount` (int, ≥1)
* `completedAt` (UTC)

**Invariants at emit**

* Status == `Completed`
* `messageCount` is final size at completion

**Ordering/Idempotency**

* Single terminal event per conversation (unless we later allow re-open—MVP: we do not).
* Dedupe by (`conversationId`, `completedAt`).

**Example**

```json
{
  "eventName": "ConversationCompleted",
  "eventId": "d2a1a8b4-8a1e-4a6e-9d4a-b37efb1e9a9f",
  "occurredAt": "2025-08-10T12:10:00Z",
  "conversationId": "6f2a84b5-2c3a-4e8b-9f6e-bc9d3a1c3f74",
  "ownerId": "c9c6f9e3-4b40-4f7f-9b14-1cfccb5a9c10",
  "messageCount": 3,
  "completedAt": "2025-08-10T12:10:00Z"
}
```

---

## Event evolution & compatibility (MVP rules)

* **Non-breaking additions**: add **new optional fields only**; never change meaning of existing fields.
* **Breaking changes**: create a new event **name** (e.g., `MessageAppendedV2`) and keep the old one for compatibility until migration completes.
* **Do not** remove fields in the same name/version.
* **Semantics are binding**: handlers may rely on invariants described above.

---

## Mapping to integration events (heads-up)

* Domain events are **in-process**. If/when projected to outbox/broker:

    * Keep names but consider contract DTOs (e.g., `Chat.MessageAppended.V1`) with explicit versioning.
    * Preserve `conversationId`, `messageId`, `role`, `sequence`; omit full content by default.
    * Include `aggregateVersion` if required for consumers.
* Outbox retention & delivery semantics are outside this doc.

---

## Quality gates for handlers (guidance)

* Treat events as **at-least-once** (even in-process, be idempotent).
* Never assume cross-aggregate ordering.
* If you need full message text, **load by `messageId`**—do not extend event payload in MVP.
* Validate invariants you depend on (e.g., `sequence` monotonic).

---

## Open questions (confirm or defer)

1. **Include `aggregateVersion`** in the domain event envelope now (for idempotent projections)?
   *Default*: omit in domain, app can attach from persistence if needed.
2. **`contentPreview` max length** — is 100 chars acceptable?
   *Default*: 100.
3. **Expose `titleSource`** (“defaulted” vs “user-provided”) instead of `isDefaultTitle`?
   *Default*: keep `isDefaultTitle` (bool) for MVP.

If you’re happy with the defaults, we’ll lock this and proceed to **03\_DOMAIN INVARIANTS & RULES (executable checklist)** or jump straight to **03\_AGGREGATE CONTRACT (state + methods)** — your call.
