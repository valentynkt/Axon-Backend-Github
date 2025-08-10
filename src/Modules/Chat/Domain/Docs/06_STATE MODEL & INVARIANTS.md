# 06\_STATE MODEL & INVARIANTS (MVP)

This document is the single source of truth for the **domain state**, **lifecycle**, and **non-negotiable invariants** for Chat (MVP). No infrastructure, no API, no outbox—pure domain.

---

## 1) Bounded Context Summary

* **Bounded Context**: Chat
* **Primary Aggregate**: `Conversation`
* **Child Entity**: `Message`
* **Value Objects** (implicit as primitives for MVP): `ConversationId (Guid)`, `MessageId (Guid)`, `OwnerId (Guid)`, `Role ("user" | "assistant")`, `Title (string)`, `Content (string)`

---

## 2) Aggregate: Conversation

### 2.1 Identity & Core Fields

* `conversationId: Guid` — identity.
* `ownerId: Guid` — required; conversation belongs to exactly one owner.
* `title: string` — *1..200* characters.
* `isDefaultTitle: bool` — whether the current title was auto-generated.
* `status: "Active" | "Completed"` — MVP states only.
* `completedAt?: UTC datetime` — set iff `status == Completed`.
* `messages: List<Message>` — ordered by `sequence` (1-based, contiguous).
* **Derived (not necessarily stored)**

    * `messageCount = messages.Count`
    * `lastMessageAt = messages.Last().occurredAt` (if any)

### 2.2 Lifecycle (MVP)

```
   [Active] --(Complete)--> [Completed]
```

* **Start**: Conversation is created in `Active`.
* **AppendMessage**: allowed only in `Active`.
* **UpdateTitle**: allowed in `Active` and `Completed`.
* **Complete**: transitions `Active → Completed` (one-way in MVP).

### 2.3 Allowed Operations & Preconditions

#### A) Start Conversation

* **Intent**: create a new active conversation for `ownerId`.
* **Inputs**: `ownerId`, `title?`, initial user message (handled by subsequent Append).
* **Preconditions**

    * `ownerId` is a valid Guid (non-empty).
    * `title` **may be omitted** → domain sets a safe default (e.g., `"New chat {shortId}"`) and `isDefaultTitle = true`. If provided, `1..200` chars.
* **Postconditions**

    * `status = Active`
    * `messages = []` (first message comes from subsequent `AppendMessage`).
    * Raise `ConversationStarted`.

> Note: MVP **requires ≥ 1 message** to make the conversation useful; we still keep conversation creation and first message append as distinct operations/events to maintain clarity and consistency.

#### B) Append Message

* **Intent**: add a new `Message` to `messages`.
* **Inputs**: `role ∈ {"user","assistant"}`, `content`.
* **Preconditions**

    * `status == Active`
    * `content length ∈ [1..100_000]`
    * `messageCount < 10_000`
    * **Sequence**: will be `messageCount + 1`
    * **Turn-taking**:

        * **No consecutive assistant messages**: if `messageCount > 0` and `last.role == "assistant"`, then `role` **must be** `"user"`.
        * **First message must be "user"**: if `messageCount == 0`, `role == "user"`.
* **Postconditions**

    * Append `Message` with `sequence = messageCount + 1`.
    * Raise `MessageAppended` with `sequence`, `role`, `contentPreview(100)`, `contentLength`.

#### C) Update Title

* **Intent**: change the conversation title.
* **Inputs**: `title`.
* **Preconditions**

    * `title length ∈ [1..200]`
    * Must differ from current title (case-insensitive compare after trim).
* **Postconditions**

    * `title` updated.
    * `isDefaultTitle = (newTitleWasDefaulted ? true : false)` (MVP: updates typically set `false`).
    * Raise `ConversationTitleUpdated`.

#### D) Complete Conversation

* **Intent**: mark as completed (immutable in MVP).
* **Preconditions**

    * `status == Active`
    * `messageCount > 0`
* **Postconditions**

    * `status = Completed`
    * `completedAt = now(UTC)`
    * Raise `ConversationCompleted`.
    * Further `AppendMessage` is disallowed.

---

## 3) Child Entity: Message

### 3.1 Identity & Fields

* `messageId: Guid` — identity.
* `conversationId: Guid` — parent FK; must equal owning conversation.
* `sequence: int` — **1-based**, contiguous within `conversationId`.
* `role: "user" | "assistant"`
* `content: string` — *1..100\_000* characters.
* `occurredAt: UTC datetime` — creation time (for projections/UX).

