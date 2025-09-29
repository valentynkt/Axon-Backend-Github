# EF Core Concurrency Handling - Issue Analysis & Solutions

## Executive Summary

After the recent auth refactoring, we discovered that optimistic concurrency control tests are failing across ALL modules (Chat, Identity). **UPDATE 2025-09-25**: Initial hypothesis about `EfWriteRepository.UpdateAsync()` was partially correct but the issue is more complex and system-wide than originally thought.

## The Problem

### Failing Test
`UpdateAsync_WithConcurrentModifications_ShouldHandleGracefully` expects a `DbUpdateConcurrencyException` when two users try to update the same entity concurrently, but no exception is thrown.

### Root Cause Analysis

1. **Configuration is Correct**:
   - `Version` property exists in `AggregateRoot<TId>` base class
   - Properly configured with `.IsConcurrencyToken()` in EF configuration
   - Incremented correctly on domain events

2. **The Issue**: `EfWriteRepository.UpdateAsync()` implementation:
   ```csharp
   public virtual Task<TAggregate> UpdateAsync(TAggregate aggregate, CancellationToken ct = default)
   {
       var entry = _context.Entry(aggregate);
       if (entry.State == EntityState.Detached)
       {
           _dbSet.Update(aggregate);  // Problem: Marks ALL properties as modified
       }
       else
       {
           _dbSet.Update(aggregate);  // Same problem for tracked entities
       }
       return Task.FromResult(aggregate);
   }
   ```

3. **Why It Fails**:
   - `DbSet.Update()` marks ALL properties as modified, including the `Version` property
   - This tells EF Core to SET Version = @NewValue instead of WHERE Version = @OriginalValue
   - Concurrency checking is effectively disabled

## How EF Core Concurrency Should Work

### Expected Behavior
1. Entity loaded with Version = 1
2. User modifies entity (Version still 1 in memory)
3. On SaveChanges, EF generates: `UPDATE ... SET ... WHERE Id = @Id AND Version = @OriginalVersion`
4. If no rows affected (because Version changed), throws `DbUpdateConcurrencyException`

### Current Behavior
1. Entity loaded with Version = 1
2. User modifies entity
3. `Update()` marks Version as modified
4. EF generates: `UPDATE ... SET Version = @NewVersion ... WHERE Id = @Id`
5. Update always succeeds, no concurrency check

## Solution Options

### Option 1: Minimal Fix (Recommended) ✅
Fix the `UpdateAsync` method to properly handle concurrency tokens:

```csharp
public virtual Task<TAggregate> UpdateAsync(TAggregate aggregate, CancellationToken ct = default)
{
    ArgumentNullException.ThrowIfNull(aggregate);

    var entry = _context.Entry(aggregate);

    if (entry.State == EntityState.Detached)
    {
        // Attach the entity first
        _dbSet.Attach(aggregate);

        // Mark the entire entity as modified, but EF will respect concurrency tokens
        entry.State = EntityState.Modified;
    }
    // If already tracked, no action needed - EF tracks changes automatically

    return Task.FromResult(aggregate);
}
```

**Pros:**
- Simple, minimal change
- Leverages EF Core's built-in concurrency handling
- No need to manually manage Version property
- Works with PostgreSQL's concurrency token approach

**Cons:**
- None significant

### Option 2: Explicit Property Exclusion
Manually exclude the Version property from being marked as modified:

```csharp
public virtual Task<TAggregate> UpdateAsync(TAggregate aggregate, CancellationToken ct = default)
{
    ArgumentNullException.ThrowIfNull(aggregate);

    var entry = _context.Entry(aggregate);

    if (entry.State == EntityState.Detached)
    {
        _dbSet.Attach(aggregate);
        entry.State = EntityState.Modified;

        // Don't mark concurrency token as modified
        var versionProperty = entry.Property("Version");
        if (versionProperty != null)
        {
            versionProperty.IsModified = false;
        }
    }

    return Task.FromResult(aggregate);
}
```

**Pros:**
- Explicit control over Version property
- Clear intent in code

**Cons:**
- More code
- Hardcoded property name
- Unnecessary - EF Core handles this automatically with Option 1

### Option 3: Use Original Values (Not Recommended)
Store and restore original values:

```csharp
// Complex implementation tracking original values
// Not shown due to complexity and lack of benefits
```

**Pros:**
- Full control

