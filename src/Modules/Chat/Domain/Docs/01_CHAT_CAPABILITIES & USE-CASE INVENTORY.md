# 01\_CHAT\_CAPABILITIES & USE-CASE INVENTORY (MVP) — LOCKED

## Purpose & scope

Single-owner, ChatGPT-style conversations. One bounded context (**Chat**). One aggregate (**Conversation**) with child entity (**Message**). Pure domain view (no API, no persistence, no infra).

---

## Actors & boundaries

* **Actor**: `Owner` (the product’s single human user/tenant).
* **Bounded context**: `Chat`.
* **Aggregate**: `Conversation` (owns `Message` list).
* **Future-proofing**: `OwnerId` is kept even in single-tenant MVP.

---

## Capability map (in scope)

1. Start Conversation
2. Append User Message
3. Append Assistant Message *(turn-taking enforced: **no consecutive assistant messages**)*
4. Update Conversation Title *(only while Active)*
5. Complete Conversation *(requires ≥1 message)*
6. List Owner’s Conversations *(via domain specifications)*
7. Get Conversation Transcript *(messages ordered by Sequence)*

### Out of scope (MVP)

* Message edit/delete, reactions, pins, threads, attachments, tools, mentions, formatting
* Multi-user/membership/permissions beyond `Owner`
* Archiving/deleting conversations
* Moderation, rate-limits, duplicate detection
* System/Tool roles (excluded)
* Message metadata (postponed)
* Context/token window building (application concern)

---

## Domain state model

**Conversation.Status** ∈ { `Active`, `Completed` }

* **Active → Completed**: allowed if `MessageCount ≥ 1`; set `CompletedAt`.
* **Completed →** *(no transitions in MVP)*.

---

## Domain invariants & limits (baseline)

* **Title**: required on aggregate (but **default if omitted** at start: `"New chat {shortId}"`, where `shortId` = first 8 of `ConversationId`), length **1…200** (trimmed).
* **Message.Content**: non-empty, max **100 000** chars (trimmed).
* **Max messages per conversation**: **10 000** (hard cap).
* **Sequence**: strictly consecutive per conversation (1…n), assigned by aggregate.
* **Append allowed only when** `Status = Active`.
* **Turn-taking**: **Assistant** may not post twice in a row; multiple **User** in a row are allowed.
* **Immutability**: Messages immutable after creation (no metadata in MVP).
* **Ownership**: Only `Owner` interacts with their conversation (both write and read access checks at domain level).

---

## Domain events (names & when)

* **ConversationStarted** — after conversation is created (Active, 0 messages).
* **MessageAppended** — after any message (User/Assistant) is appended.
* **ConversationTitleUpdated** — after title successfully changes.
* **ConversationCompleted** — after status switches to Completed.

*(Event payloads & semantics will be fully specified in “02\_EVENT CATALOG & SEMANTICS”.)*

---

## Use cases

### UC-01 Start Conversation

**Pre**: Owner present.
**Flow**:

1. Validate/normalize title; if blank → default `"New chat {shortId}"`.
2. Create `Conversation` (Active, 0 messages, OwnerId, Title).
3. Raise `ConversationStarted`.
   **Post**: Conversation Active, MessageCount=0.

---

### UC-02 Append User Message

**Pre**: Conversation Active; caller is Owner.
**Flow**:

1. Validate ownership & state Active.
2. Validate content.
3. Compute `Sequence = MessageCount + 1`; create and append User message.
4. Raise `MessageAppended`.
   **Post**: Message appended, sequence contiguous.

---

### UC-03 Append Assistant Message

**Pre**: Conversation Active.
**Flow**:

1. Validate state Active.
2. Enforce turn-taking: **last message must not be Assistant**.
3. Validate content.
4. Compute next sequence; append Assistant message.
5. Raise `MessageAppended`.
   **Err**: Violates turn-taking → fail.

---

### UC-04 Update Conversation Title

**Pre**: Owner; **Conversation Active** (rename after completion is **disallowed** in MVP).
**Flow**:

1. Validate ownership & new title (1…200).
2. Apply title; raise `ConversationTitleUpdated`.
   **Post**: Title updated.

---

### UC-05 Complete Conversation

**Pre**: Conversation Active; **MessageCount ≥ 1**.
**Flow**:

1. Validate state Active and count ≥1.
2. Set `Completed`, `CompletedAt`.
3. Raise `ConversationCompleted`.
   **Post**: Appends are forbidden.

---

### UC-06 List Owner’s Conversations

**Pre**: Owner.
**Flow**: Apply domain specifications (e.g., by Owner, by Status, recent).
**Post**: Collection satisfies predicates. *(No side effects.)*

---

### UC-07 Get Conversation Transcript

**Pre**: Owner; conversation exists.
**Flow**: Return messages in ascending `Sequence (1..n)`.
**Post**: Read-only; no side effects.

---

## Dependencies & ordering

* Must **Start** before **Append** or **Complete**.
* **Complete** is terminal for appending.
* **Update Title** only while **Active**.
* Queries depend on existence + ownership; no state change.

---

## Alignment check (MVP goals)

* Minimal surface, single aggregate: ✅
* ChatGPT-like linear dialog: ✅
* Turn-taking guard only for Assistant: ✅
* No infra leakage, clean events for app/outbox: ✅
* Future-proofing via OwnerId & events: ✅

---

## Ubiquitous language (selected)

* **Conversation**: A session of ordered messages between Owner and Assistant.
* **Message**: An immutable utterance with a `Role` and `Sequence`.
* **Owner**: The human user/tenant.
* **Assistant**: The AI agent’s voice in the conversation.
* **Transcript**: The ordered set of Messages (1..n).

---

## Locked decisions (summary)

* **No consecutive Assistant messages** (turn-taking).
* **Default title**: `"New chat {shortId}"` if omitted/blank.
* **Complete requires ≥1 message**.
* **No metadata in MVP** (messages immutable).
* **Roles**: only `User`, `Assistant`.
* **Limits**: Title ≤200, Message ≤100k, Max messages ≤10k.
* **Rename only while Active**.

---