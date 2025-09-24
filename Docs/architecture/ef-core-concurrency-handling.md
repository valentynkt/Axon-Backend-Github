# EF Core Concurrency Handling - Issue Analysis & Solutions

## Executive Summary

After the recent auth refactoring, we discovered that optimistic concurrency control tests are failing. The root cause is in our `EfWriteRepository.UpdateAsync()` implementation, which uses `DbSet.Update()` in a way that bypasses EF Core's built-in concurrency token checking.

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

## Conclusion

The issue is a simple misuse of EF Core's `Update()` method. The fix is straightforward: use `Attach()` + `EntityState.Modified` for detached entities, which properly respects concurrency tokens. This solution is:

1. **Clean**: Minimal code change, leverages EF Core properly
2. **Correct**: Follows EF Core best practices
3. **Simple**: No custom logic or overengineering

The fix aligns with our architecture principles of using framework features correctly rather than building custom solutions.

## References

- [EF Core Concurrency Tokens](https://docs.microsoft.com/en-us/ef/core/modeling/concurrency)
- [EF Core Change Tracking](https://docs.microsoft.com/en-us/ef/core/change-tracking/)
- [PostgreSQL MVCC](https://www.postgresql.org/docs/current/mvcc.html)