**Cons:**
- Complex
- Error-prone
- Reinventing EF Core's built-in functionality

## Recommended Implementation

### 1. Fix the Repository
Update `/Users/valentynkit/Repos/Axon-Backend/src/BuildingBlocks/Infrastructure/Persistence/Write/EfWriteRepository.cs`:

```csharp
public virtual Task<TAggregate> UpdateAsync(TAggregate aggregate, CancellationToken ct = default)
{
    ArgumentNullException.ThrowIfNull(aggregate);

    var entry = _context.Entry(aggregate);

    // Only take action for detached entities
    if (entry.State == EntityState.Detached)
    {
        // Attach marks the entity as Unchanged
        _dbSet.Attach(aggregate);

        // Then mark as Modified - EF Core will respect concurrency tokens
        entry.State = EntityState.Modified;
    }
    // For already tracked entities, EF Core handles everything automatically

    return Task.FromResult(aggregate);
}
```

### 2. Verify Test Behavior
The test should work as-is after the fix:
- Two instances of the same entity are loaded
- First update succeeds, incrementing Version in DB
- Second update fails with `DbUpdateConcurrencyException` because Version mismatch

### 3. Additional Considerations

#### PostgreSQL Specifics
- PostgreSQL doesn't have `rowversion` like SQL Server
- Using `uint Version` with `.IsConcurrencyToken()` is correct
- No need for `xmin` system column unless we want automatic versioning

#### Domain Event Handling
- Current implementation increments Version on `RaiseDomainEvent()`
- This is correct but only affects in-memory state
- Actual DB version check happens during SaveChanges

## Testing Strategy

### Unit Tests Required
1. ✅ Concurrent modification detection (currently failing, will pass after fix)
2. Normal update scenarios (should continue to work)
3. Aggregate with child entities (navigation properties)
4. Multiple aggregates in single transaction

### Integration Tests
1. Real PostgreSQL concurrency scenarios
2. High-concurrency stress tests
3. Transaction rollback scenarios

## Migration Path

1. **Phase 1**: Apply the fix to `EfWriteRepository`
2. **Phase 2**: Run all existing tests to ensure no regression
3. **Phase 3**: Add additional concurrency tests if needed
4. **Phase 4**: Monitor production for any issues

## Best Practices Going Forward

### DO:
- ✅ Use `Attach()` + `EntityState.Modified` for detached entities
- ✅ Let EF Core handle concurrency tokens automatically
- ✅ Trust EF Core's change tracking for attached entities
- ✅ Keep Version property configuration simple with `.IsConcurrencyToken()`

### DON'T:
- ❌ Use `Update()` for entities with concurrency tokens
- ❌ Manually modify the Version property value
- ❌ Try to outsmart EF Core's concurrency mechanism
- ❌ Mix different concurrency strategies

## Performance Impact

The recommended fix has minimal performance impact:
- `Attach()` is lightweight - just adds to change tracker
- Setting `EntityState.Modified` is a simple flag operation
- No additional database queries
- Same SQL generated for the UPDATE statement

## Security Considerations

Proper concurrency control prevents:
- Lost updates in multi-user scenarios
- Race conditions in financial operations
- Data corruption from parallel modifications
- Inconsistent aggregate states

## Update: Deep Investigation Results (2025-09-25)

### Investigation Conducted
After implementing the documented fix, all concurrency tests were still failing. A comprehensive investigation was conducted to identify the root cause.

### Findings

#### ✅ What Was Successfully Fixed
1. **EfWriteRepository Implementation**: Applied the documented fix correctly
   ```csharp
   // Fixed implementation
   public virtual Task<TAggregate> UpdateAsync(TAggregate aggregate, CancellationToken ct = default)
   {
       ArgumentNullException.ThrowIfNull(aggregate);
       var entry = _context.Entry(aggregate);

       if (entry.State == EntityState.Detached)
       {
           // For detached entities: attach first, then mark as modified
           _dbSet.Attach(aggregate);
           entry.State = EntityState.Modified;
       }
       // For tracked entities, EF Core handles automatically

       return Task.FromResult(aggregate);
   }
   ```

2. **Removed Problematic Code**: Eliminated `_dbSet.Update()` calls that were marking Version as modified
3. **Removed DetectChanges()**: This was interfering with concurrency token handling

#### ❌ What's Still Failing

