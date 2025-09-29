# Chat API Contracts

**REST endpoints, request/response schemas, and validation.**

---

## Endpoints

| Endpoint | Method | Purpose | Auth | Pagination |
|----------|--------|---------|------|------------|
| `/api/v1/chat/turns` | POST | Send message (start/continue) | ✅ | ❌ |
| `/api/v1/conversations` | GET | List conversations | ✅ | ✅ |
| `/api/v1/conversations/{id}/messages` | GET | Get messages | ✅ | ✅ |

**All endpoints**: Require JWT Bearer token, validate conversation ownership (403 if not owner)

---

## 1. POST /api/v1/chat/turns

Universal chat endpoint. `conversationId` null → new conversation, present → append to existing.

### Request

```http
POST /api/v1/chat/turns
Authorization: Bearer {token}
Content-Type: application/json

{
  "conversationId": "guid-or-null",
  "message": "user message text"
}
```

**Validation:**
- `message`: Required, not empty/whitespace, max 32K chars
- `conversationId`: If present, not empty GUID

### Response (200)

```json
{
  "conversationId": "7f3e4d2c-...",
  "userMessageId": "8e4f5d3c-...",
  "assistantMessageId": "9f5g6e4d-...",
  "assistantMessage": "AI response...",
  "timestamp": "2025-01-29T10:30:00Z"
}
```

### Error Responses

| Code | When | Retry? |
|------|------|--------|
| **400** | Validation failed | ❌ |
| **401** | No/invalid auth | ❌ |
| **403** | Not owner | ❌ |
| **404** | Conversation not found | ❌ |
| **409** | Concurrency conflict | ✅ |
| **422** | Business rule violation | ❌ |
| **502** | AI service error | ✅ |
| **504** | AI timeout | ✅ |
| **500** | Internal error | ❌ |

**Common 422 violations:**
- **CHAT010**: Turn-taking (consecutive same-role)
- **CHAT006**: Message limit (100 max)
- **CHAT003**: Conversation not active
- **CHAT008**: Content length (1-32K chars)

---

## 2. GET /api/v1/conversations

List user's conversations with pagination, sorting, and filtering.

### Request

```http
GET /api/v1/conversations?pageNumber=1&pageSize=20&sortBy=UpdatedAt&sortDirection=Desc&titleContains=search
Authorization: Bearer {token}
```

**Query Parameters:**

| Parameter | Type | Default | Range | Description |
|-----------|------|---------|-------|-------------|
| `pageNumber` | int | 1 | 1-∞ | Current page (1-based) |
| `pageSize` | int | 20 | 1-100 | Items per page |
| `sortBy` | string | UpdatedAt | UpdatedAt, CreatedAt, Title | Sort field |
| `sortDirection` | string | Desc | Asc, Desc | Sort direction |
| `titleContains` | string | null | - | Case-insensitive filter |

### Response (200)

```json
{
  "items": [
    {
      "conversationId": "a1b2c3d4-...",
      "title": "Clean Architecture Discussion",
      "createdAtUtc": "2025-01-28T10:00:00Z",
      "updatedAtUtc": "2025-01-29T10:31:00Z",
      "lastAssistantResponseId": "msg_abc123xyz"
    }
  ],
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 42,
  "totalPages": 3,
  "hasPrevious": false,
  "hasNext": true,
  "count": 20,
  "isEmpty": false,
  "firstItemIndex": 1,
  "lastItemIndex": 20
}
```

---

## 3. GET /api/v1/conversations/{id}/messages

Get paginated messages from a conversation.

### Request

```http
GET /api/v1/conversations/{conversationId}/messages?pageNumber=1&pageSize=50&includeDeleted=false
Authorization: Bearer {token}
```

**Query Parameters:**

| Parameter | Type | Default | Range |
|-----------|------|---------|-------|
| `pageNumber` | int | 1 | 1-∞ |
| `pageSize` | int | 50 | 1-100 |
| `includeDeleted` | bool | false | - |

### Response (200)

```json
{
  "items": [
    {
      "messageId": "b2c3d4e5-...",
      "role": "user",
      "content": "What is Clean Architecture?",
      "createdAtUtc": "2025-01-28T10:00:00Z",
      "sequence": 1,
      "aiResponseId": null
    },
    {
      "messageId": "c3d4e5f6-...",
      "role": "assistant",
      "content": "Clean Architecture is...",
      "createdAtUtc": "2025-01-28T10:00:05Z",
      "sequence": 2,
      "aiResponseId": "msg_abc123xyz"
    }
  ],
  "pageNumber": 1,
  "pageSize": 50,
  "totalCount": 4,
  "totalPages": 1,
  "hasPrevious": false,
  "hasNext": false,
  "count": 4,
  "isEmpty": false,
  "firstItemIndex": 1,
  "lastItemIndex": 4
}
```

---

## Authentication

**All endpoints require JWT Bearer token:**

```http
Authorization: Bearer {jwt-token}
```

**Token Claims:**
- `sub`: User ID (AxonUserId)
- `exp`: Expiration timestamp

**Ownership:** Conversations filtered by `OwnerId = AxonUserId`. Accessing another user's conversation → **403 Forbidden**.

---

## Pagination

**Offset-based** (1-indexed):
- `pageNumber`: Current page (default 1)
- `pageSize`: Items per page (default 20, max 100)

**Metadata** includes: `totalCount`, `totalPages`, `hasPrevious`, `hasNext`, `count`, `isEmpty`, `firstItemIndex`, `lastItemIndex`

---

## Error Format

**RFC 7807 Problem Details:**

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.x.x",
  "title": "Error Title",
  "status": 400,
  "detail": "Detailed error description",
  "code": "CHAT_ERROR_CODE"
}
```

**Error Code Prefixes:**
- `CHAT001-019`: Business rules
- `CHAT_VAL_*`: Validation
- `CHAT_CONV_*`: Conversation (404, etc.)
- `CHAT_CONC_*`: Concurrency (409)
- `CHAT_AI_*`: AI processing
- `CHAT_INT_*`: Internal errors

---

## Code References

**Endpoints:**
- `Api/Endpoints/V1/Chat/Commands/ChatTurn/ChatTurnEndpoint.cs`
- `Api/Endpoints/V1/Chat/Queries/GetConversations/GetConversationsEndpoint.cs`
- `Api/Endpoints/V1/Chat/Queries/GetConversationMessages/GetConversationMessagesEndpoint.cs`

**Contracts:**
- `Api/Contracts/V1/Chat/*RequestDto.cs`
- `Api/Contracts/V1/Chat/*ResponseDto.cs`

**Validators:**
- `Api/Endpoints/V1/Chat/Commands/ChatTurn/ChatTurnRequestValidator.cs`
- `Api/Endpoints/V1/Chat/Queries/GetConversations/GetConversationsRequestValidator.cs`

---

## Related Docs

- **[01-domain-model.md](01-domain-model.md)** - Domain layer
- **[03-messaging-flows.md](03-messaging-flows.md)** - Message processing
- **[06-database-schema.md](06-database-schema.md)** - Database