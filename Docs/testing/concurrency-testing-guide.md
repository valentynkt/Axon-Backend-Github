# Concurrency Testing Guide

## Overview

This guide provides best practices for testing optimistic concurrency control in Entity Framework Core applications. It covers common pitfalls, proper testing patterns, and debugging techniques for concurrency-related issues.

## The Problem: EF Core Identity Map

### Common Mistake
```csharp
// ❌ WRONG: This doesn't test concurrency!
var repository = new Repository(dbContext);

var entity1 = await repository.GetByIdAsync(id);  // First load
var entity2 = await repository.GetByIdAsync(id);  // Returns SAME instance!

// Both variables point to the same object
Assert.True(ReferenceEquals(entity1, entity2)); // This passes!
```

**Why it fails:** EF Core's identity map ensures only one instance of an entity exists per context. When you load the same entity twice from the same DbContext, you get the same object reference.

### Correct Approach
```csharp
// ✅ CORRECT: Use separate DbContext instances
using var context1 = CreateNewContext();
using var context2 = CreateNewContext();

var repo1 = new Repository(context1);
var repo2 = new Repository(context2);

var entity1 = await repo1.GetByIdAsync(id);  // Loaded in context1
var entity2 = await repo2.GetByIdAsync(id);  // Different instance in context2

// Now they're different instances with same data
Assert.False(ReferenceEquals(entity1, entity2)); // Different objects
Assert.Equal(entity1.Id, entity2.Id);            // Same ID
Assert.Equal(entity1.Version, entity2.Version);   // Same version initially
```

## Testing Pattern: ConcurrencyTestBase

We've created a base class that provides proper infrastructure for concurrency testing:

```csharp
public abstract class ConcurrencyTestBase<TContext> : PostgreSqlTestBase
    where TContext : DbContext
{
    // Creates two separate contexts for concurrent access
    protected async Task<(TContext context1, TContext context2)> CreateConcurrentContextsAsync();

    // Simulates concurrent updates and returns detailed results
    protected async Task<ConcurrencyTestResult> SimulateConcurrentUpdatesAsync<TAggregate, TId>(...);

    // Tests with detached entities
    protected async Task<ConcurrencyTestResult> SimulateConcurrentDetachedUpdatesAsync<TAggregate, TId>(...);
}
```

## Critical Test Scenarios (20/80 Rule)

### 1. Basic Concurrent Update
Tests the most common scenario - two users updating the same entity:

```csharp
[Test]
public async Task UpdateAsync_ConcurrentModifications_ShouldThrowConcurrencyException()
{
    // Arrange
    var entity = await CreateAndSaveEntityAsync();

    // Act & Assert
    var result = await SimulateConcurrentUpdatesAsync<Entity, EntityId>(
        entity.Id,
        context => new Repository(context),
        e1 => e1.Update("Change 1"),
        e2 => e2.Update("Change 2")
    );

    AssertOptimisticConcurrencyHandled(result);
}
```

### 2. Detached Entity Updates
Common in web APIs where entities are serialized/deserialized:

```csharp
[Test]
public async Task UpdateAsync_DetachedEntities_ShouldThrowConcurrencyException()
{
    var entity = await CreateAndSaveEntityAsync();

    var result = await SimulateConcurrentDetachedUpdatesAsync<Entity, EntityId>(
        entity.Id,
        context => new Repository(context),
        e1 => e1.Update("Detached 1"),
        e2 => e2.Update("Detached 2")
    );

    AssertOptimisticConcurrencyHandled(result);
}
```

### 3. Mixed Operations
Different types of modifications on the same entity:

```csharp
[Test]
public async Task MixedOperations_ShouldDetectConflicts()
{
    var conversation = await CreateConversationAsync();

    var result = await SimulateConcurrentUpdatesAsync(
        conversation.Id,
        context => new ConversationRepository(context),
        c1 => c1.UpdateTitle("New Title"),        // Admin updates title
        c2 => c2.AppendMessage("User message")    // User sends message
    );

    AssertOptimisticConcurrencyHandled(result);
}
```

### 4. Navigation Property Changes
Testing concurrent modifications to collections:

```csharp
[Test]
public async Task NavigationProperties_ConcurrentChanges_ShouldDetectConflicts()
{
    var principal = await CreatePrincipalWithWalletsAsync();

    var result = await SimulateConcurrentUpdatesAsync(
        principal.Id,
        context => new PrincipalRepository(context),
        p1 => p1.AddWallet(newWallet1),    // Add wallet
        p2 => p2.RemoveWallet(existingId)  // Remove different wallet
    );

    AssertOptimisticConcurrencyHandled(result);
}
```

## Repository Fix Explanation

### The Problem
```csharp
// ❌ WRONG: Marks ALL properties as modified, including Version!
public Task<T> UpdateAsync<T>(T entity)
{
    _dbSet.Update(entity);  // This bypasses concurrency control!
    return Task.FromResult(entity);
}
```

### The Solution
```csharp
// ✅ CORRECT: Respects concurrency tokens
public Task<T> UpdateAsync<T>(T entity)
{
    var entry = _context.Entry(entity);

    if (entry.State == EntityState.Detached)
    {
        _dbSet.Attach(entity);           // Attach as Unchanged
        entry.State = EntityState.Modified;  // Mark modified (preserves Version)
    }
    // For tracked entities, EF Core handles everything

    return Task.FromResult(entity);
}
```

