Alright—rolling right into the next foundational doc. Given what we’ve locked so far, the next logical layer for the domain (before we write epics/stories) is the **domain events contract**: what facts we emit, when, and with what payloads. This keeps projections, integrations, and tests aligned and prevents churn later.

# 05\_CHAT\_DOMAIN\_EVENTS & SEMANTICS (MVP)

## Scope & principles (recap)

* **Domain only**: in-process facts about state changes. Outbox/envelopes/brokers are **application/infra**.
* **Small, stable facts**: include IDs and minimal denormalized fields needed for projections.
* **No aggregateVersion in domain** (per decision); app/infra may attach it from persistence.
* **Event versioning**: each event has `schemaVersion = 1` (bump only on breaking changes).
* **Ordering**: within a single `Conversation`, events are raised synchronously after mutation; `MessageAppended.sequence` is the ordering source of truth.
* **Idempotency**: consumers use event’s **natural keys** (see per-event below).

---

## Event catalog (MVP)

### 1) `ConversationStarted`

**When**: conversation is created (MVP requires ≥ 1 message at start; title may be user-provided or default plain string but we track `isDefaultTitle`).

**Preconditions**

* Owner validated.
* Title validated (≤ 200).
* Status becomes `Active`.
* No messages yet at the *moment* of this event (first message will follow with `sequence = 1`).

**Payload (domain fact)**

* `conversationId` (Guid)
* `ownerId` (Guid)
* `title` (string)
* `isDefaultTitle` (bool)
* `occurredAt` (UTC)

**Idempotency key**

* `conversationId` (a conversation can start once)

**Notes**

* If later we support title regeneration, that’s a separate event (not in MVP).

---

### 2) `MessageAppended`

**When**: a new message (user or assistant) is appended to the conversation.

**Preconditions**

* Conversation is `Active`.
* Content validated (1..100k).
* Message limit not exceeded (≤ 10k).
* **Turn-taking**: no consecutive **assistant** messages.
* Sequence will be `messages.Count + 1` (contiguous; starts at 1).

**Payload (domain fact)**

* `conversationId` (Guid)
* `messageId` (Guid)
* `role` (`"user"` | `"assistant"`)
* `sequence` (int, 1-based)
* `contentPreview` (string; first **100** chars, trimmed)
* `contentLength` (int; length of full content)
* `occurredAt` (UTC)

**Idempotency key**

* `(conversationId, messageId)` — unique per message
* Projections that also key by `sequence` can guard with `(conversationId, sequence)`

**Notes**

* Domain event intentionally **does not** carry full content (keeps events light; app layer can fetch aggregate if needed).
* If we ever add System/Tool roles, this event evolves (non-breaking if we only extend enum).

---

### 3) `ConversationTitleUpdated` *(optional but useful; included in MVP)*

**When**: title is updated by owner (or by domain defaulting behavior in a future evolution).

**Preconditions**

* New title validated (1..200).
* Must differ from current title.

**Payload (domain fact)**

* `conversationId` (Guid)
* `title` (string)
* `isDefaultTitle` (bool)
* `occurredAt` (UTC)

**Idempotency key**

* `(conversationId, title)` (same title update is idempotent)

**Notes**

* Having explicit title events simplifies list/projection updates without reloading the aggregate.

---

### 4) `ConversationCompleted`

**When**: conversation is marked completed.

**Preconditions**

* Status is `Active`.
* `messageCount > 0`.
* `completedAt` set.

**Payload (domain fact)**

* `conversationId` (Guid)
* `ownerId` (Guid)
* `messageCount` (int)
* `occurredAt` (UTC)

**Idempotency key**

* `conversationId` (completion is a one-way toggle in MVP)

**Notes**

* MVP has no “reopen” or “archive” events. If added later, introduce `ConversationReopened`/`ConversationArchived`.

---

## Cross-event guarantees & sequencing

