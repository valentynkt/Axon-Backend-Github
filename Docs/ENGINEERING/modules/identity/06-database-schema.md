# Identity Database Schema

**EF Core owned entity patterns, concurrency control, and table structures.**

---

## Architecture Pattern: EF Core OwnsMany

The Identity module uses **EF Core owned entities** for a unique aggregate ownership pattern where the `AxonPrincipal` aggregate root owns three entity collections through `OwnsMany` relationships.

### Key Characteristics
- **Composite Keys**: `(PrincipalId, Id)` for all owned entities
- **No Independent DbSet**: Owned entities accessed only through aggregate root
- **Single Concurrency Token**: `xmin` (PostgreSQL) on aggregate root only
- **Cascading Operations**: Owned entities follow aggregate lifecycle
- **Property Access Mode**: Field-based (`PropertyAccessMode.Field`)

---

## Schema Overview

### Tables
```
identity.Principal                    // Aggregate root
identity.Credential                   // Owned by Principal
identity.WalletOwnership              // Owned by Principal  
identity.PrincipalChainDefault        // Owned by Principal
```

All tables in `identity` schema (PostgreSQL).

---

## Table: Principal (Aggregate Root)

**EF Configuration**: `src/Modules/Identity/Infrastructure/Persistence/Configurations/AxonPrincipalConfiguration.cs`

### Columns
```sql
id              uuid PRIMARY KEY
type            varchar(20) NOT NULL        -- 'Human' | 'Service'
risk_tier       varchar(20) NOT NULL        -- 'Low' | 'Medium' | 'High'
created_at      timestamptz NOT NULL
updated_at      timestamptz NOT NULL
xmin            xid (row version)           -- Optimistic concurrency
```

### Indexes
```sql
ix_principal_type            ON (type)
ix_principal_risk_tier       ON (risk_tier)
ix_principal_created_at      ON (created_at)
```

### Concurrency Control
```csharp
builder.Property(p => p.Version)
    .HasColumnName("xmin")
    .HasColumnType("xid")
    .IsRowVersion();
```

**PostgreSQL `xmin`**: System column automatically incremented on each row update, provides transaction-level concurrency without explicit version column.

---

## Table: Credential (Owned Entity)

**Owned By**: `AxonPrincipal.Credentials`

### Columns
```sql
id              uuid NOT NULL
principal_id    uuid NOT NULL REFERENCES Principal(id)
provider        varchar(100) NOT NULL       -- 'dynamic', 'siws', etc.
issuer          varchar(500) NOT NULL       -- JWT issuer URL
subject         varchar(500) NOT NULL       -- Provider user ID
last_seen_at    timestamptz NOT NULL
created_at      timestamptz NOT NULL
updated_at      timestamptz NOT NULL
is_deleted      boolean NOT NULL DEFAULT false
deleted_at      timestamptz
PRIMARY KEY (principal_id, id)
```

### Indexes
```sql
ux_credential_provider UNIQUE (provider, issuer, subject) 
    WHERE is_deleted = false
```

### EF Core Configuration
```csharp
builder.OwnsMany(p => p.Credentials, credentials =>
{
    credentials.ToTable("Credential", "identity");
    credentials.WithOwner().HasForeignKey(c => c.PrincipalId);
    
    // Composite key
    credentials.HasKey(c => new { c.PrincipalId, c.Id });
    
    // ID generation handled by application (ValueGeneratedNever)
    credentials.Property(c => c.Id)
        .HasConversion(id => id.Value, value => new IdentityCredentialId(value))
        .ValueGeneratedNever();
    
    // Unique constraint for provider identity
    credentials.HasIndex(c => new { c.Provider, c.Issuer, c.Subject })
        .IsUnique()
        .HasFilter("is_deleted = false");
});
```

**Key Pattern**: Application generates UUID via `IdentityCredentialId.New()` before insert. `ValueGeneratedNever()` tells EF Core not to treat ID as database-generated.

---

## Table: WalletOwnership (Owned Entity)

**Owned By**: `AxonPrincipal.WalletOwnerships`

### Columns
```sql
id                      uuid NOT NULL
principal_id            uuid NOT NULL REFERENCES Principal(id)
wallet_id               uuid NOT NULL
access_mode             varchar(20) NOT NULL    -- 'Signing' | 'WatchOnly'
status                  varchar(20) NOT NULL    -- 'Pending' | 'Verified' | 'Revoked'
verification_source     varchar(50) NOT NULL    -- 'DynamicAttested' | 'ManualVerified' | 'SiwsVerified'
verified_at             timestamptz
revoked_at              timestamptz
revoke_reason           varchar(500)
created_at              timestamptz NOT NULL
updated_at              timestamptz NOT NULL
is_deleted              boolean NOT NULL DEFAULT false
deleted_at              timestamptz
PRIMARY KEY (principal_id, id)
```

