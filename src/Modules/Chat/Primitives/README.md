# Chat Primitives Library

This library contains shared Value Objects (VOs) and primitive types used across the Chat module layers.

## Architecture Pattern: Value Objects for Type Safety

The Chat module uses **Value Objects** to achieve strict typing and eliminate primitive obsession. This provides:

- **Type Safety**: `MessageContent` instead of raw `string`
- **Self-Validation**: VOs validate themselves when created
- **Domain Clarity**: `ConversationId` vs `MessageId` are distinct types
- **Consistency**: Validation rules centralized in one place

## Value Objects

### Core Domain Types

- **`ConversationId`** - Strongly-typed conversation identifier
- **`MessageId`** - Strongly-typed message identifier  
- **`UserId`** - Strongly-typed user identifier
- **`MessageContent`** - Validated message content (1-16,000 characters)
- **`AiResponseId`** - Strongly-typed AI response tracking ID

### Usage Pattern

```csharp
// ❌ Primitive Obsession (before)
public Result<Message> AppendUserMessage(string content, IClock clock)
{
    if (string.IsNullOrWhiteSpace(content)) 
        return Error.Validation("Content required");
    // ... duplicate validation logic everywhere
}

// ✅ Value Objects (after)
public Result<Message> AppendUserMessageToConversation(MessageContent content, IClock clock)
{
    // No validation needed - MessageContent is already valid!
    // Domain method can focus on business logic
}
```

### DTO ↔ VO Conversion

At API boundaries, convert DTOs to VOs with proper error handling:

```csharp
// API Layer: Convert DTO to VO
var contentResult = MessageContent.Create(request.Message);
if (contentResult.IsFailure)
    return Result.Failure(contentResult.Error);

var command = new StartConversationCommand(
    Message: contentResult.Value  // Strongly-typed!
);
```

## Constants

- **`ChatPrimitiveConstants`** - Shared validation limits and constraints

## Benefits Achieved

1. **Eliminated Primitive Obsession** - No more raw strings/GUIDs
2. **Centralized Validation** - Rules defined once in VOs
3. **Compile-Time Safety** - Cannot pass wrong type to methods
4. **Reduced Duplication** - Validation logic not repeated
5. **Clear Domain Intent** - Method signatures express business concepts

## Layer Dependencies

```
API Layer          → Primitives (DTO → VO conversion)
Application Layer  → Primitives (Commands use VOs)
Domain Layer       → Primitives (Aggregates use VOs)
Infrastructure     → Primitives (Persistence mapping)
```

This architecture ensures the domain remains pure while providing practical benefits across all layers.