# Persistence Guide

**Comprehensive guide to Entity Framework Core, repositories, migrations, and concurrency in Axon.**

---

## Overview

**Stack**: EF Core 9 + PostgreSQL 16 + Modular DbContexts

**Key Patterns**:
- Read/Write DbContext separation (CQRS)
- Owned entities for aggregates (DDD)
- Optimistic concurrency with `xmin` (PostgreSQL system column)
- Repository pattern with generic base implementations

---

## DbContext Architecture

### Read/Write Separation

```
Module (e.g., Identity)
├── IdentityWriteDbContext  # Commands (CUD operations)
│   └── Aggregates only (AxonPrincipal, Wallet)
└── IdentityReadDbContext   # Queries (optimized reads)
    └── Projections, DTOs, denormalized views
```

**Benefits**:
- Separate optimization strategies (write vs read)
- Independent scaling potential
- Clear CQRS boundary enforcement

### Write DbContext Example

```csharp
public sealed class IdentityWriteDbContext
    : WriteDbContextBase<IdentityModule>, IIdentityWriteDbContext
{
    public IdentityWriteDbContext(
        DbContextOptions<IdentityWriteDbContext> options,
        ILogger<IdentityWriteDbContext>? logger = null)
        : base(options, logger)
    {
    }

    public override string ModuleName => "identity";

    // Aggregates only
    public DbSet<AxonPrincipal> Principals => Set<AxonPrincipal>();
    public DbSet<Wallet> Wallets => Set<Wallet>();

    // Owned entities (accessed through aggregate root)
    public DbSet<IdentityCredential> Credentials => Set<IdentityCredential>();
    public DbSet<WalletOwnership> WalletOwnerships => Set<WalletOwnership>();
}
```

**Key Points**:
- Inherits from `WriteDbContextBase<TModule>` (BuildingBlocks)
- ModuleName used for database schema (`identity`)
- Owned entities exposed for rare direct queries (prefer aggregate root access)

---

## Owned Entity Pattern (EF Core OwnsMany)

### What is an Owned Entity?

**Owned entities** belong to an aggregate root and share its lifecycle. They cannot exist independently.

**Example**: `AxonPrincipal` (aggregate) owns `IdentityCredential`, `WalletOwnership`, `PrincipalChainDefault`

### Configuration

```csharp
public class AxonPrincipalConfiguration : IEntityTypeConfiguration<AxonPrincipal>
{
    public void Configure(EntityTypeBuilder<AxonPrincipal> builder)
    {
        builder.ToTable("Principal", "identity");
        builder.HasKey(p => p.Id);

        // Optimistic concurrency with PostgreSQL xmin
        builder.Property(p => p.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion();

        // Owned collection: Credentials
        builder.OwnsMany(p => p.Credentials, credentials =>
        {
            credentials.ToTable("Credential", "identity");
            credentials.WithOwner().HasForeignKey(c => c.PrincipalId);

            // Composite key: (PrincipalId, Id)
            credentials.HasKey(c => new { c.PrincipalId, c.Id });

            // ValueGeneratedNever = Application provides IDs (not DB)
            credentials.Property(c => c.Id)
                .HasConversion(id => id.Value, value => new IdentityCredentialId(value))
                .HasColumnType("uuid")
                .ValueGeneratedNever();

            credentials.Property(c => c.PrincipalId)
                .HasConversion(id => id.Value, value => new AxonUserId(value))
                .HasColumnType("uuid")
                .IsRequired();

            // No concurrency token on owned entities - protected by aggregate root
        });

        // Similar configuration for WalletOwnerships, PrincipalChainDefaults...
    }
}
```

### Owned Entity Rules

**✅ DO**:
- Use composite keys `(AggregateId, OwnedEntityId)`
- Set `ValueGeneratedNever()` for owned entity IDs (app controls IDs)
- Access owned entities through aggregate root methods
- Single concurrency token on aggregate root only

**❌ DON'T**:
- Add concurrency tokens to owned entities
- Query owned entities independently (breaks encapsulation)
- Use `ValueGeneratedOnAdd()` for owned entity IDs

---

## Concurrency Control

### PostgreSQL xmin System Column

**Strategy**: Optimistic locking with PostgreSQL's built-in `xmin` transaction ID column

**Benefits**:
- No explicit version column needed
- Automatic per-transaction versioning
- Database-level guarantee

### Configuration

```csharp
// Aggregate root
builder.Property(p => p.Version)
    .HasColumnName("xmin")
    .HasColumnType("xid")
    .IsRowVersion();
```

### Concurrency Exception Handling

