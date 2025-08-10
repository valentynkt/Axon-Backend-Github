# 00\_CHAT\_DOMAIN\_ABSTRACTIONS (MVP, no code) — Final

## 1) Purpose & scope

MVP chat: a single human converses with an assistant (and occasionally system/tool). The **domain** guarantees a clean, append-only conversation with strict ordering and minimal, stable business rules. No infrastructure or AI details leak into domain.

## 2) Ubiquitous language

* **Conversation** — one chat thread owned by one human.
* **Message** — one utterance in the thread.
* **Role** — producer of a message: `user | assistant | system | tool`.
* **Complete** — the conversation is closed to new messages.
* **Metadata** — small, domain-agnostic key/value bag attached to a message (domain doesn’t inspect it).

## 3) Aggregate model

### Conversation (Aggregate Root)

* **Id**: `ConversationId`.
* **Owner**: `UserId` (one human; no other members).
* **Title**: optional; max length (≤ **200** chars).
* **Status**: `Active | Completed` (MVP only two states).
* **CompletedAt**: set when status becomes `Completed`.
* **Messages**: ordered collection of `Message`, with **consecutive Sequence** starting at 1.
* **Derived**: `MessageCount`, `IsActive`.

### Message (Entity within Conversation)

* **Id**: `MessageId`.
* **ConversationId**: backreference (must match parent).
* **Role**: `MessageRole` ∈ {user, assistant, system, tool}.
* **Content**: `MessageContent` (trimmed, non-empty, length ≤ **100,000** chars).
* **Sequence**: positive int assigned by Conversation (`MessageCount + 1`).
* **Timestamps**: `CreatedAt` (for audit). No edits/deletes in MVP.
* **Metadata**: optional, opaque to domain.

### Value objects

* **ConversationId / MessageId / UserId** — strong IDs.
* **MessageRole** — constrained set (user/assistant/system/tool).
* **MessageContent** — trimmed, non-empty string with max length (≤ 100,000).
* **ConversationStatus** — `Active | Completed`.

## 4) State machine (Conversation)

* **Initial**: `Active` (created by Start).
* **Active → Completed**: **Complete** operation.
* **Completed**: terminal for MVP (no appends; no exit transitions).

## 5) Operations (domain behaviors)

1. **Start**

    * **Input**: `OwnerId`, optional `Title`.
    * **Preconditions**: `OwnerId` present; if `Title` provided, length ≤ 200.
    * **Postconditions**: `Status = Active`, messages empty, maybe set Title, emit `ConversationStarted`.

2. **AppendMessage**

    * **Input**: `Role`, `Content`, optional `Metadata`.
    * **Preconditions**: `Status == Active`; `Role` valid; `Content` non-empty & ≤ 100,000.
    * **Postconditions**: create `Message` with `Sequence = MessageCount + 1`, add to list, emit `MessageAppended`.
    * **Notes**: No rate limiting, deduplication, token budgeting in domain.

3. **UpdateTitle**

    * **Input**: `NewTitle` (optional).
    * **Preconditions**: if provided, length ≤ 200.
    * **Postconditions**: set Title (can be empty/cleared). No event required in MVP.

4. **Complete**

    * **Preconditions**: `Status == Active`.
    * **Postconditions**: `Status = Completed`, set `CompletedAt`, emit `ConversationCompleted`.
    * **Effects**: blocks future appends.

## 6) Invariants (must always hold)

* Conversation has a valid **OwnerId**.
* **Status** is `Active` or `Completed`.
* **Messages** are strictly ordered by **Sequence** with no gaps (1…N).
* Every message has a valid **Role** and valid **Content** (non-empty, ≤ 100,000).
* When `Status != Active`, **AppendMessage** is not permitted.

## 7) Domain events (minimal & factual)

* **ConversationStarted**(ConversationId, OwnerId, Title)
* **MessageAppended**(ConversationId, MessageId, Sequence, Role)
* **ConversationCompleted**(ConversationId, OwnerId, MessageCount)

> Events are **pure domain** facts. The application layer wraps, persists to Outbox, and maps to integration events. Domain knows nothing about buses or outbox.

## 8) Out of scope for MVP (explicit exclusions)

* AI/LLM specifics (token limits, context building, truncation).
* MCP/Tooling infrastructure (server URLs, tool execution details).
* Rate limiting, duplicate detection, spam heuristics.
* Edits/deletes, reactions, pins, attachments.
* Multi-user/membership, rooms, permissions beyond ownership.
* Caching, retries, transactions, outbox, brokers, MassTransit/RabbitMQ.
* Specifications/queries beyond simple owner/status filters (handled in app/repo).

## 9) Extension seams (future)

* Add **edits/deletes**: messages already have identity.
* Add **turns** (user→assistant) without changing message model.
* Add **ConversationSettings** to carry policy (max messages, allowed roles).
* Enrich **tool** messages (tool call/result) via metadata or a future `MessageKind`.
* Add **archiving** as another terminal state if needed later.
* Add **title change event** if external listeners care.

## 10) Dependency rules

* Domain layer **does not depend** on application, infrastructure, AI SDKs, brokers, or EF.
* Optimistic concurrency, outbox, serialization, and mapping are **infra concerns**.
* Domain events are plain objects (no envelopes/headers).

## 11) Implementation guidance (ordering & correctness)

* Assign `Sequence = MessageCount + 1` atomically within the aggregate when appending.
* Validate **preconditions first**, then mutate state, then raise the event.
* Keep **Message** creation simple; Conversation enforces ownership and lifecycle.
* Keep **metadata** opaque; size/shape limits belong to infra/app.

---
