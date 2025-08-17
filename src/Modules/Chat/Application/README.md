# Chat Application Layer

This is the Application layer for the Chat module following Clean Architecture, CQRS, DDD, and SOLID principles.

## Folder Structure

- **Abstractions/** - Ports (interfaces) visible to Infrastructure/Web
- **Commands/** - Feature-based command handlers
- **Queries/** - Feature-based query handlers
- **DTOs/** - Application-internal DTOs
- **Services/** - Provider-agnostic implementations
- **Validation/** - Custom validation extensions
- **DependencyInjection/** - DI registration

## Features

### Commands
- **StartConversation** - Initialize new conversations
- **AppendUserMessage** - Add user messages with AI responses (2-phase idempotency)
- **UpdateConversationTitle** - Modify conversation titles

### Queries
- **GetConversation** - Retrieve conversation with messages (cached 60s)
- **GetAllConversationIds** - List user's conversations (non-cached)

## Key Patterns

- **Pipeline Behaviors**: Uses BuildingBlocks validation, transaction, and caching behaviors
- **DI Registrations**: Application services now properly registered for handler resolution
- **Idempotency**: 2-phase approach with 30s TTL for message append operations
- **Caching**: Explicit cache policies per query type

## Rules

- No provider or HTTP types in Application layer
- One feature = one folder under Commands/ or Queries/
- Domain rules first: app validators should be thin
- Response DTOs use primitives only (no Domain Value Objects)