```csharp
public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
{
    try
    {
        return await base.SaveChangesAsync(ct);
    }
    catch (DbUpdateConcurrencyException ex)
    {
        ThrowConcurrencyException(ex);
        throw; // Unreachable, but satisfies compiler
    }
}

private static void ThrowConcurrencyException(DbUpdateConcurrencyException ex)
{
    var firstEntry = ex.Entries.FirstOrDefault();
    var entityType = firstEntry?.Entity.GetType().Name ?? "Unknown";
    var entityId = GetEntityKey(firstEntry);

    throw new ConcurrencyException(
        $"The {entityType} with key [{entityId}] has been modified by another user.",
        entityType,
        entityId,
        "xmin",
        "xmin");
}
```

### Testing Concurrency

See [Concurrency Testing Guide](../testing/concurrency-testing-guide.md) for comprehensive patterns.

**Quick Example**:
```csharp
[Test]
public async Task UpdateAsync_ConcurrentModifications_ShouldThrowConcurrency()
{
    // Arrange - Create entity
    var principal = TestDataFixtures.CreatePrincipalA();
    await _repository.AddAsync(principal);
    await _context.SaveChangesAsync();

    // Act - Simulate concurrent updates with separate contexts
    using var context1 = await CreateNewContextAsync();
    using var context2 = await CreateNewContextAsync();

    var repo1 = new AxonPrincipalWriteRepository(context1);
    var repo2 = new AxonPrincipalWriteRepository(context2);

    var entity1 = await repo1.GetByIdAsync(principal.Id);
    var entity2 = await repo2.GetByIdAsync(principal.Id);

    entity1!.UpdateRiskTier(RiskTier.Medium);
    entity2!.UpdateRiskTier(RiskTier.High);

    await context1.SaveChangesAsync(); // First save succeeds

    // Assert - Second save fails
    var ex = Assert.ThrowsAsync<DbUpdateConcurrencyException>(
        async () => await context2.SaveChangesAsync());
}
```

---

## Repository Pattern

### Generic Base Repository

```csharp
// Read repository
public interface IReadRepository<TEntity, in TId>
    where TEntity : class, IEntity<TId>
    where TId : StrongId<TId>
{
    Task<TEntity?> GetByIdAsync(TId id, CancellationToken ct = default);
    Task<bool> ExistsAsync(TId id, CancellationToken ct = default);
    Task<List<TEntity>> GetAllAsync(CancellationToken ct = default);
}

// Write repository
public interface IWriteRepository<TEntity, in TId>
    where TEntity : class, IAggregateRoot<TId>
    where TId : StrongId<TId>
{
    Task<TEntity?> GetByIdAsync(TId id, CancellationToken ct = default);
    Task AddAsync(TEntity entity, CancellationToken ct = default);
    Task<TEntity> UpdateAsync(TEntity entity, CancellationToken ct = default);
    Task DeleteAsync(TId id, CancellationToken ct = default);
}
```

### Module-Specific Repository

```csharp
public interface IAxonPrincipalWriteRepository : IWriteRepository<AxonPrincipal, AxonUserId>
{
    // Domain-specific queries
    Task<AxonPrincipal?> GetByCredentialAsync(
        string provider,
        string issuer,
        string subject,
        CancellationToken ct = default);

    Task<bool> HasVerifiedSigningOwnershipAsync(
        WalletId walletId,
        CancellationToken ct = default);
}
```

### Implementation Example

```csharp
public sealed class AxonPrincipalWriteRepository
    : EfWriteRepository<AxonPrincipal, AxonUserId, IdentityWriteDbContext>,
      IAxonPrincipalWriteRepository
{
    public AxonPrincipalWriteRepository(IdentityWriteDbContext context)
        : base(context)
    {
    }

    public async Task<AxonPrincipal?> GetByCredentialAsync(
        string provider, string issuer, string subject, CancellationToken ct)
    {
        return await _context.Principals
            .Where(p => p.Credentials.Any(c =>
                c.Provider == provider &&
                c.Issuer == issuer &&
                c.Subject == subject &&
                !c.IsDeleted))
            .FirstOrDefaultAsync(ct);
    }
}
```

---

## Migrations

### Creating Migrations

```bash
# Identity module
dotnet ef migrations add AddWalletOwnershipTable \
    --project src/Modules/Identity/Infrastructure \
    --context IdentityWriteDbContext \
    --output-dir Persistence/Migrations

# Chat module
dotnet ef migrations add AddMessageTable \
    --project src/Modules/Chat/Infrastructure \
    --context ChatDbContext \
    --output-dir Persistence/Migrations
```

### Applying Migrations

