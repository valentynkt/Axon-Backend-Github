# Chat Domain — Error Card (MVP)

### Return shape (domain)

Failures are returned as `Error` objects (no exceptions for expected cases):

* `code` — stable identifier (contract)
* `message` — short English default
* `meta` (optional) — `{ key: value }` hints (limits, actuals, ids)

> Events are emitted **only on success**. Methods **do not mutate** on failure.

---

## Quick limits (for meta & validation)

* **Title**: max **200** chars
* **Message content**: max **100000** chars
* **Max messages per conversation**: **10000**
* **Roles**: `user`, `assistant` (MVP only)
* **Turn-taking**: **no two consecutive assistant** messages

---

## Conversation — lifecycle & title

| Code                                  | Type       | Message                                                | Common `meta`         |
| ------------------------------------- | ---------- | ------------------------------------------------------ | --------------------- |
| `CHAT_CONVERSATION_OWNER_REQUIRED`    | Validation | Conversation owner must be specified.                  | —                     |
| `CHAT_CONVERSATION_NOT_ACTIVE`        | Validation | Conversation must be active to perform this operation. | `{ status }`          |
| `CHAT_CONVERSATION_EMPTY_ON_COMPLETE` | Validation | Cannot complete an empty conversation.                 | `{ messageCount }`    |
| `CHAT_CONVERSATION_TITLE_EMPTY`       | Validation | Title cannot be empty.                                 | `{ min:1 }`           |
| `CHAT_CONVERSATION_TITLE_TOO_LONG`    | Validation | Title cannot exceed 200 characters.                    | `{ max:200, actual }` |
| `CHAT_CONVERSATION_TITLE_NO_CHANGE`   | Conflict   | New title must differ from the current title.          | `{ current }`         |

---

## Ownership & access

| Code                       | Type      | Message                                              | Common `meta`          |
| -------------------------- | --------- | ---------------------------------------------------- | ---------------------- |
| `CHAT_OWNERSHIP_FORBIDDEN` | Forbidden | Only the conversation owner can perform this action. | `{ ownerId, actorId }` |

---

## Messages — content, limits, roles, turn-taking

| Code                                    | Type       | Message                                          | Common `meta`              |
| --------------------------------------- | ---------- | ------------------------------------------------ | -------------------------- |
| `CHAT_MESSAGE_CONTENT_EMPTY`            | Validation | Message content cannot be empty.                 | `{ min:1 }`                |
| `CHAT_MESSAGE_CONTENT_TOO_LONG`         | Validation | Message content cannot exceed 100000 characters. | `{ max:100000, actual }`   |
| `CHAT_MESSAGE_LIMIT_EXCEEDED`           | Validation | Conversation cannot exceed 10000 messages.       | `{ max:10000, current }`   |
| `CHAT_MESSAGE_ASSISTANT_TURN_VIOLATION` | Validation | Assistant cannot send two messages in a row.     | `{ lastRole:"assistant" }` |
| `CHAT_MESSAGE_ROLE_UNSUPPORTED`         | Validation | Unsupported message role for MVP.                | `{ role }`                 |

---

## Invariants (defensive consistency)

| Code                                | Type       | Message                                                   | Common `meta`             |
| ----------------------------------- | ---------- | --------------------------------------------------------- | ------------------------- |
| `CHAT_INVARIANT_SEQUENCE_VIOLATION` | Unexpected | Message sequence must be contiguous starting at 1.        | `{ expectedNext, found }` |
| `CHAT_INVARIANT_LIFECYCLE_MISMATCH` | Unexpected | Completed conversations must have a completion timestamp. | `{ status, completedAt }` |

---

## Meta payload conventions

* LowerCamelCase keys; primitives only.
* Examples:

    * Too long content → `{ max:100000, actual:245331 }`
    * Title too long → `{ max:200, actual:237 }`
    * Limit exceeded → `{ max:10000, current:10000 }`
    * Forbidden → `{ ownerId:"...", actorId:"..." }`

---

## Usage cues (at a glance)

* Prefer **Validation** errors for caller-fixable input/state.
* Use **Forbidden** for actor not allowed.
* Use **Conflict** for state collision (e.g., “no change”).
* Reserve **Unexpected** for invariant breaches (shouldn’t happen in normal flows).

> Keep codes stable. Add new codes rather than renaming existing ones.