## Common Pitfalls and Solutions

### Pitfall 1: Testing with In-Memory Database
```csharp
// ⚠️ In-memory database doesn't enforce concurrency!
options.UseInMemoryDatabase("TestDb");
```

**Solution:** Use real PostgreSQL with Testcontainers:
```csharp
// ✅ Real database with proper concurrency
var container = new PostgreSqlBuilder()
    .WithImage("postgres:16-alpine")
    .Build();
```

### Pitfall 2: Not Clearing Change Tracker
```csharp
// ❌ Entity might still be tracked
context.ChangeTracker.Clear();
var entity = await repo.GetByIdAsync(id);
```

**Solution:** Use fresh contexts:
```csharp
// ✅ Guaranteed fresh context
using var newContext = CreateNewContext();
var entity = await newContext.Set<Entity>().FindAsync(id);
```

### Pitfall 3: Assuming Version Auto-Increments
```csharp
// ❌ Version only increments when domain events are raised
entity.SomeProperty = "new value";  // No version change!
```

**Solution:** Ensure domain methods raise events:
```csharp
// ✅ Domain method raises event, incrementing version
public Result Update(string value)
{
    SomeProperty = value;
    RaiseDomainEvent(new EntityUpdatedEvent(Id));  // Version++
    return Result.Success();
}
```

## Debugging Concurrency Issues

### 1. Check Entity State
```csharp
var entry = context.Entry(entity);
Console.WriteLine($"State: {entry.State}");
Console.WriteLine($"Current Version: {entry.CurrentValues["Version"]}");
Console.WriteLine($"Original Version: {entry.OriginalValues["Version"]}");
```

### 2. Enable SQL Logging
```csharp
optionsBuilder
    .UseNpgsql(connectionString)
    .LogTo(Console.WriteLine, LogLevel.Information)
    .EnableSensitiveDataLogging();
```

### 3. Inspect Generated SQL
Look for the WHERE clause in UPDATE statements:
```sql
-- ✅ Correct: Includes version in WHERE
UPDATE entities
SET name = @p0, version = @p1
WHERE id = @p2 AND version = @p3;

-- ❌ Wrong: No version check
UPDATE entities
SET name = @p0, version = @p1
WHERE id = @p2;
```

## Best Practices

### 1. Test Coverage Priorities (20/80 Rule)
Focus on scenarios that cover 80% of real-world issues:
- Basic concurrent updates (most common)
- Detached entity updates (web APIs)
- Critical business operations (financial, security)
- High-frequency operations (messages, status updates)

### 2. Test Organization
```
tests/
├── BuildingBlocks/
│   ├── Testing/
│   │   └── ConcurrencyTestBase.cs        # Shared infrastructure
│   └── Infrastructure/
│       └── EfWriteRepositoryConcurrencyTests.cs  # Generic tests
└── Modules/
    ├── Chat/Infrastructure/
    │   └── ConversationConcurrencyTests.cs  # Domain-specific
    └── Identity/Infrastructure/
        ├── AxonPrincipalConcurrencyTests.cs
        └── WalletConcurrencyTests.cs
```

### 3. Test Naming Convention
```csharp
[Test]
public async Task {Method}_{Scenario}_Should{ExpectedBehavior}()
{
    // Example:
    // UpdateAsync_ConcurrentModifications_ShouldThrowConcurrencyException
}
```

### 4. Performance Considerations
- Use connection pooling for test contexts
- Dispose contexts properly to avoid connection leaks
- Consider parallel test execution settings
- Clean database state between tests

## Integration with CI/CD

### GitHub Actions Example
```yaml
test-concurrency:
  runs-on: ubuntu-latest
  services:
    postgres:
      image: postgres:16-alpine
      env:
        POSTGRES_PASSWORD: postgres
      options: >-
        --health-cmd pg_isready
        --health-interval 10s
  steps:
    - uses: actions/checkout@v3
    - name: Run Concurrency Tests
      run: dotnet test --filter "FullyQualifiedName~Concurrency"
```

## Troubleshooting

### Issue: Tests Pass Locally but Fail in CI
**Cause:** Timing differences, connection pool exhaustion
**Solution:** Add retries, increase timeouts, check connection disposal

### Issue: Intermittent Test Failures
**Cause:** Race conditions in test setup
**Solution:** Ensure proper async/await, use SemaphoreSlim for coordination

### Issue: DbUpdateConcurrencyException Not Thrown
**Checklist:**
1. ✅ Using separate DbContext instances?
2. ✅ Version property configured with `.IsConcurrencyToken()`?
3. ✅ Repository uses Attach + Modified, not Update()?
4. ✅ Domain events incrementing Version?
5. ✅ Using real database, not in-memory?

## Summary

Proper concurrency testing requires:
1. **Separate DbContext instances** to simulate different users
2. **Real database** (PostgreSQL) for accurate behavior
3. **Proper repository implementation** that respects concurrency tokens
4. **Focus on critical scenarios** following the 20/80 rule
5. **Good test infrastructure** (ConcurrencyTestBase) for consistency

By following these patterns, you can ensure your application handles concurrent modifications safely and predictably.