```bash
# Development
dotnet ef database update --project src/Modules/Identity/Infrastructure

# Production (via API startup)
// In Program.cs:
using (var scope = app.Services.CreateScope())
{
    var identityContext = scope.ServiceProvider
        .GetRequiredService<IdentityWriteDbContext>();
    await identityContext.Database.MigrateAsync();
}
```

### Migration Best Practices

**✅ DO**:
- One migration per logical schema change
- Review generated SQL before committing
- Test migrations on local PostgreSQL first
- Use meaningful migration names (`AddWalletOwnership`, not `Migration1`)

**❌ DON'T**:
- Edit applied migrations (create new rollback migration instead)
- Mix data migrations with schema migrations
- Skip migration testing

---

## Query Optimization

### Indexes

**Defined in EF Core Configuration**:
```csharp
// In AxonPrincipalConfiguration
builder.OwnsMany(p => p.Credentials, credentials =>
{
    // Index on lookup keys
    credentials.HasIndex(c => new { c.Provider, c.Issuer, c.Subject })
        .HasDatabaseName("IX_Credential_Provider_Issuer_Subject");

    // Filtered index for active credentials
    credentials.HasIndex(c => c.IsDeleted)
        .HasDatabaseName("IX_Credential_IsDeleted")
        .HasFilter("is_deleted = false");
});
```

### N+1 Prevention

**Problem**:
```csharp
// ❌ N+1 query problem
var principals = await _context.Principals.ToListAsync();
foreach (var p in principals)
{
    // Triggers separate query per principal
    var wallets = p.WalletOwnerships.Count;
}
```

**Solution**:
```csharp
// ✅ Eager loading
var principals = await _context.Principals
    .Include(p => p.WalletOwnerships)
    .Include(p => p.Credentials)
    .ToListAsync();
```

### Projections for Read Models

```csharp
// ✅ Project to DTO (no entity tracking overhead)
var dtos = await _context.Principals
    .Where(p => p.Type == PrincipalType.Human)
    .Select(p => new PrincipalDto
    {
        Id = p.Id,
        RiskTier = p.RiskTier,
        WalletCount = p.WalletOwnerships.Count(w => !w.IsDeleted)
    })
    .ToListAsync();
```

---

## Connection Management

### Connection String

```json
// appsettings.json
{
  "ConnectionStrings": {
    "IdentityDb": "Host=localhost;Port=5432;Database=axon;Username=axon_user;Password=***"
  }
}
```

### DbContext Registration

```csharp
services.AddDbContext<IdentityWriteDbContext>(options =>
    options.UseNpgsql(
        configuration.GetConnectionString("IdentityDb"),
        npgsqlOptions =>
        {
            npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "identity");
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorCodesToAdd: null);
        }));
```

---

## Common Patterns

### Unit of Work

```csharp
// DbContext IS the Unit of Work
using var context = new IdentityWriteDbContext(options);
var repository = new AxonPrincipalWriteRepository(context);

// Multiple operations in single transaction
var principal = await repository.GetByIdAsync(id);
principal.UpdateRiskTier(RiskTier.High);
principal.LinkWalletOwnership(ownership);

await context.SaveChangesAsync(); // Atomic commit
```

### Detached Entity Updates

```csharp
// For web APIs where entities are serialized/deserialized
public async Task<TEntity> UpdateAsync(TEntity entity)
{
    var entry = _context.Entry(entity);

    if (entry.State == EntityState.Detached)
    {
        _context.Attach(entity); // Attach as Unchanged
        entry.State = EntityState.Modified; // Mark modified (preserves concurrency token)
    }

    return entity;
}
```

---

## Best Practices

### ✅ DO
- Use owned entities for aggregate components
- Single concurrency token on aggregate root
- Project to DTOs for read-only queries
- Eager load related entities to prevent N+1
- Use migrations for schema changes
- Test concurrency with separate DbContext instances

### ❌ DON'T
- Query owned entities independently
- Add concurrency tokens to owned entities
- Use `DbSet.Update()` (marks ALL properties modified)
- Use in-memory database for concurrency tests
- Skip migration testing

---

## Related Documentation

- [Concurrency Testing Guide](../testing/concurrency-testing-guide.md) - Testing optimistic concurrency
- [Repository Pattern](../../guides/patterns/00-QUICK-REFERENCE.md) - CQRS repository patterns
- [Domain Modeling](../../guides/patterns/domain-modeling.md) - Aggregate design
- [Identity Module](../../modules/identity/06-database-schema.md) - Identity schema details

---

**Last Updated**: 2025-09-30
**Maintained By**: Axon Engineering Team