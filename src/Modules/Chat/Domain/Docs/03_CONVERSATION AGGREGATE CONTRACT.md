locked ✅

* `aggregateVersion` stays **out** of domain events (app can attach).
* `contentPreview` max length **100**.
* Keep **`isDefaultTitle`** (bool) in events.

moving on 👇

# 03\_CONVERSATION AGGREGATE CONTRACT (MVP)

## Purpose

Single-source-of-truth for the **domain-layer** contract of the Chat MVP. Defines state, behaviors, pre/post-conditions, and invariants for the **Conversation** aggregate and its **Message** entity. No infrastructure, no APIs—pure domain.

---

## Ubiquitous Language (MVP)

* **Conversation** — a chat transcript owned by one user, composed of messages.
* **Owner** — the user who owns the conversation.
* **Message** — an immutable record of text added by a role.
* **Role** — one of: `user`, `assistant` (MVP only).
* **Sequence** — 1-based, strictly contiguous index per conversation.
* **Active / Completed** — conversation lifecycle states.

---

## Aggregate: Conversation

### Identity

* `ConversationId` (strong id)

### State (authoritative)

* `OwnerId` (strong id) — **immutable**.
* `Title` (string, trimmed, 1–200) — either provided or defaulted.
* `Status` (`Active` | `Completed`) — MVP has no `Archived`.
* `CompletedAt` (UTC, nullable) — set when moved to `Completed`.
* `Messages` (private list of `Message`) — append-only.
* (Optional) `TitleIsDefault` (bool) — **domain may store** for clarity; events always include `isDefaultTitle`.
* Concurrency/versioning is infrastructure; domain assumes optimistic concurrency exists.

### Derived/virtual

* `MessageCount`
* `NextSequence = MessageCount + 1`
* `LastMessageRole` (nullable if none)
* `IsActive = (Status == Active)`

### Invariants (must always hold)

1. **Ownership**: `OwnerId` is set and never changes.
2. **Title**: trimmed length ∈ \[1, 200].
3. **Lifecycle**: `CompletedAt` is set iff `Status == Completed`.
4. **Messages**:

    * `Sequence` is contiguous starting at 1.
    * `MessageCount ≤ 10_000`.
    * **Turn-taking**: **no two consecutive assistant messages**. (Consecutive user messages are allowed.)
5. **Roles**: only `user` and `assistant` exist in MVP.
6. **Content**: length ∈ \[1, 100\_000], trimmed.
7. **Append gating**: only when `Status == Active`.

### Default title rule

* If no non-empty title is provided at start, set:
  `Title = "New chat {shortId}"`, where `shortId = first 8 hex chars of ConversationId`.
* Events include `isDefaultTitle = true` in that case.

---

## Entity: Message

### Identity

* `MessageId` (strong id)

### State

* `ConversationId`
* `Role` (`user` | `assistant`)
* `Content` (string, trimmed, 1–100k)
* `Sequence` (int ≥ 1, unique per conversation, contiguous)
* `CreatedAt` (UTC)

### Behavior

* **Immutable after creation** in MVP (no edits, deletes, metadata).

---

## Behaviors (Aggregate Methods)

> All methods return a domain `Result` (or equivalent) and **emit domain events** only on success (see Event Catalog).

### 1) Start

**Signature**: `Start(ownerId, optionalTitle) -> Conversation`
**Preconditions**

* `ownerId` present.
* If `optionalTitle` provided → length ∈ \[1, 200] after trim.

**State changes**

* Create new `Conversation` with:

    * `OwnerId = ownerId`
    * `Title = provided || defaulted ("New chat {shortId}")`
    * `Status = Active`
    * `Messages = []`

**Events**

* `ConversationStarted(conversationId, ownerId, title, isDefaultTitle, startedAt)`

### 2) AppendUserMessage

**Signature**: `AppendUserMessage(requestingOwnerId, content) -> Message`
**Preconditions**

* `Status == Active`
* `requestingOwnerId == OwnerId`
* `content` length ∈ \[1, 100\_000]
* `MessageCount < 10_000`
  *(no turn-taking restriction for user)*

**State changes**

* Create `Message` with:

    * `Role = user`
    * `Sequence = NextSequence`
    * `Content = trimmed content`
* Append to `Messages`.

**Events**

* `MessageAppended(conversationId, messageId, role, sequence, contentLength, contentPreview<=100, appendedAt)`

### 3) AppendAssistantMessage

**Signature**: `AppendAssistantMessage(content) -> Message`
**Preconditions**

* `Status == Active`
* `content` length ∈ \[1, 100\_000]
* `MessageCount < 10_000`
* **Turn-taking**: `LastMessageRole != assistant`

**State changes**

* Create and append `Message` with `Role = assistant`, `Sequence = NextSequence`.

**Events**

* `MessageAppended(...)` (as above with role `assistant`)

### 4) UpdateTitle

**Signature**: `UpdateTitle(newTitle) -> Unit`
**Preconditions**

* `Status == Active`
* `newTitle` trimmed length ∈ \[1, 200]
* `newTitle != Title`

**State changes**

* `Title = newTitle`
* Optionally set `TitleIsDefault = false`

**Events**

* `ConversationTitleUpdated(conversationId, oldTitle, newTitle, updatedAt)`

### 5) Complete

**Signature**: `Complete() -> Unit`
**Preconditions**

* `Status == Active`
* `MessageCount ≥ 1`

**State changes**

* `Status = Completed`
* `CompletedAt = now`

**Events**

* `ConversationCompleted(conversationId, ownerId, messageCount, completedAt)`

---

## Validation & Rejection (typical failure reasons)

* **Ownership violation** (user tries to append to someone else’s conversation)
* **Conversation not active** (append/rename)
* **Content invalid** (empty or >100k)
* **Message limit reached** (≥ 10k)
* **Assistant turn-taking violation** (assistant twice in a row)
* **Title invalid** (empty or >200)
* **No-op rename** (newTitle equals current)

---

## Event Emission (binding to 02\_EVENT CATALOG)

* Methods above emit exactly the events specified in **02\_EVENT CATALOG & SEMANTICS**.
* Event payload rules apply:

    * **No full content**; use `contentPreview` (≤100) + `contentLength`.
    * `isDefaultTitle` included on `ConversationStarted`.
    * Domain events **do not** carry `aggregateVersion`; application may attach.

---

## Exclusions (explicitly out of MVP)

* No message editing/deleting/metadata
* No system/tool roles
* No participants beyond single owner
* No archiving/reopening
* No attachments, reactions, or tool execution records
* No read receipts, typing, or presence

---

## Open Extension Points (for later)

* Add `Archived` status and `Reopen()`.
* Introduce `System`/`Tool` roles with additional rules.
* Message metadata and rich content types.
* Per-conversation rate limits or anti-abuse rules (beyond basic limits).
* Title auto-generation strategies beyond `{shortId}`.

---

If this looks good, I’ll lock **03\_CONVERSATION AGGREGATE CONTRACT (MVP)** and propose the next foundation doc: **04\_VALIDATION & ERROR CATALOG (MVP)** (canonical error codes/messages and how domain returns them), or if you prefer, **04\_PERSISTENCE MAPPING NOTES (domain-only concerns, e.g., sequence uniqueness & constraints)**.
