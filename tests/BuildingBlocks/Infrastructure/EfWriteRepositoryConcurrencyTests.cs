using BuildingBlocks.Application;
using BuildingBlocks.Core.Domain.Entities.Abstractions;
using BuildingBlocks.Core.Domain.Entities.Base;
using BuildingBlocks.Core.Domain.Events;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Infrastructure.Persistence.Write;
using BuildingBlocks.Testing;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Shouldly;

namespace BuildingBlocks.Infrastructure.Tests;

/// <summary>
/// Tests for EfWriteRepository concurrency handling.
/// Validates that optimistic concurrency control works correctly with the Version property.
/// </summary>
[TestFixture]
public class EfWriteRepositoryConcurrencyTests : ConcurrencyTestBase<EfWriteRepositoryConcurrencyTests.TestDbContext>
{
    private TestDbContext _setupContext = null!;

    [SetUp]
    public async Task SetUp()
    {
        // Create and setup initial database schema
        _setupContext = CreateContext(CreateContextOptions());
        await _setupContext.Database.EnsureDeletedAsync();
        await _setupContext.Database.EnsureCreatedAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _setupContext.DisposeAsync();
    }

    protected override DbContextOptions<TestDbContext> CreateContextOptions()
    {
        return CreateDbContextOptionsBuilder<TestDbContext>()
            .Options;
    }

    protected override TestDbContext CreateContext(DbContextOptions<TestDbContext> options)
    {
        return new TestDbContext(options);
    }

    protected override async Task<TId> CreateAndSaveTestAggregateAsync<TAggregate, TId>()
    {
        throw new NotImplementedException("Use specific test methods instead");
    }

    #region Core Concurrency Scenarios

    [Test]
    public async Task UpdateAsync_AttachedEntitiesConcurrentModification_ShouldThrowConcurrencyException()
    {
        // Arrange - Create and save a test aggregate
        var aggregate = TestAggregate.Create("Initial Name");
        var repository = new EfWriteRepository<TestAggregate, TestAggregateId>(_setupContext);
        await repository.AddAsync(aggregate);
        await _setupContext.SaveChangesAsync();

        // Act & Assert - Simulate concurrent modifications with attached entities
        var result = await SimulateConcurrentUpdatesAsync<TestAggregate, TestAggregateId>(
            aggregate.Id,
            context => new EfWriteRepository<TestAggregate, TestAggregateId>(context),
            agg1 => agg1.UpdateName("Update from Context 1"),
            agg2 => agg2.UpdateName("Update from Context 2")
        );

        AssertOptimisticConcurrencyHandled(result);
    }

    [Test]
    public async Task UpdateAsync_DetachedEntitiesConcurrentModification_ShouldThrowConcurrencyException()
    {
        // Arrange - Create and save a test aggregate
        var aggregate = TestAggregate.Create("Initial Name");
        var repository = new EfWriteRepository<TestAggregate, TestAggregateId>(_setupContext);
        await repository.AddAsync(aggregate);
        await _setupContext.SaveChangesAsync();

        // Act & Assert - Simulate concurrent modifications with detached entities
        var result = await SimulateConcurrentDetachedUpdatesAsync<TestAggregate, TestAggregateId>(
            aggregate.Id,
            context => new EfWriteRepository<TestAggregate, TestAggregateId>(context),
            agg1 => agg1.UpdateName("Detached Update 1"),
            agg2 => agg2.UpdateName("Detached Update 2")
        );

        AssertOptimisticConcurrencyHandled(result);
    }

