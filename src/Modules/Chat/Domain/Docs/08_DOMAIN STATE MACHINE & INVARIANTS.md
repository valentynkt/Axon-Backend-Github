awesome—locked. next up in the stack, the most useful foundation before epics/stories is a **formal state + invariants spec** for the domain. i’ll draft it now so engineers and QA have a crisp “truth table” of what’s allowed, when, and why.

# 08\_DOMAIN STATE MACHINE & INVARIANTS (MVP)

## 1) Scope & Intent

Domain-only, implementation-agnostic description of:

* lifecycle states of a **Conversation**
* allowed transitions (operations) and their guards
* invariants that must hold at all times
* derived/monotonic fields and sequencing guarantees
* deterministic event emission rules

This is the contract the domain must uphold; application/infra are out of scope.

---

## 2) Core Concepts (recap)

* **Actors/Roles:** `user`, `assistant`. (No `system`/`tool` in MVP.)
* **Turn-taking:** **No consecutive `assistant`** messages. Consecutive `user` messages are allowed.
* **Bounds:** `title` length ∈ \[1..200]; `content` length ∈ \[1..100\_000]; `messageCount` ≤ 10\_000.
* **Preview:** `contentPreview` max 100 chars.

---

## 3) Aggregate: Conversation

### 3.1 States

* **Active** — can accept messages and title updates.
* **Completed** — immutable wrt messages; title updates still allowed.

> *Note:* `Archived` is reserved (not in MVP).

### 3.2 Identity & Versioning

* `conversationId` — strong-typed GUID.
* `messageId` — strong-typed GUID per message.
* `sequence` — 1-based, contiguous integer per message within a conversation.

---

## 4) Operations (Domain Methods)

### 4.1 Start Conversation

* **Inputs:** `ownerId`, `title?`
* **Guards:**

    * `ownerId` ≠ empty → else `CHAT.CONVERSATION.OWNER_REQUIRED`
    * If `title` provided → length ∈ \[1..200] else `CHAT.CONVERSATION.TITLE_INVALID`
* **Effects:**

    * If title empty/omitted → set default `"New chat {shortId}"`, `isDefaultTitle=true`
    * State = `Active`, `messageCount=0`
* **Event:** `ConversationStarted(conversationId, ownerId, title)`

### 4.2 Append Message

* **Inputs:** `role ∈ {user, assistant}`, `content`
* **Guards (in order):**

    1. State must be `Active` → else `CHAT.CONVERSATION.NOT_ACTIVE`
    2. `content` length ∈ \[1..100\_000] after trim → else `CHAT.MESSAGE.CONTENT_INVALID`
    3. **If messageCount=0** then `role` must be `user` → else `CHAT.MESSAGE.FIRST_MUST_BE_USER`
    4. **Turn-taking:** If lastRole=`assistant` and role=`assistant` → `CHAT.MESSAGE.TURN_TAKING_VIOLATION`
    5. `messageCount < 10_000` → else `CHAT.CONVERSATION.MESSAGE_LIMIT_EXCEEDED`
* **Effects:**

    * `sequence = messageCount + 1`
    * Append message; increment `messageCount`
* **Event:**
  `MessageAppended(conversationId, messageId, sequence, role, contentPreview(≤100), contentLength, occurredAt)`

### 4.3 Update Title

* **Inputs:** `newTitle`
* **Guards:**

    * `newTitle` length ∈ \[1..200] (after trim) → else `CHAT.CONVERSATION.TITLE_INVALID`
    * Must be an effective change (case-insensitive, ignore extra spaces) → else `CHAT.CONVERSATION.TITLE_NO_CHANGE`
* **Effects:**

    * `title = normalized(newTitle)`
    * `isDefaultTitle = false`
* **Event:** `ConversationTitleUpdated(conversationId, oldTitle, newTitle)`

### 4.4 Complete Conversation

* **Inputs:** none
* **Guards:**

    * State must be `Active` → else `CHAT.CONVERSATION.NOT_ACTIVE`
    * `messageCount ≥ 1` → else `CHAT.CONVERSATION.EMPTY_CANNOT_COMPLETE`
* **Effects:**

    * State = `Completed`, `completedAt = now()`
* **Event:** `ConversationCompleted(conversationId, ownerId, messageCount)`

---

## 5) State Transition Table

| From \ Operation |          Start |   Append(user) |                              Append(assistant) | UpdateTitle |       Complete |
| ---------------- | -------------: | -------------: | ---------------------------------------------: | ----------: | -------------: |
| **— (no agg)**   | → **Active** ✅ |              — |                                              — |           — |              — |
| **Active**       |              — |   ✅ guards 1–5 | ✅ guards 1–5 (plus first-user rule if count=0) |           ✅ |  ✅ (if ≥1 msg) |
| **Completed**    |              — | ❌ `NOT_ACTIVE` |                                 ❌ `NOT_ACTIVE` |           ✅ | ❌ `NOT_ACTIVE` |

*Turn-taking:* only forbids **assistant→assistant**. `user→user` is allowed.

---

## 6) Invariants (Must Always Hold)

### Identity & Ownership

* `ownerId` is non-empty (`OWNER_REQUIRED`).

### Title

* `title.Length ∈ [1..200]` at all times.
* If `isDefaultTitle == true` → `title` equals default scheme (`"New chat {shortId}"`).

### Message Sequence

* For all `i` in `[0..messageCount-1]`: `messages[i].sequence == i+1` (contiguous, no gaps).

