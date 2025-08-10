# 04\_VALIDATION & ERROR CATALOG (MVP)

## Scope & intent

Authoritative guide to **what can fail in the Chat domain (MVP)** and **how failures are surfaced**. Pure domain focus:

* No HTTP, no persistence, no bus concerns.
* Stable **error codes** + clear human-readable **messages**.
* Applies to the **Conversation** aggregate and its **Message** entity per our locked MVP contracts.

---

## Error primitive & return semantics

* All domain behaviors return a **Result** (or equivalent) that is either **Success** or **Failure(Error)**.
* A domain **Error** contains:

    * `code` — **stable identifier** (e.g., `CHAT_CONVERSATION_NOT_ACTIVE`)
    * `message` — concise, English default
    * `meta` (optional) — key/value hints (limits, actuals, ids) to help UI/handlers
* **Fail-fast** at the operation level (return the **first** relevant error).
  Exception: internal **invariant validation** may yield multiple errors during diagnostics; MVP methods still return a single error normally.
* Events are emitted **only on success**.

**Error types (domain-level semantics)**

* `Validation` — malformed or out-of-range input/state (caller can fix now).
* `Forbidden` — actor not allowed for this action.
* `Conflict` — action collides with current state but could later succeed after state change.
* `Unexpected` — invariant/consistency issues that should not happen in normal use.

> Mapping to transport (HTTP, etc.) is out of scope; keep codes/type stable and messages short.

---

## Validation regimen (where & when)

1. **Preconditions** (method entry): actor, status, limits, lengths.
2. **Construction** (value objects/entities): title/content trimming & bounds; message built immutable.
3. **Invariants** (aggregate): contiguous sequences; lifecycle consistency; turn-taking rule (no consecutive **assistant**).
4. **Postconditions**: only successful state transitions emit events.

---

## Canonical error codes

**Naming scheme:** `CHAT_<CONTEXT>_<NAME>`
Contexts: `CONVERSATION`, `MESSAGE`, `OWNERSHIP`, `INVARIANT`.

### Conversation (life-cycle & properties)

| Code                                  | Type       | Message                                                | Meta (suggested)      |
| ------------------------------------- | ---------- | ------------------------------------------------------ | --------------------- |
| `CHAT_CONVERSATION_OWNER_REQUIRED`    | Validation | Conversation owner must be specified.                  | —                     |
| `CHAT_CONVERSATION_TITLE_EMPTY`       | Validation | Title cannot be empty.                                 | `{ min:1 }`           |
| `CHAT_CONVERSATION_TITLE_TOO_LONG`    | Validation | Title cannot exceed 200 characters.                    | `{ max:200, actual }` |
| `CHAT_CONVERSATION_NOT_ACTIVE`        | Validation | Conversation must be active to perform this operation. | `{ status }`          |
| `CHAT_CONVERSATION_EMPTY_ON_COMPLETE` | Validation | Cannot complete an empty conversation.                 | `{ messageCount }`    |
| `CHAT_CONVERSATION_TITLE_NO_CHANGE`   | Conflict   | New title must differ from the current title.          | `{ current }`         |

### Ownership & access

| Code                       | Type      | Message                                              | Meta                   |
| -------------------------- | --------- | ---------------------------------------------------- | ---------------------- |
| `CHAT_OWNERSHIP_FORBIDDEN` | Forbidden | Only the conversation owner can perform this action. | `{ ownerId, actorId }` |

### Messages (content, limits, roles, turn-taking)

| Code                                    | Type       | Message                                          | Meta                       |
| --------------------------------------- | ---------- | ------------------------------------------------ | -------------------------- |
| `CHAT_MESSAGE_CONTENT_EMPTY`            | Validation | Message content cannot be empty.                 | `{ min:1 }`                |
| `CHAT_MESSAGE_CONTENT_TOO_LONG`         | Validation | Message content cannot exceed 100000 characters. | `{ max:100000, actual }`   |
| `CHAT_MESSAGE_LIMIT_EXCEEDED`           | Validation | Conversation cannot exceed 10000 messages.       | `{ max:10000, current }`   |
| `CHAT_MESSAGE_ASSISTANT_TURN_VIOLATION` | Validation | Assistant cannot send two messages in a row.     | `{ lastRole:"assistant" }` |
| `CHAT_MESSAGE_ROLE_UNSUPPORTED`         | Validation | Unsupported message role for MVP.                | `{ role }`                 |

