# Start Conversation Command (CMD-001)

## Overview
Creates a new conversation owned by the current authenticated user.

## API Mapping
- **Route**: `POST /api/v1/chat/conversations`
- **Request**: `{ "title": "optional or null" }`
- **Response**: `200 { "conversationId": "<guid>" }`

## Domain Rules
- User must be authenticated
- Title is optional (null or empty creates default title)
- Title max length: 200 characters after trim
- Domain aggregate: `Conversation.Start(ownerId, titleOrNull, clock)`

## Files
- `StartConversationCommand.cs` - Command DTO
- `StartConversationHandler.cs` - Business logic orchestration
- `StartConversationValidator.cs` - Input validation (title length)
- `StartConversationResponse.cs` - Response DTO

## Dependencies
- `IConversationRepository` - Aggregate persistence
- `ICurrentUserService` - Authentication context
- `IClock` - Time abstraction
- `IWriteUnitOfWork` - Transaction management

## Error Codes
- `CHAT.AUTH.UNAUTHENTICATED` - No authenticated user
- `CHAT.CONVERSATION.TITLE.TOO_LONG` - Title exceeds 200 chars
- Domain validation errors propagated as-is