    [Test]
    public async Task UpdateAsync_MixedAttachedDetachedConcurrent_ShouldThrowConcurrencyException()
    {
        // Arrange - Create and save a test aggregate
        var aggregate = TestAggregate.Create("Initial Name");
        var repository = new EfWriteRepository<TestAggregate, TestAggregateId>(_setupContext);
        await repository.AddAsync(aggregate);
        await _setupContext.SaveChangesAsync();

        // Load as detached in first context
        TestAggregate detached;
        using (var loadContext = CreateContext(CreateContextOptions()))
        {
            var loadRepo = new EfWriteRepository<TestAggregate, TestAggregateId>(loadContext);
            var loaded = await loadRepo.GetByIdAsync(aggregate.Id);
            loaded.ShouldNotBeNull();
            loadContext.Entry(loaded).State = EntityState.Detached;
            detached = loaded;
        }

        // Create two contexts - one will use detached, one will load attached
        var (context1, context2) = await CreateConcurrentContextsAsync();

        try
        {
            var repo1 = new EfWriteRepository<TestAggregate, TestAggregateId>(context1);
            var repo2 = new EfWriteRepository<TestAggregate, TestAggregateId>(context2);

            // Context2 loads entity (attached)
            var attached = await repo2.GetByIdAsync(aggregate.Id);
            attached.ShouldNotBeNull();

            // Modify both
            detached.UpdateName("Detached Update");
            attached.UpdateName("Attached Update");

            // Update detached first
            await repo1.UpdateAsync(detached);
            await context1.SaveChangesAsync();

            // Try to update attached - should fail
            await repo2.UpdateAsync(attached);

            AssertConcurrencyException(async () =>
                await context2.SaveChangesAsync());
        }
        finally
        {
            await context1.DisposeAsync();
            await context2.DisposeAsync();
        }
    }

    [Test]
    public async Task UpdateAsync_MultiplePropertiesChanged_ShouldDetectConcurrencyOnAnyChange()
    {
        // Arrange
        var aggregate = TestAggregate.Create("Initial", "initial@test.com");
        var repository = new EfWriteRepository<TestAggregate, TestAggregateId>(_setupContext);
        await repository.AddAsync(aggregate);
        await _setupContext.SaveChangesAsync();

        // Act & Assert
        var result = await SimulateConcurrentUpdatesAsync<TestAggregate, TestAggregateId>(
            aggregate.Id,
            context => new EfWriteRepository<TestAggregate, TestAggregateId>(context),
            agg1 => agg1.UpdateName("New Name 1"), // Change name only
            agg2 => agg2.UpdateEmail("newemail@test.com") // Change email only
        );

        AssertOptimisticConcurrencyHandled(result);
    }

    [Test]
    public async Task UpdateAsync_RapidSuccessiveUpdates_ShouldMaintainVersionIntegrity()
    {
        // Arrange
        var aggregate = TestAggregate.Create("Initial");
        var repository = new EfWriteRepository<TestAggregate, TestAggregateId>(_setupContext);
        await repository.AddAsync(aggregate);
        await _setupContext.SaveChangesAsync();

        var initialVersion = aggregate.Version;

        // Act - Perform multiple sequential updates
        using var context = CreateContext(CreateContextOptions());
        var repo = new EfWriteRepository<TestAggregate, TestAggregateId>(context);

        var loaded = await repo.GetByIdAsync(aggregate.Id);
        loaded.ShouldNotBeNull();

        for (int i = 1; i <= 5; i++)
        {
            loaded.UpdateName($"Update {i}");
            await repo.UpdateAsync(loaded);
            await context.SaveChangesAsync();
        }

        // Assert - Version should have incremented appropriately
        loaded.Version.ShouldBeGreaterThan(initialVersion);

        // Now try concurrent update with stale version - should fail
        using var staleContext = CreateContext(CreateContextOptions());
        var staleRepo = new EfWriteRepository<TestAggregate, TestAggregateId>(staleContext);

        // Manually create entity with old version
        var staleAggregate = TestAggregate.CreateWithVersion(
            aggregate.Id,
            "Stale Update",
            initialVersion);

        await staleRepo.UpdateAsync(staleAggregate);

        AssertConcurrencyException(async () =>
            await staleContext.SaveChangesAsync());
    }