### Indexes
```sql
idx_ownership_wallet_id     ON (wallet_id)

ux_ownership_pair UNIQUE (principal_id, wallet_id) 
    WHERE is_deleted = false

ux_exclusive_signing UNIQUE (wallet_id, access_mode, status) 
    WHERE access_mode = 'Signing' AND status = 'Verified' AND is_deleted = false
```

### Critical Index: `ux_exclusive_signing`

**Purpose**: Enforces wallet signing exclusivity - only ONE principal can have verified+signing access to a wallet.

**Partial Unique Index**: Uses PostgreSQL `WHERE` clause to apply uniqueness only to active verified signing ownerships.

```sql
CREATE UNIQUE INDEX ux_exclusive_signing 
    ON identity.WalletOwnership(wallet_id, access_mode, status) 
    WHERE access_mode = 'Signing' 
      AND status = 'Verified' 
      AND is_deleted = false;
```

**Business Rule**: Watch-only ownerships (`WatchOnly`) can be shared by multiple principals, but signing ownerships (`Signing`) are exclusive when verified.

### EF Core Configuration
```csharp
builder.OwnsMany(p => p.WalletOwnerships, ownerships =>
{
    ownerships.ToTable("WalletOwnership", "identity");
    ownerships.WithOwner().HasForeignKey(o => o.PrincipalId);
    
    // Composite key
    ownerships.HasKey(o => new { o.PrincipalId, o.Id });
    
    // ID generation
    ownerships.Property(o => o.Id)
        .HasConversion(id => id.Value, value => new WalletOwnershipId(value))
        .ValueGeneratedNever();
    
    // Partial unique index for signing exclusivity
    ownerships.HasIndex(o => new { o.WalletId, o.AccessMode, o.Status })
        .HasDatabaseName("ux_exclusive_signing")
        .IsUnique()
        .HasFilter("access_mode = 'Signing' AND status = 'Verified' AND is_deleted = false");
    
    // One ownership per principal-wallet pair
    ownerships.HasIndex(o => new { o.PrincipalId, o.WalletId })
        .IsUnique()
        .HasFilter("is_deleted = false");
});
```

---

## Table: PrincipalChainDefault (Owned Entity)

**Owned By**: `AxonPrincipal.PrincipalChainDefaults`

### Columns
```sql
id              uuid NOT NULL
principal_id    uuid NOT NULL REFERENCES Principal(id)
chain_id        varchar(50) NOT NULL        -- 'solana-mainnet', 'ethereum-mainnet'
wallet_id       uuid NOT NULL
created_at      timestamptz NOT NULL
updated_at      timestamptz NOT NULL
is_deleted      boolean NOT NULL DEFAULT false
deleted_at      timestamptz
PRIMARY KEY (principal_id, id)
```

### Indexes
```sql
ux_chain_default UNIQUE (principal_id, chain_id) 
    WHERE is_deleted = false

idx_default_principal_id    ON (principal_id)
idx_default_wallet_id       ON (wallet_id)
```

### EF Core Configuration
```csharp
builder.OwnsMany(p => p.PrincipalChainDefaults, chainDefaults =>
{
    chainDefaults.ToTable("PrincipalChainDefault", "identity");
    chainDefaults.WithOwner().HasForeignKey(d => d.PrincipalId);
    
    // Composite key
    chainDefaults.HasKey(d => new { d.PrincipalId, d.Id });
    
    // ID generation (Version 7 GUID)
    chainDefaults.Property(d => d.Id)
        .ValueGeneratedNever();  // Application generates Guid.CreateVersion7()
    
    // One default per chain per principal
    chainDefaults.HasIndex(d => new { d.PrincipalId, d.ChainId })
        .IsUnique()
        .HasFilter("is_deleted = false");
});
```

**ChainId Format**: Compound format always used (`solana-mainnet`, `ethereum-sepolia`, etc.)

---

## Concurrency Strategy

### Aggregate Root Only
Only `AxonPrincipal` has concurrency token (`xmin`). Owned entities **do not have row versions** - they're protected by the aggregate's concurrency token.

