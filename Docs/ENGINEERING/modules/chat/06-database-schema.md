# Database Schema

**EF Core configuration, tables, and indexes.**

---

## Overview

**Schema**: `chat` (PostgreSQL)
**Pattern**: EF Core `OwnsMany` for aggregate boundaries
**Concurrency**: PostgreSQL `xmin` (optimistic locking)

---

## Tables

### Conversations (Aggregate Root)

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `Id` | uuid | PK | ConversationId (Vogen) |
| `OwnerId` | uuid | NOT NULL | AxonUserId (Vogen) |
| `Title` | varchar(200) | NULL | Optional title |
| `Status` | varchar | NOT NULL | "Active" or "Completed" |
| `LastAiResponseId` | varchar(100) | NULL | For prompt caching |
| `CreatedAt` | timestamptz | NOT NULL | UTC timestamp |
| `UpdatedAt` | timestamptz | NULL | UTC timestamp |
| `IsDeleted` | bool | NOT NULL, DEFAULT false | Soft delete |
| `xmin` | xid | Row version | Optimistic concurrency |

**Indexes:**
- PK: `Id`
- Suggested: `OwnerId`, `Status`, `CreatedAt` (query performance)

### Messages (Owned Entity)

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `Id` | uuid | PK (composite) | MessageId (Vogen) |
| `ConversationId` | uuid | PK (composite), FK | Parent aggregate |
| `Role` | varchar(50) | NOT NULL | "user" or "assistant" |
| `Content` | text | NOT NULL | Message content (1-32K chars) |
| `Sequence` | int | NOT NULL | 1-based order |
| `AiResponseId` | varchar(100) | NULL | Claude response ID (assistant only) |
| `CreatedAt` | timestamptz | NOT NULL | UTC timestamp |
| `UpdatedAt` | timestamptz | NULL | UTC timestamp |
| `IsDeleted` | bool | NOT NULL, DEFAULT false | Soft delete |

**Composite Key:** `(ConversationId, Id)`
**Foreign Key:** `ConversationId` → `Conversations.Id`

**Indexes:**
- PK: `(ConversationId, Id)`
- `ConversationId` (query performance)
- `(ConversationId, Sequence)` (ordering)

---

## EF Core Configuration

**Source**: `src/Modules/Chat/Infrastructure/Persistence/Configurations/ConversationConfiguration.cs`

### Aggregate Root

```csharp
builder.ToTable("Conversations");
builder.HasKey(c => c.Id);

// Value conversions
builder.Property(c => c.Id).HasConversion(new ConversationId.EfCoreValueConverter());
builder.Property(c => c.OwnerId).HasConversion(new AxonUserId.EfCoreValueConverter());
builder.Property(c => c.Status).HasConversion<string>();
builder.Property(c => c.LastAiResponseId).HasConversion(new AiResponseId.EfCoreValueConverter());

// Concurrency (PostgreSQL xmin)
builder.Property(c => c.Version)
    .HasColumnName("xmin")
    .HasColumnType("xid")
    .IsRowVersion();
```

### Owned Entity

```csharp
builder.OwnsMany<Message>("_messages", messages =>
{
    messages.ToTable("Messages");
    messages.WithOwner().HasForeignKey(m => m.ConversationId);

    // Composite key
    messages.HasKey(m => new { m.ConversationId, m.Id });

    // Value conversions
    messages.Property(m => m.Id).HasConversion(new MessageId.EfCoreValueConverter());
    messages.Property(m => m.Role).HasConversion(
        role => role.Value,
        value => MessageRole.FromString(value).Value!);
    messages.Property(m => m.Content).HasConversion(new MessageContent.EfCoreValueConverter());
    messages.Property(m => m.AiResponseId).HasConversion(new AiResponseId.EfCoreValueConverter());

    // Indexes
    messages.HasIndex(m => m.ConversationId);
    messages.HasIndex(m => new { m.ConversationId, m.Sequence });

    // Access mode
    messages.UsePropertyAccessMode(PropertyAccessMode.Field);
});
```

**Key Points:**
- Messages accessed via private `_messages` field (not DbSet)
- No independent concurrency control (owned by aggregate)
- Soft delete supported but no query filter on owned entities (EF Core limitation)

---

## DbContext

**Source**: `src/Modules/Chat/Infrastructure/Persistence/DbContexts/ChatDbContext.cs`

```csharp
public sealed class ChatDbContext : WriteDbContextBase<ChatModule>, IChatWriteDbContext
{
    public override string ModuleName => "chat";
    public DbSet<Conversation> Conversations => Set<Conversation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ChatDbContext).Assembly);

        // Clear query filters from owned entities (not supported)
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.IsOwned())
                entityType.SetQueryFilter(null);
        }
    }
}
```

**Schema**: `chat` (set by base class)
**Migrations**: `__EFMigrationsHistory` table in `chat` schema

---

## Concurrency Control

**Strategy**: Optimistic locking via PostgreSQL `xmin`

**How it works:**
1. EF Core tracks `xmin` value on load
2. On save, checks if `xmin` changed
3. If changed → `DbUpdateConcurrencyException` → HTTP 409
4. Client refetches and retries

**Owned entities:** No separate concurrency control (managed by aggregate root)

---

## Query Patterns

### Load Conversation with Messages

```csharp
var conversation = await dbContext.Conversations
    .Include(c => c._messages)  // EF Core auto-includes owned entities
    .FirstOrDefaultAsync(c => c.Id == conversationId);
```

### Query Messages (Read Model)

```csharp
// Separate read repository for queries
var messages = await readContext.Messages
    .Where(m => m.ConversationId == conversationId)
    .OrderBy(m => m.Sequence)
    .ToListAsync();
```

**Note:** Write model uses owned entities (no DbSet), read model may use separate projections for performance.

---

## Migrations

**Location**: `src/Modules/Chat/Infrastructure/Migrations/`

**Commands:**
```bash
# Add migration
dotnet ef migrations add MigrationName --project src/Modules/Chat/Infrastructure --context ChatDbContext

# Apply migrations
dotnet ef database update --project src/Modules/Chat/Infrastructure --context ChatDbContext
```

---

## Code References

**Configuration:** `Infrastructure/Persistence/Configurations/ConversationConfiguration.cs`
**DbContext:** `Infrastructure/Persistence/DbContexts/ChatDbContext.cs`
**Repositories:** `Infrastructure/Persistence/Repositories/ConversationRepository.cs`
**Migrations:** `Infrastructure/Migrations/`

---

## Related Docs

- **[01-domain-model.md](01-domain-model.md)** - Domain layer
- **[03-messaging-flows.md](03-messaging-flows.md)** - Message processing
- **[05-api-contracts.md](05-api-contracts.md)** - REST API