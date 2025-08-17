# Chat Application Layer

This is the Application layer for the Chat module following Clean Architecture, CQRS, DDD, and SOLID principles.

## Folder Structure

- **Abstractions/** - Ports (interfaces) visible to Infrastructure/Web
- **Behaviors/** - Pipeline behaviors (App-level, provider-agnostic)
- **Commands/** - Feature-based command handlers
- **Queries/** - Feature-based query handlers
- **DTOs/** - Application-internal DTOs
- **Services/** - Provider-agnostic implementations
- **Persistence/** - Module marker and specifications
- **Mapping/** - Optional mapper profiles
- **Configuration/** - App-level options
- **DependencyInjection/** - DI registration

## Rules

- No provider or HTTP types in Application layer
- One feature = one folder under Commands/ or Queries/
- Domain rules first: app validators should be thin
- Do not place controllers or EF DbContexts here

TODO: Complete implementation following APP-70 specification