### Update Pattern
```csharp
// EF Core tracks the aggregate and owned entities together
var principal = await repository.GetByIdAsync(principalId);
principal.LinkWalletOwnership(...);  // Modifies owned collection
await repository.UpdateAsync(principal);  // Single transaction, xmin checked
```

### Conflict Detection
```csharp
try {
    await _dbContext.SaveChangesAsync();
} catch (DbUpdateConcurrencyException) {
    // Aggregate was modified by another transaction
    // Re-fetch and retry
}
```

**Benefit**: Single concurrency check at aggregate boundary, not per-entity.

---

## Soft Delete Pattern

All owned entities support soft delete via `IsDeleted` flag.

```csharp
public bool IsDeleted { get; private set; }
public DateTime? DeletedAt { get; private set; }

public void SoftDelete()
{
    IsDeleted = true;
    DeletedAt = DateTime.UtcNow;
}
```

**Queries**: Filter `WHERE is_deleted = false` in LINQ and indexes.

**Domain Methods**:
```csharp
// Helper methods on AxonPrincipal
GetActiveWalletOwnerships() => _walletOwnerships.Where(wo => !wo.IsDeleted)
GetActivePrincipalChainDefaults() => _principalChainDefaults.Where(pcd => !pcd.IsDeleted)
```

---

## Strong-Typed IDs (Vogen)

All IDs are strong-typed value objects:

```csharp
AxonUserId              // Principal aggregate ID
IdentityCredentialId    // Credential owned entity ID
WalletOwnershipId       // Ownership owned entity ID
WalletId                // Reference to Wallet aggregate
```

**EF Core Conversion**:
```csharp
.Property(e => e.Id)
    .HasConversion(
        id => id.Value,                  // To DB: Guid
        value => new AxonUserId(value))  // From DB: StrongId
    .HasColumnType("uuid");
```

**Benefits**: Compile-time type safety, prevents ID mixing (e.g., can't pass `WalletId` where `AxonUserId` expected).

---

## Migration Patterns

### Creating Owned Entity Tables
```csharp
migrationBuilder.CreateTable(
    name: "Credential",
    schema: "identity",
    columns: table => new {
        id = table.Column<Guid>(nullable: false),
        principal_id = table.Column<Guid>(nullable: false),
        // ... other columns
    },
    constraints: table => {
        table.PrimaryKey("PK_Credential", x => new { x.principal_id, x.id });
        table.ForeignKey(
            name: "FK_Credential_Principal_principal_id",
            column: x => x.principal_id,
            principalSchema: "identity",
            principalTable: "Principal",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade);
    });
```

### Adding Partial Unique Indexes
```csharp
migrationBuilder.CreateIndex(
    name: "ux_exclusive_signing",
    schema: "identity",
    table: "WalletOwnership",
    columns: new[] { "wallet_id", "access_mode", "status" },
    unique: true,
    filter: "access_mode = 'Signing' AND status = 'Verified' AND is_deleted = false");
```

---

## Performance Considerations

### Query Optimization
```csharp
// Include owned entities explicitly for eager loading
var principal = await _dbContext.Principals
    .Include(p => p.Credentials)
    .Include(p => p.WalletOwnerships)
    .Include(p => p.PrincipalChainDefaults)
    .FirstOrDefaultAsync(p => p.Id == principalId);
```

### Batch Operations
`ApplyChainDefaultsBatch()` minimizes database roundtrips by:
1. Single query for all owned entities
2. In-memory dictionary lookups for existing defaults
3. Bulk insert/update via single `SaveChangesAsync()`

### Index Strategy
- **Performance Indexes**: `idx_ownership_wallet_id`, `idx_default_wallet_id` for FK lookups
- **Uniqueness Indexes**: `ux_exclusive_signing`, `ux_ownership_pair`, `ux_chain_default`
- **Partial Indexes**: `WHERE is_deleted = false` for soft delete performance

---

## Code References

**Configuration**: `src/Modules/Identity/Infrastructure/Persistence/Configurations/`
- `AxonPrincipalConfiguration.cs` - Aggregate + owned entities configuration

**Migrations**: `src/Modules/Identity/Infrastructure/Migrations/`
- EF Core migration history

**Repositories**: `src/Modules/Identity/Infrastructure/Persistence/Repositories/`
- `AxonPrincipalWriteRepository.cs` - Aggregate persistence
- `AxonPrincipalReadRepository.cs` - Read model queries

**DbContext**: `src/Modules/Identity/Infrastructure/Persistence/DbContexts/`
- `IdentityWriteDbContext.cs` - Write model context (aggregate roots only)
- `IdentityReadDbContext.cs` - Read model context (projections)