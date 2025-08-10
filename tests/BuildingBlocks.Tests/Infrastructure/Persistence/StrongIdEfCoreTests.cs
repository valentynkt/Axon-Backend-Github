using BuildingBlocks.Core.Domain.Primitives;
using BuildingBlocks.Infrastructure.Persistence.StrongIds;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Tests.Infrastructure.Persistence;

[TestFixture]
[Category("Unit")]
[Category("Infrastructure")]
[Category("Persistence")]
public sealed class StrongIdEfCoreTests
{
    [Test]
    public void ValueConverter_Should_ConvertToAndFromPrimitive()
    {
        // Arrange
        var converter = new StrongIdValueConverter<TestEntityId, Guid>();
        var strongId = TestEntityId.New();

        // Act
        var primitiveValue = converter.ConvertToProvider(strongId);
        var convertedBack = converter.ConvertFromProvider(primitiveValue);

        // Assert
        primitiveValue.ShouldBe(strongId.Value);
        convertedBack.ShouldBe(strongId);
    }

    [Test]
    public void ValueComparer_Should_CompareByUnderlyingValue()
    {
        // Arrange
        var comparer = new StrongIdValueComparer<TestEntityId, Guid>();
        var guid = Guid.NewGuid();
        var strongId1 = TestEntityId.From(guid);
        var strongId2 = TestEntityId.From(guid);
        var strongId3 = TestEntityId.New();

        // Act & Assert
        comparer.Equals(strongId1, strongId2).ShouldBeTrue();
        comparer.Equals(strongId1, strongId3).ShouldBeFalse();
        comparer.GetHashCode(strongId1).Should().Be(comparer.GetHashCode(strongId2));
        comparer.GetHashCode(strongId1).Should().NotBe(comparer.GetHashCode(strongId3));
    }

    [Test]
    public void ValueComparer_Should_CreateSnapshotCorrectly()
    {
        // Arrange
        var comparer = new StrongIdValueComparer<TestEntityId, Guid>();
        var strongId = TestEntityId.New();

        // Act
        var snapshot = comparer.Snapshot(strongId);

        // Assert
        snapshot.ShouldNotBeSameAs(strongId);
        snapshot.ShouldBe(strongId);
        snapshot.Value.ShouldBe(strongId.Value);
    }

    [Test]
    public async Task EfCore_Should_PersistAndRetrieveStrongIdCorrectly()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        await using var context = new TestDbContext(options);
        var entity = new TestEntity
        {
            Id = TestEntityId.New(),
            Name = "Test Entity"
        };

        // Act - Insert
        context.TestEntities.Add(entity);
        await context.SaveChangesAsync();

        // Act - Retrieve
        var retrievedEntity = await context.TestEntities
            .FirstOrDefaultAsync(e => e.Id == entity.Id);

        // Assert
        retrievedEntity.ShouldNotBeNull();
        retrievedEntity!.Id.ShouldBe(entity.Id);
        retrievedEntity.Id.Value.ShouldBe(entity.Id.Value);
        retrievedEntity.Name.ShouldBe(entity.Name);
    }

    [Test]
    public async Task EfCore_Should_HandleNullableStrongIdCorrectly()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        await using var context = new TestDbContext(options);
        var entity = new TestEntity
        {
            Id = TestEntityId.New(),
            Name = "Test Entity",
            ParentId = null // Nullable StrongId
        };

        // Act - Insert
        context.TestEntities.Add(entity);
        await context.SaveChangesAsync();

        // Act - Retrieve
        var retrievedEntity = await context.TestEntities
            .FirstOrDefaultAsync(e => e.Id == entity.Id);

        // Assert
        retrievedEntity.ShouldNotBeNull();
        retrievedEntity!.ParentId.ShouldBeNull();
    }

    [Test]
    public async Task EfCore_Should_HandleStrongIdInQuery()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        await using var context = new TestDbContext(options);
        var entity1 = new TestEntity { Id = TestEntityId.New(), Name = "Entity 1" };
        var entity2 = new TestEntity { Id = TestEntityId.New(), Name = "Entity 2" };

        context.TestEntities.AddRange(entity1, entity2);
        await context.SaveChangesAsync();

        // Act - Query by StrongId
        var found = await context.TestEntities
            .Where(e => e.Id == entity1.Id)
            .FirstOrDefaultAsync();

        // Assert
        found.ShouldNotBeNull();
        found!.Id.ShouldBe(entity1.Id);
    }
}

// Test entities and StrongIds
public sealed record TestEntityId : StrongId<Guid>
{
    private TestEntityId(Guid value) : base(value) { }

    public static TestEntityId New() => new(Guid.NewGuid());
    public static TestEntityId From(Guid value) => new(value);
}

public sealed class TestEntity
{
    public TestEntityId Id { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public TestEntityId? ParentId { get; set; }
}

public sealed class TestDbContext : DbContext
{
    public DbSet<TestEntity> TestEntities { get; set; } = null!;

    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply StrongId conventions
        modelBuilder.ApplyStrongIdConventions();

        // Configure TestEntity
        modelBuilder.Entity<TestEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100);
        });
    }
}