### Roles & Turn-taking

* If `messageCount ≥ 1` → `messages[0].role == user`.
* For any adjacent pair `(i, i+1)`: not `(assistant, assistant)`.

### State-dependent

* If `status == Completed`:

    * `completedAt != null`
    * Append attempts must fail `NOT_ACTIVE`.

### Bounds & Counters

* `messageCount == messages.Count`
* `messageCount ∈ [0..10_000]`
* Each message `content.Length ∈ [1..100_000]`

---

## 7) Derived/Monotonic Data

* **`sequence`**: strictly increasing by 1 per accepted append; never reused.
* **`messageCount`**: equals messages list length; increments on append; never decrements.
* **`contentPreview`**: `content[..min(100, content.Length)]` (no ellipsis in event payload unless specified; MVP uses raw truncate).
* **`isDefaultTitle`**: flips permanently to `false` on first successful title update.

---

## 8) Deterministic Event Emission

For every successful operation exactly one domain event is raised:

* Start → `ConversationStarted`
* Append → `MessageAppended`
* Update Title → `ConversationTitleUpdated`
* Complete → `ConversationCompleted`

**Event purity (MVP):**

* Values come solely from aggregate state & inputs.
* **No** `aggregateVersion` in domain event; app/infra may enrich envelopes later.
* Timestamps: `occurredAt` captured by domain at emit time.

---

## 9) Normalization Rules

* **Title normalization:** trim; collapse only outer spaces; case preserved in storage; equality checks for “no change” are **case-insensitive** and ignore surrounding whitespace.
* **Content normalization:** trim; do not alter internal whitespace (MVP). Validation occurs on the trimmed content.

---

## 10) Error Mapping (guards → codes)

* Ownership/title at start → `CHAT.CONVERSATION.OWNER_REQUIRED`, `CHAT.CONVERSATION.TITLE_INVALID`
* Title update:

    * invalid length → `CHAT.CONVERSATION.TITLE_INVALID`
    * no effective change → `CHAT.CONVERSATION.TITLE_NO_CHANGE`
* Append:

    * not active → `CHAT.CONVERSATION.NOT_ACTIVE`
    * first must be user → `CHAT.MESSAGE.FIRST_MUST_BE_USER`
    * turn-taking violation (assistant→assistant) → `CHAT.MESSAGE.TURN_TAKING_VIOLATION`
    * content invalid (0 or >100k) → `CHAT.MESSAGE.CONTENT_INVALID`
    * limit exceeded (10k) → `CHAT.CONVERSATION.MESSAGE_LIMIT_EXCEEDED`
* Complete:

    * not active → `CHAT.CONVERSATION.NOT_ACTIVE`
    * empty conversation → `CHAT.CONVERSATION.EMPTY_CANNOT_COMPLETE`

(See 04 catalog for canonical messages.)

---

## 11) Edge Cases & Boundaries (explicit)

* **Start with empty/omitted title:** allowed; default title applied; `isDefaultTitle=true`.
* **Append boundaries:** content length exactly 1 and exactly 100\_000 → allowed.
* **Message limit:** when `messageCount=9_999` → append allowed (becomes 10\_000). Next append rejects.
* **Title “no-op”:** `"  My Title  "` considered equal to existing `"my title"` (ignore casing & outer spaces) → reject.
* **Completed:** title updates allowed; message appends prohibited.

---

## 12) Open Clarifications (confirm or adjust)

1. **Event preview formatting:** plain truncate at 100 chars (no ellipsis) — **OK for MVP?**
2. **Title normalization:** we preserve internal whitespace and case in storage — **confirm**.
3. **Clock source for timestamps:** domain uses `UtcNow` equivalent (deterministic in tests via injectable clock later) — **OK for MVP?**

If you’re good with the above defaults, we can lock this and proceed to **09\_EPICS & USER STORIES (MVP)** using these states/guards as acceptance criteria.

---

Perfect—locking the first two, and I agree on introducing a clock abstraction for clean, testable time.

# Final deltas to 08\_DOMAIN STATE MACHINE & INVARIANTS

* **Event preview formatting:** plain truncate to 100 chars (no ellipsis). ✅
* **Title normalization:** preserve internal whitespace and case in storage; trim only for validation/equality checks. ✅
* **Clocking (timestamps):** use a domain-level clock abstraction (no direct `UtcNow`). ✅

## Domain clock design (MVP, simple & testable)

**Interface (concept):** `IClock` with a single member `UtcNow : DateTimeOffset`.

**Default impl:** `SystemClock : IClock` returning `DateTimeOffset.UtcNow`.

**Where used:** anywhere the domain captures time:

* `ConversationStarted.occurredAt`
* `MessageAppended.occurredAt`
* `Conversation.completedAt`
* `ConversationCompleted.occurredAt`

**How injected (cleanest for domain):**

* Domain methods that need time accept an **optional** `IClock clock = null`.
* Inside the method: `var now = (clock ?? SystemClock.Instance).UtcNow;`
* This keeps the domain self-contained (no service locators/DI in entities), but trivially testable by passing a fake clock.

**Guidelines:**

* Never call `DateTime.UtcNow` directly in domain code.
* All event timestamps and date fields come from `IClock`.
* Tests pass `FixedClock(instant)` or `StepClock(...)` for deterministic assertions.
* Application/infra can pass their own clock (or omit to use the default).

If you’re happy, I’ll update the spec text accordingly and we can roll into **09\_EPICS & USER STORIES (MVP)**. Ready to proceed?