### 3.2 Invariants (per message)

* `sequence >= 1`
* `content length ∈ [1..100_000]`
* `role ∈ {"user","assistant"}`
* `conversationId` matches parent aggregate.
* **Immutability**: messages are immutable after creation in MVP (no edits, no deletes).

---

## 4) Global Invariants (per Conversation)

1. **Ownership**

    * `ownerId` must be a valid non-empty Guid.
    * A conversation belongs to exactly one owner.

2. **Status & Completion**

    * `status ∈ {"Active","Completed"}`
    * If `status == Completed` ⇒ `completedAt` is set.
    * If `status == Active` ⇒ `completedAt` is null.

3. **Title**

    * `title length ∈ [1..200]`, always non-empty.
    * `isDefaultTitle` reflects whether title was auto-generated.
    * Defaulting allowed; can be replaced later.

4. **Messages & Sequence**

    * `messages` are strictly ordered by `sequence` and **contiguous** with no gaps:
      `messages[i].sequence == i + 1`.
    * `messages[0].role == "user"` (first message is user).
    * **No consecutive assistant messages**:
      For any `i > 0`, if `messages[i-1].role == "assistant"`, then `messages[i].role == "user"`.
    * `messageCount ≤ 10_000`.

5. **Mutability Rules**

    * `AppendMessage` only when `status == Active`.
    * `Complete` only from `Active` and when `messageCount > 0`.
    * `UpdateTitle` always allowed (Active/Completed) within constraints.
    * Messages are immutable (no edit/delete in MVP).

6. **Event Consistency**

    * Each state change that passes preconditions must raise its corresponding domain event exactly once.
    * `MessageAppended.sequence` equals the new message’s sequence.
    * `contentPreview` is a **pure function** of `content` (see below).

---

## 5) Deterministic Functions (for consistency)

* **contentPreview(content: string, max = 100)**

    * Trim leading/trailing whitespace.
    * If `content.Length ≤ max`, return as-is.
    * Otherwise return `content.Substring(0, max)` (no ellipsis in events; UI may add it).

* **defaultTitle(conversationId)**

    * `"New chat {shortId}"`, where `shortId` is the first 8 hex chars of `conversationId` (lowercase).
    * Sets `isDefaultTitle = true`.

* **messageSequence(messages)**

    * `next = messages.Count + 1`.

> These are *domain* rules (deterministic) to keep projections and tests stable.

---

## 6) Rejection Rules (Validation → No State Change, No Event)

* **Start**: reject if `ownerId` invalid; if provided `title` violates length. (If omitted, default.)
* **AppendMessage**: reject if:

    * `status != Active`
    * `role` invalid
    * `content` length outside `[1..100_000]`
    * `messageCount == 10_000`
    * Turn-taking violation (assistant after assistant)
    * First message not `"user"`
* **UpdateTitle**: reject if length invalid or no effective change.
* **Complete**: reject if `status != Active` or `messageCount == 0`.

Errors map to the **04\_VALIDATION & ERROR CATALOG (MVP)** codes/messages.

---

## 7) Event Coupling (informational, domain-only)

* `ConversationStarted` occurs once per conversation at creation.
* `MessageAppended` occurs once per successful append; strictly increases `sequence`.
* `ConversationTitleUpdated` occurs only when title changes.
* `ConversationCompleted` occurs once; terminal in MVP.

> Domain **does not** include aggregate version in event payload; infra may enrich envelopes.
> Domain **does not** include full message content in events (use preview + length).

---

## 8) Edge Cases & Clarifications

* **Empty/whitespace content**: invalid (rejected).
* **Title casing/whitespace differences**: treat as “no change” if trimmed-equal ignoring case.
* **Time**: `occurredAt` is UTC; do **not** rely on time for ordering—use `sequence`.
* **Replay safety**: consumers should use `(conversationId, messageId)` or `(conversationId, sequence)` as idempotency keys for `MessageAppended`.

---

## 9) Minimal State Examples

### New conversation after first message

* `status = Active`
* `messages = [ { sequence: 1, role: "user" } ]`
* `messageCount = 1`
* `lastMessageAt = m1.occurredAt`

### Active with alternating turns (valid)

`user → assistant → user → assistant → …`
Assistant can only follow user.

### Completed conversation

* `status = Completed`
* `completedAt != null`
* `AppendMessage` rejected

---

## 10) What’s intentionally excluded from MVP

* System/Tool roles
* Editing or deleting messages
* Archiving/Reopening
* Message metadata bag
* Multi-ownership/participants
* Cross-conversation operations

---