### Invariants & internal consistency

| Code                                | Type       | Message                                                   | Meta                      |
| ----------------------------------- | ---------- | --------------------------------------------------------- | ------------------------- |
| `CHAT_INVARIANT_SEQUENCE_VIOLATION` | Unexpected | Message sequence must be contiguous starting at 1.        | `{ expectedNext, found }` |
| `CHAT_INVARIANT_LIFECYCLE_MISMATCH` | Unexpected | Completed conversations must have a completion timestamp. | `{ status, completedAt }` |

> The two `CHAT_INVARIANT_*` codes are primarily for defensive checks/logging; regular flows should prevent them.

---

## Failure reasons by behavior (MVP)

### Start(ownerId, optionalTitle)

* `CHAT_CONVERSATION_OWNER_REQUIRED` — missing/empty owner.
* `CHAT_CONVERSATION_TITLE_TOO_LONG` — provided title > 200.
* *(Note)* Empty/omitted title is **allowed** → default to `"New chat {shortId}"` (no error).

### AppendUserMessage(actorId, content)

* `CHAT_OWNERSHIP_FORBIDDEN` — actor ≠ owner.
* `CHAT_CONVERSATION_NOT_ACTIVE` — not `Active`.
* `CHAT_MESSAGE_CONTENT_EMPTY` — after trim.
* `CHAT_MESSAGE_CONTENT_TOO_LONG` — > 100k.
* `CHAT_MESSAGE_LIMIT_EXCEEDED` — already at 10k.

### AppendAssistantMessage(content)

* `CHAT_CONVERSATION_NOT_ACTIVE`
* `CHAT_MESSAGE_CONTENT_EMPTY`
* `CHAT_MESSAGE_CONTENT_TOO_LONG`
* `CHAT_MESSAGE_LIMIT_EXCEEDED`
* `CHAT_MESSAGE_ASSISTANT_TURN_VIOLATION` — last role was `assistant`.

### UpdateTitle(newTitle)

* `CHAT_CONVERSATION_NOT_ACTIVE`
* `CHAT_CONVERSATION_TITLE_EMPTY` — after trim.
* `CHAT_CONVERSATION_TITLE_TOO_LONG`
* `CHAT_CONVERSATION_TITLE_NO_CHANGE`

### Complete()

* `CHAT_CONVERSATION_NOT_ACTIVE`
* `CHAT_CONVERSATION_EMPTY_ON_COMPLETE` — `MessageCount == 0`

---

## Messages: style & stability

* **Concise**, action-oriented, no stack traces.
* Include **limits** and **actuals** in `meta` rather than the message when possible.
* Message text is English default; **code is the stable contract**.
* Do not leak PII beyond `actorId/ownerId/conversationId` where helpful.

---

## Meta payload conventions (optional but recommended)

Examples the domain may attach to `Error.meta`:

* Content too long: `{ max: 100000, actual: 245331 }`
* Title too long: `{ max: 200, actual: 237 }`
* Message limit: `{ max: 10000, current: 10000 }`
* Turn-taking: `{ lastRole: "assistant" }`
* Forbidden: `{ ownerId: "...", actorId: "..." }`

Meta keys are **lowerCamelCase**, primitives only (numbers/strings/bools).

---

## Consistency rules

* **Trim** inputs before validating lengths.
* Content preview elsewhere uses **max 100** (not part of error; here for awareness).
* Only roles `user` and `assistant` are valid in MVP; everything else returns `CHAT_MESSAGE_ROLE_UNSUPPORTED`.
* Methods **must not** mutate state on failure; no events on failure.

---

## Evolution & versioning

* Adding a new code is backward-compatible.
* **Do not rename existing codes.** If semantics change materially, introduce a new code.
* Messages may be refined; **codes remain stable**.
* Keep limits in a single place (aggregate constants); reflect them in meta.

---

## Quick reference (by code)

`CHAT_CONVERSATION_*`
`CHAT_MESSAGE_*`
`CHAT_OWNERSHIP_*`
`CHAT_INVARIANT_*`

That’s the canonical set for the MVP. If you want, I can generate a one-page “error card” (cheatsheet) or wire these into a small `ChatDomainErrors` helper class later—purely for discoverability, not as a runtime dependency.