    [Test]
    public async Task UpdateRangeAsync_ConcurrentBatchUpdates_ShouldDetectConflicts()
    {
        // Arrange - Create multiple aggregates
        var aggregates = new[]
        {
            TestAggregate.Create("Aggregate 1"),
            TestAggregate.Create("Aggregate 2"),
            TestAggregate.Create("Aggregate 3")
        };

        var repository = new EfWriteRepository<TestAggregate, TestAggregateId>(_setupContext);
        await repository.AddRangeAsync(aggregates);
        await _setupContext.SaveChangesAsync();

        // Create two contexts for concurrent batch updates
        var (context1, context2) = await CreateConcurrentContextsAsync();

        try
        {
            var repo1 = new EfWriteRepository<TestAggregate, TestAggregateId>(context1);
            var repo2 = new EfWriteRepository<TestAggregate, TestAggregateId>(context2);

            // Load all aggregates in both contexts
            var batch1 = new List<TestAggregate>();
            var batch2 = new List<TestAggregate>();

            foreach (var agg in aggregates)
            {
                var loaded1 = await repo1.GetByIdAsync(agg.Id);
                var loaded2 = await repo2.GetByIdAsync(agg.Id);

                loaded1.ShouldNotBeNull();
                loaded2.ShouldNotBeNull();

                batch1.Add(loaded1);
                batch2.Add(loaded2);
            }

            // Modify all in both batches
            foreach (var agg in batch1)
            {
                agg.UpdateName(agg.Name + " - Batch 1");
            }

            foreach (var agg in batch2)
            {
                agg.UpdateName(agg.Name + " - Batch 2");
            }

            // Update and save first batch
            await repo1.UpdateRangeAsync(batch1);
            await context1.SaveChangesAsync();

            // Try to update second batch - should fail
            await repo2.UpdateRangeAsync(batch2);

            AssertConcurrencyException(async () =>
                await context2.SaveChangesAsync());
        }
        finally
        {
            await context1.DisposeAsync();
            await context2.DisposeAsync();
        }
    }

    #endregion

    #region Test Infrastructure

    /// <summary>
    /// Test aggregate for concurrency testing.
    /// </summary>
    public class TestAggregate : AggregateRoot<TestAggregateId>
    {
        public string Name { get; private set; } = string.Empty;
        public string? Email { get; private set; }

        private TestAggregate() { } // EF Core constructor

        public static TestAggregate Create(string name, string? email = null)
        {
            var aggregate = new TestAggregate
            {
                Id = TestAggregateId.New(),
                Name = name,
                Email = email
            };

            aggregate.RaiseDomainEvent(new TestAggregateCreatedEvent(aggregate.Id, name));
            return aggregate;
        }

        public static TestAggregate CreateWithVersion(TestAggregateId id, string name, uint version)
        {
            return new TestAggregate
            {
                Id = id,
                Name = name,
                Version = version
            };
        }

        public Result UpdateName(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
                return Result.Failure(ErrorConstants.Validation("Name cannot be empty"));

            Name = newName;
            RaiseDomainEvent(new TestAggregateUpdatedEvent(Id, Name));
            return Result.Success();
        }

        public Result UpdateEmail(string newEmail)
        {
            Email = newEmail;
            RaiseDomainEvent(new TestAggregateEmailUpdatedEvent(Id, Email));
            return Result.Success();
        }
    }

    /// <summary>
    /// Strong ID for test aggregate.
    /// </summary>
    public readonly struct TestAggregateId : IEquatable<TestAggregateId>
    {
        public Guid Value { get; }

        public TestAggregateId(Guid value) => Value = value;

        public static TestAggregateId New() => new(Guid.NewGuid());
        public static TestAggregateId Empty => new(Guid.Empty);

        public bool Equals(TestAggregateId other) => Value.Equals(other.Value);
        public override bool Equals(object? obj) => obj is TestAggregateId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();

        public static implicit operator Guid(TestAggregateId id) => id.Value;
    }

    /// <summary>
    /// Test domain events.
    /// </summary>
    public sealed record TestAggregateCreatedEvent(TestAggregateId AggregateId, string Name) : DomainEvent();
    public sealed record TestAggregateUpdatedEvent(TestAggregateId AggregateId, string Name) : DomainEvent();
    public sealed record TestAggregateEmailUpdatedEvent(TestAggregateId AggregateId, string? Email) : DomainEvent();

    /// <summary>
    /// Test DbContext with proper configuration.
    /// </summary>
    public class TestDbContext : DbContext
    {
        public DbSet<TestAggregate> TestAggregates => Set<TestAggregate>();

        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure TestAggregate
            modelBuilder.Entity<TestAggregate>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasConversion(
                        id => id.Value,
                        value => new TestAggregateId(value));

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.Email)
                    .HasMaxLength(200);

                // Configure Version for optimistic concurrency
                entity.Property(e => e.Version)
                    .IsConcurrencyToken();

                entity.Ignore(e => e.DomainEvents);
            });
        }
    }

    #endregion
}