* **Per-aggregate total order**: For one `conversationId`, domain raises events in the order mutations are applied.
* **Monotonic message sequence**: `MessageAppended.sequence` is strictly contiguous; gaps are invariant breaches (should never emit).
* **Time**: `occurredAt` is UTC at mutation time; do not use time to order within the same conversation—use `sequence`.

---

## Schema versioning & evolution

* Each event implicitly carries `schemaVersion = 1`.
* **Non-breaking changes**: add new optional fields; consumers must ignore unknown fields.
* **Breaking changes**: bump to `schemaVersion = 2`, keep old handlers until all consumers migrate (app/infra policy).

---

## Envelope & correlation (out of domain)

* **Aggregate version**: **omitted** in domain events (per decision). Infra can enrich envelopes from persistence.
* **Correlation/Causation IDs**: not part of domain payload; infra may add.
* **Outbox**: outside scope here; domain emits facts, app persists/envelopes.

---

## Projections & read models (guidance)

* **Conversation list item**: listen to `ConversationStarted`, `ConversationTitleUpdated`, `MessageAppended`, `ConversationCompleted`.

    * Keep `lastMessagePreview` from `contentPreview` and `lastMessageAt` from `occurredAt` of last `MessageAppended`.
    * Maintain `messageCount` by incrementing on `MessageAppended`.
    * Update `title`/`isDefaultTitle` on `ConversationTitleUpdated`.
    * Mark `status = Completed` on `ConversationCompleted`.

* **Message feed** (if needed in MVP):

    * Append on `MessageAppended` keyed by `(conversationId, sequence)` for idempotency.

---

## Failure semantics

* Domain **never emits** an event on a failed operation (validation/forbidden/conflict).
* All domain methods are transactional per aggregate; invariant breaches are treated as **Unexpected** and should not leak as valid events.

---

## Event naming & casing

* **Type names**: `ConversationStarted`, `MessageAppended`, `ConversationTitleUpdated`, `ConversationCompleted`.
* **Field names**: lowerCamelCase (e.g., `conversationId`, `contentPreview`).
* **Roles**: exactly `"user"` or `"assistant"` in MVP.

---

## Examples (conceptual shapes)

```json
// ConversationStarted v1
{
  "type": "ConversationStarted",
  "schemaVersion": 1,
  "conversationId": "6f4dcf41-...-b2f3",
  "ownerId": "e2a5b7c0-...-d91a",
  "title": "Trip planning with AI",
  "isDefaultTitle": false,
  "occurredAt": "2025-08-10T12:34:56Z"
}
```

```json
// MessageAppended v1
{
  "type": "MessageAppended",
  "schemaVersion": 1,
  "conversationId": "6f4dcf41-...-b2f3",
  "messageId": "a1b2c3d4-...-e5f6",
  "role": "assistant",
  "sequence": 5,
  "contentPreview": "Sure! To plan your trip, I’ll need a few details...",
  "contentLength": 428,
  "occurredAt": "2025-08-10T12:35:07Z"
}
```

```json
// ConversationTitleUpdated v1
{
  "type": "ConversationTitleUpdated",
  "schemaVersion": 1,
  "conversationId": "6f4dcf41-...-b2f3",
  "title": "Italy trip planner",
  "isDefaultTitle": false,
  "occurredAt": "2025-08-10T12:36:10Z"
}
```

```json
// ConversationCompleted v1
{
  "type": "ConversationCompleted",
  "schemaVersion": 1,
  "conversationId": "6f4dcf41-...-b2f3",
  "ownerId": "e2a5b7c0-...-d91a",
  "messageCount": 17,
  "occurredAt": "2025-08-10T13:02:41Z"
}
```

---

## What’s intentionally **not** in MVP events

* Full message body (keeps events light; repositories can load full content when needed).
* System/Tool roles (can be added later; extending `role` set is non-breaking).
* Aggregate version, correlation/causation IDs (infra concerns).
* Any transport/bus headers (infra concerns).

---

If this looks good, next up I recommend **06\_STATE MODEL & INVARIANTS (MVP)** as a single visual/structured reference (fields, transitions, and invariants) so devs can implement aggregates confidently without hopping between docs.
