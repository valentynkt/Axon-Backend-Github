# 09\_CHAT\_DOMAIN\_EPIC (MVP) — FINAL

## Epic Summary

Deliver a minimal, robust **Conversation** aggregate (root) with a **Message** child entity for a single-user chat with an **Assistant**, fully aligned with **SPEC-1 BuildingBlocks—Domain Core** and all locked chat docs (00–04, clocks, events). Domain only: no application/infra/broker/EF concerns.

### Scope

* Aggregates: `Conversation` (root), `Message` (child).
* Strong IDs: `ConversationId`, `MessageId`, `UserId`.
* Value Objects: `ConversationTitle`, `MessageContent`, `MessageRole` (User/Assistant only).
* Domain Events: `ConversationStartedEvent`, `UserMessageAppendedEvent`, `AssistantMessageAppendedEvent`, `ConversationCompletedEvent`.
* Rules: title ≤ 200; message 1..100,000 chars; max messages per conversation ≤ 10,000; **no consecutive Assistant**; conversation must be Active to append; Complete requires ≥ 1 message.
* Clock: **`IClock`** (domain abstraction) for all timestamps.
* Functional results: **`Result<T>` / `Result<Unit>`** and **`Validation<Unit>`** everywhere.

### Non-Goals (MVP)

* System/Tool roles, edits/deletes, reactions, attachments, metadata bag.
* Context building/summarization, outbox, envelopes, persistence details.
* Any transport or EF mapping.

---

## Global Decisions & Constraints (locked)

* **Title defaulting:** On start, **empty title is allowed**. Domain sets `isDefaultTitle = true` in `ConversationStartedEvent`. (App may later compute a human title.)
* **Ownership:** Keep `OwnerId : UserId` on `Conversation` (future-proofing), even for single-user MVP.
* **Turn-taking:** Enforce **no consecutive Assistant** messages (consecutive User allowed).
* **Event content preview:** Plain substring to **100** chars (no ellipsis).
* **Events & version:** Domain raises **pure events** (no aggregateVersion in event). If needed, app attaches version in an envelope later.
* **UTC time:** All timestamps from **`IClock.UtcNow`**; tests use a fixed clock.
* **Purity:** No infra/transport references; no JSON attributes in domain types.

---

## Artifacts to Produce (Domain)

* **IDs:** `ConversationId`, `MessageId`, `UserId` (Guid-based StrongIds per BuildingBlocks).
* **VOs:**

    * `ConversationTitle` (empty allowed only at start; otherwise 1..200)
    * `MessageContent` (1..100,000; preserves internal whitespace and case)
    * `MessageRole` (User, Assistant), helpers `IsUser`, `IsAssistant`.
* **Aggregate Root:** `Conversation`

    * State: `Id`, `OwnerId`, `Title`, `IsDefaultTitle`, `Status` (Active/Completed/Archived), `CompletedAt?`, internal `_messages` list, derived `MessageCount`.
    * Behavior: `Start(...)`, `AppendUserMessage(...)`, `AppendAssistantMessage(...)`, `Complete(...)`.
    * Emits events after successful state change.
* **Child Entity:** `Message`

    * State: `Id`, `ConversationId`, `Role`, `Content`, `Sequence`, `CreatedAt`.
    * Immutable after creation.
* **Events (v1):**

    * `ConversationStartedEvent { conversationId, ownerId, title, isDefaultTitle, occurredAt }`
    * `UserMessageAppendedEvent { conversationId, messageId, sequence, role=User, contentPreview(100), occurredAt }`
    * `AssistantMessageAppendedEvent { conversationId, messageId, sequence, role=Assistant, contentPreview(100), occurredAt }`
    * `ConversationCompletedEvent { conversationId, ownerId, messageCount, occurredAt }`
* **Rules:**

    * `MessageContentRule` (1..100k),
    * `ConversationTitleRule` (1..200; only applied where non-empty is required),
    * `NoConsecutiveAssistantRule`,
    * `ConversationMustBeActiveRule`,
    * `MaxMessagesPerConversationRule (≤10k)`,
    * `ConversationHasAtLeastOneMessageToCompleteRule`.
* **Specifications (read-side composition):**

    * `ConversationsByOwnerSpec(UserId)`, `ActiveConversationsSpec`, `ConversationsByStatusSpec` (minimal set).
* **Clock:** Use BuildingBlocks `IClock` abstraction; all behaviors accept an optional clock parameter and default to `SystemClock` (domain core).

---

## User Stories & Acceptance

> IDs (D\*), Value Objects (D\*), Aggregates (A\*), Events (E\*), Rules (R\*), Specs (S\*), Tests/Docs (T\*).
> All error codes/messages must match the **04\_VALIDATION & ERROR CATALOG (MVP)**.

### D1 — Strong IDs

**Deliver:** `ConversationId`, `MessageId`, `UserId`
**AC**

* New/From/Parse guard empty GUID; structural equality holds.
* No primitive IDs leaked in public APIs.

### D2 — MessageContent VO

**Deliver:** `MessageContent.Create(string)`
**AC**

* Reject null/empty; reject >100,000. Preserve internal whitespace and case.
* `GetPreview(100)` returns exact substring length ≤ 100, no ellipsis.
* Validation returns canonical error codes.

### D3 — ConversationTitle VO

**Deliver:** `ConversationTitle.Create(string)`
**AC**

* General rule: 1..200 characters where title is required.
* Start flow may pass empty title; aggregate records `IsDefaultTitle=true` and event carries it.
* Trims ends for validation; preserves inner whitespace/case.