**System-Wide Issue Discovered**:
- **Chat Module**: 8 out of 57 tests failing (all concurrency tests)
- **Identity Module**: 13 out of 13 concurrency tests failing
- **Pattern**: All failures show `result.SecondUpdateFailed should be True but was False`

**Failing Test Categories**:
- Concurrent title updates
- Concurrent message appending
- Concurrent status changes
- Detached entity concurrent updates

### Root Cause Analysis

#### 1. **Type Mismatch Discovered**
```csharp
// AggregateRoot.cs
public uint Version { get; protected set; }  // uint (4 bytes)

// Migration
Version = table.Column<long>(type: "bigint", nullable: false)  // long (8 bytes)
```

This type mismatch may be causing EF Core to not properly handle the concurrency token.

#### 2. **Configuration Analysis**
```csharp
// ConversationConfiguration.cs - CORRECT
builder.Property(c => c.Version)
    .IsConcurrencyToken();  ✅

// Domain Events - CORRECT
protected void RaiseDomainEvent(IDomainEvent @event)
{
    _domainEvents.Add(@event);
    Version++;  ✅ Increments correctly
}
```

#### 3. **Test Infrastructure Analysis**
- Tests use real PostgreSQL via Testcontainers ✅
- Separate DbContext instances created properly ✅
- ConcurrencyTestBase implementation looks correct ✅

### Investigation Steps Taken

1. **Repository Fix Applied**: Implemented the documented solution
2. **DetectChanges Removal**: Removed interfering change detection
3. **Type Analysis**: Discovered uint → bigint mismatch
4. **Cross-Module Testing**: Confirmed issue affects all modules
5. **SQL Generation**: Attempted to analyze actual SQL (requires deeper investigation)
6. **Test Pattern Analysis**: Verified test infrastructure is correct

### Likely Root Causes

1. **Primary Suspect - Type Mismatch**: EF Core may not be generating proper WHERE clauses for uint → bigint mapping
2. **PostgreSQL Configuration**: May need specific PostgreSQL concurrency token configuration
3. **EF Core Version Issue**: .NET 10 preview + EF Core 9 may have concurrency bugs
4. **Migration Issue**: Database schema may not match entity configuration

### Next Steps Required

#### Immediate Actions Needed:
1. **Fix Type Mismatch**:
   ```csharp
   // Option 1: Change AggregateRoot to use long
   public long Version { get; protected set; }

   // Option 2: Explicit conversion in configuration
   builder.Property(c => c.Version)
       .IsConcurrencyToken()
       .HasConversion<long>();
   ```

2. **Enable SQL Logging**: Add detailed EF Core SQL logging to see generated UPDATE statements

3. **Create Minimal Repro**: Build simple test case outside test framework

4. **Consider PostgreSQL-Specific Solutions**:
   ```csharp
   // Use PostgreSQL's xmin system column
   builder.Property<uint>("xmin")
       .HasColumnType("xid")
       .ValueGeneratedOnAddOrUpdate()
       .IsConcurrencyToken();
   ```

#### Alternative Approaches:
1. **Manual Concurrency Check**: Implement explicit version checking in repository
2. **Database Triggers**: Use PostgreSQL triggers for version management
3. **Switch to RowVersion**: Use byte[] rowversion approach (if supported in PostgreSQL)

### Impact Assessment
- **Severity**: HIGH - Concurrency control completely broken
- **Affected Areas**: ALL aggregates across ALL modules
- **Production Risk**: CRITICAL - Lost updates and race conditions possible
- **Urgency**: IMMEDIATE fix required before any production deployment

## Conclusion

~~The issue is a simple misuse of EF Core's `Update()` method~~. **UPDATE**: The issue is more complex than initially thought. While the repository fix was necessary and correct, there's a deeper system-wide problem likely related to:

1. Type mismatches between C# (uint) and PostgreSQL (bigint)
2. EF Core concurrency token handling in PostgreSQL
3. Possible framework version compatibility issues

**Status**: Repository layer fixed, but core concurrency control still broken. Requires additional investigation and likely architectural changes to the Version property handling.

## References

- [EF Core Concurrency Tokens](https://docs.microsoft.com/en-us/ef/core/modeling/concurrency)
- [EF Core Change Tracking](https://docs.microsoft.com/en-us/ef/core/change-tracking/)
- [PostgreSQL MVCC](https://www.postgresql.org/docs/current/mvcc.html)