# ADR-004: Strong Typed IDs

**Status**: ✅ Accepted
**Date**: 2025-01-16
**Deciders**: Engineering Team
**Technical Story**: Type Safety for Entity Identifiers

---

## Context and Problem Statement

Using primitive types (Guid, int) for entity IDs creates risk of mixing IDs from different entities, leading to subtle bugs that only appear at runtime.

---

## Decision Outcome

**Chosen**: StrongId<T> pattern using StronglyTypedId source generator.

**Rationale:**
- Compile-time type safety prevents ID confusion
- Zero runtime overhead (source generator)
- Automatic EF Core configuration
- JSON serialization support
- Swagger documentation support

---

## Implementation

```csharp
// Define strong ID
[StronglyTypedId]
public partial struct UserId { }

[StronglyTypedId]
public partial struct ConversationId { }

// Compile-time safety
void ProcessUser(UserId id) { }

UserId userId = UserId.New();
ConversationId convId = ConversationId.New();

ProcessUser(userId);    // ✅ Compiles
ProcessUser(convId);    // ❌ Compile error!
```

---

## Benefits

```csharp
// ❌ Before: Primitive obsession
public async Task<User> GetUserAsync(Guid userId)
{
    // Can accidentally pass conversationId here - compiles fine!
}

// ✅ After: Type safety
public async Task<User> GetUserAsync(UserId userId)
{
    // Cannot pass ConversationId - compile error
}
```

---

## EF Core Integration

```csharp
// Auto-configured by StronglyTypedId generator
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        // UserId automatically maps to Guid in database
    }
}
```

---

## Related Decisions

- [ADR-003: Result Pattern](./003-result-pattern.md)

---

**Last Updated**: 2025-09-29