### D4 — MessageRole VO

**Deliver:** User/Assistant only
**AC**

* Normalization to allowed set; helpers `IsUser`, `IsAssistant`.

---

### A1 — Start Conversation (with first User message)

**Signature:** `Conversation.Start(ownerId, firstMessageContent, title?, clock?) → Result<Conversation>`
**AC**

* OwnerId valid; first message `MessageContent` valid; sequence starts at 1.
* If title is null/empty → set aggregate `IsDefaultTitle=true` and store empty title; else accept 1..200 and set `IsDefaultTitle=false`.
* Conversation `Status=Active`, `MessageCount=1`.
* Events raised:

    * `ConversationStartedEvent` (with `isDefaultTitle`)
    * `UserMessageAppendedEvent` (contentPreview 100)
* All timestamps from `clock.UtcNow`.

### A2 — Append User Message

**Signature:** `AppendUserMessage(content, clock?) → Result<Message>`
**AC**

* Requires `Status=Active`; `MessageContent` valid; `MessageCount < 10,000`.
* Consecutive User allowed.
* Appends with next sequence; raises `UserMessageAppendedEvent`.

### A3 — Append Assistant Message (turn-taking enforced)

**Signature:** `AppendAssistantMessage(content, clock?) → Result<Message>`
**AC**

* Requires `Status=Active`; `MessageContent` valid; `MessageCount < 10,000`.
* **NoConsecutiveAssistantRule** holds (last message must not be Assistant).
* Appends with next sequence; raises `AssistantMessageAppendedEvent`.

### A4 — Complete Conversation

**Signature:** `Complete(clock?) → Result<Unit>`
**AC**

* Requires `Status=Active` and `MessageCount ≥ 1`.
* Sets `Status=Completed`, `CompletedAt=clock.UtcNow`.
* Raises `ConversationCompletedEvent`.

> (Optional rename deferred for MVP; if later added, it must clear `IsDefaultTitle=false` when user-provided.)

---

## Events (v1) — Immutable Facts

* Names are past tense and stable; payloads minimal; include `occurredAt` from `IClock`.
* **No aggregateVersion in domain events** (application can attach it in envelopes).
* `contentPreview` generated by domain at emission time (plain 100-char cut).
* Version bump required on breaking payload changes.

---

## Rules (canonical)

* `MessageContentRule` → codes from 04 (e.g., `CHAT.MESSAGE.EMPTY`, `CHAT.MESSAGE.TOO_LONG`).
* `ConversationTitleRule` → codes (e.g., `CHAT.TITLE.EMPTY`, `CHAT.TITLE.TOO_LONG`) when used.
* `NoConsecutiveAssistantRule` → `CHAT.TURN.ASSISTANT_CONSECUTIVE_FORBIDDEN`.
* `ConversationMustBeActiveRule` → `CHAT.CONVERSATION.NOT_ACTIVE`.
* `MaxMessagesPerConversationRule` → `CHAT.CONVERSATION.MAX_MESSAGES_EXCEEDED`.
* `ConversationHasAtLeastOneMessageToCompleteRule` → `CHAT.CONVERSATION.COMPLETE_REQUIRES_MESSAGE`.

All rules used via `RuleBuilder`; results and validations propagate first error consistently.

---

## Specifications (minimal, EF-translatable)

* `ConversationsByOwnerSpec(UserId)`
* `ActiveConversationsSpec`
* `ConversationsByStatusSpec`

> No `UtcNow` or clock usage inside expressions.

---

## Tests & Docs

* **Clocks:** `FixedClock` for deterministic timestamps.
* **Factories/VOs:** D1–D4 happy/sad path coverage.
* **Aggregates:** A1–A4 state + emitted events (payload asserts, preview length, isDefaultTitle flag).
* **Rules:** Positive and negative coverage for each rule.
* **Specs:** Basic composition smoke tests.
* **Docs:** Update `02_CHAT_DOMAIN_LAYER` with final signatures, invariants, and event payloads.

---

## Sequencing

1. D1–D4 (IDs, VOs, Role)
2. Rules (MessageContent, Title, Active, NoConsecutiveAssistant, MaxMessages, HasMessagesToComplete)
3. Events v1 definitions
4. A1 (Start) → A2 (User append) → A3 (Assistant append) → A4 (Complete)
5. Specs
6. Tests & docs

---

## Epic Acceptance

* All public domain APIs return `Result<T>/Result<Unit>` or `Validation<Unit>` with canonical error codes.
* Conversation enforces: title defaulting at start, message bounds, max count, active-only append, **no consecutive Assistant**, completion preconditions.
* Domain events emitted exactly once per successful mutation with `occurredAt` from `IClock` and 100-char `contentPreview`.
* No infra/persistence concerns leak into domain.
* Unit tests prove invariants and event payloads.

---

## Risks & Mitigations

* **Title semantics drift** → kept explicit via `IsDefaultTitle` and event payload; rename (if added later) must clear it.
* **Turn-taking evolution** → isolated `NoConsecutiveAssistantRule` allows future extension (System/Tool) without touching core behaviors.
* **Payload creep** → events kept minimal (preview only); integration can rehydrate details via read model if needed.

---

## Glossary (Ubiquitous Language)

* **Conversation:** A single user’s running dialogue with the Assistant.
* **Message:** An immutable utterance within a Conversation, from `User` or `Assistant`.
* **Start:** The act of creating a Conversation with the first User message.
* **Complete:** Marking a Conversation closed to further messages.
* **Default Title:** An empty/placeholder title at start; flagged by `IsDefaultTitle=true`.
