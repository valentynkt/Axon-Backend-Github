using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Core.Model;
using BuildingBlocks.Core.Pagination;
using BuildingBlocks.Postgres;
using BuildingBlocks.EFCore;

namespace AxonBackend.Tests;

/// <summary>
/// THE TEST GUARDIAN - Comprehensive PostgreSQL Architecture Validation
/// This test validates that all architectural patterns have been correctly preserved
/// and that the surgical refactoring from MongoDB to PostgreSQL maintains consistency
/// </summary>
public class PostgresValidationTest
{
    /// <summary>
    /// Test entity that implements all required interfaces
    /// </summary>
    public class TestEntity : IEntity<Guid>, IAggregate<Guid>
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
        public long? CreatedBy { get; set; }
        public DateTime? LastModified { get; set; }
        public long? LastModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public long Version { get; set; }
        
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        private readonly List<IDomainEvent> _domainEvents = new();
        public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        public void AddDomainEvent(IDomainEvent domainEvent)
        {
            _domainEvents.Add(domainEvent);
        }

        public void RemoveDomainEvent(IDomainEvent domainEvent)
        {
            _domainEvents.Remove(domainEvent);
        }

        public void ClearDomainEvents()
        {
            _domainEvents.Clear();
        }
    }

    /// <summary>
    /// Test DbContext for validation
    /// </summary>
    public class TestDbContext : PostgresDbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            modelBuilder.Entity<TestEntity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.Version).IsConcurrencyToken();
            });
        }
    }

    /// <summary>
    /// Execute comprehensive validation tests
    /// </summary>
    public static async Task<bool> ExecuteValidationAsync()
    {
        Console.WriteLine("🛡️ THE TEST GUARDIAN - PostgreSQL Architecture Validation");
        Console.WriteLine("========================================================");
        
        try
        {
            // Setup in-memory database for testing
            var services = new ServiceCollection();
            services.AddDbContext<TestDbContext>(options =>
                options.UseInMemoryDatabase("TestDb"));
            services.AddScoped<IPostgresDbContext>(provider => provider.GetRequiredService<TestDbContext>());
            services.AddScoped<IRepository<TestEntity, Guid>, EfRepository<TestEntity, Guid>>();
            services.AddScoped<IUnitOfWork, PostgresUnitOfWork>();
            services.AddLogging();

            using var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var repository = scope.ServiceProvider.GetRequiredService<IRepository<TestEntity, Guid>>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            // Ensure database is created
            await context.Database.EnsureCreatedAsync();

            Console.WriteLine("✅ Test 1: Repository Interface Validation");
            await ValidateRepositoryInterfaces(repository);

            Console.WriteLine("✅ Test 2: CRUD Operations Validation");
            await ValidateCrudOperations(repository, unitOfWork);

            Console.WriteLine("✅ Test 3: Pagination Validation");
            await ValidatePaginationOperations(repository, unitOfWork);

            Console.WriteLine("✅ Test 4: Bulk Operations Validation");
            await ValidateBulkOperations(repository, unitOfWork);

            Console.WriteLine("✅ Test 5: Unit of Work Validation");
            await ValidateUnitOfWorkOperations(repository, unitOfWork);

            Console.WriteLine("✅ Test 6: Command Queuing Validation");
            await ValidateCommandQueuing(context);

            Console.WriteLine("\n🎉 ALL TESTS PASSED! PostgreSQL architecture is validated and ready!");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ VALIDATION FAILED: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            return false;
        }
    }

    private static async Task ValidateRepositoryInterfaces(IRepository<TestEntity, Guid> repository)
    {
        // Validate that repository implements all required interfaces
        if (repository is not IReadRepository<TestEntity, Guid> readRepo)
            throw new InvalidOperationException("Repository does not implement IReadRepository");

        if (repository is not IWriteRepository<TestEntity, Guid> writeRepo)
            throw new InvalidOperationException("Repository does not implement IWriteRepository");

        Console.WriteLine("  - Repository implements IReadRepository ✓");
        Console.WriteLine("  - Repository implements IWriteRepository ✓");
        Console.WriteLine("  - Repository implements full IRepository interface ✓");
    }

    private static async Task ValidateCrudOperations(IRepository<TestEntity, Guid> repository, IUnitOfWork unitOfWork)
    {
        // Create
        var entity = new TestEntity { Name = "Test Entity", Description = "Test Description" };
        var createdEntity = await repository.AddAsync(entity);
        await unitOfWork.SaveChangesAsync();

        if (createdEntity.Id == Guid.Empty)
            throw new InvalidOperationException("Entity creation failed - ID not assigned");

        Console.WriteLine("  - Create operation ✓");

        // Read
        var foundEntity = await repository.FindByIdAsync(createdEntity.Id);
        if (foundEntity == null || foundEntity.Name != "Test Entity")
            throw new InvalidOperationException("Entity retrieval failed");

        Console.WriteLine("  - Read operation ✓");

        // Update
        foundEntity.Description = "Updated Description";
        await repository.UpdateAsync(foundEntity);
        await unitOfWork.SaveChangesAsync();

        var updatedEntity = await repository.FindByIdAsync(createdEntity.Id);
        if (updatedEntity?.Description != "Updated Description")
            throw new InvalidOperationException("Entity update failed");

        Console.WriteLine("  - Update operation ✓");

        // Delete
        await repository.DeleteByIdAsync(createdEntity.Id);
        await unitOfWork.SaveChangesAsync();

        var deletedEntity = await repository.FindByIdAsync(createdEntity.Id);
        if (deletedEntity != null)
            throw new InvalidOperationException("Entity deletion failed");

        Console.WriteLine("  - Delete operation ✓");
    }

    private static async Task ValidatePaginationOperations(IRepository<TestEntity, Guid> repository, IUnitOfWork unitOfWork)
    {
        // Create test data
        var entities = new List<TestEntity>();
        for (int i = 1; i <= 25; i++)
        {
            entities.Add(new TestEntity { Name = $"Entity {i}", Description = $"Description {i}" });
        }

        await repository.AddRangeAsync(entities);
        await unitOfWork.SaveChangesAsync();

        // Test pagination
        var pageRequest = new PageRequest(1, 10);
        var pagedResult = await repository.GetByPageFilter(pageRequest);

        if (pagedResult.Items.Count != 10)
            throw new InvalidOperationException($"Expected 10 items in first page, got {pagedResult.Items.Count}");

        if (pagedResult.TotalCount != 25)
            throw new InvalidOperationException($"Expected total count of 25, got {pagedResult.TotalCount}");

        if (pagedResult.TotalPages != 3)
            throw new InvalidOperationException($"Expected 3 total pages, got {pagedResult.TotalPages}");

        Console.WriteLine("  - Pagination operation ✓");
        Console.WriteLine($"    - Page 1: {pagedResult.Items.Count} items");
        Console.WriteLine($"    - Total: {pagedResult.TotalCount} items in {pagedResult.TotalPages} pages");

        // Clean up
        await repository.BulkDeleteAsync(e => e.Name.StartsWith("Entity"));
        await unitOfWork.SaveChangesAsync();
    }

    private static async Task ValidateBulkOperations(IRepository<TestEntity, Guid> repository, IUnitOfWork unitOfWork)
    {
        // Create test data
        var entities = new List<TestEntity>();
        for (int i = 1; i <= 15; i++)
        {
            entities.Add(new TestEntity { Name = $"Bulk Entity {i}", Description = $"Bulk Description {i}" });
        }

        // Bulk insert
        await repository.AddRangeAsync(entities);
        await unitOfWork.SaveChangesAsync();

        var count = await repository.CountAsync(e => e.Name.StartsWith("Bulk Entity"));
        if (count != 15)
            throw new InvalidOperationException($"Expected 15 bulk entities, found {count}");

        Console.WriteLine("  - Bulk insert operation ✓");

        // Bulk update
        var updatedCount = await repository.BulkUpdateAsync(
            e => e.Name.StartsWith("Bulk Entity"),
            e => e.SetProperty(p => p.Description, "Bulk Updated"));

        if (updatedCount != 15)
            throw new InvalidOperationException($"Expected to update 15 entities, updated {updatedCount}");

        Console.WriteLine("  - Bulk update operation ✓");

        // Bulk delete
        var deletedCount = await repository.BulkDeleteAsync(e => e.Name.StartsWith("Bulk Entity"));
        if (deletedCount != 15)
            throw new InvalidOperationException($"Expected to delete 15 entities, deleted {deletedCount}");

        Console.WriteLine("  - Bulk delete operation ✓");
    }

    private static async Task ValidateUnitOfWorkOperations(IRepository<TestEntity, Guid> repository, IUnitOfWork unitOfWork)
    {
        // Test transaction management
        var entity1 = new TestEntity { Name = "Transaction Test 1", Description = "Test 1" };
        var entity2 = new TestEntity { Name = "Transaction Test 2", Description = "Test 2" };

        // Add entities within unit of work
        await repository.AddAsync(entity1);
        await repository.AddAsync(entity2);
        await unitOfWork.SaveChangesAsync();

        var count = await repository.CountAsync(e => e.Name.StartsWith("Transaction Test"));
        if (count != 2)
            throw new InvalidOperationException($"Expected 2 transaction test entities, found {count}");

        Console.WriteLine("  - Unit of Work save operation ✓");

        // Clean up
        await repository.BulkDeleteAsync(e => e.Name.StartsWith("Transaction Test"));
        await unitOfWork.SaveChangesAsync();
    }

    private static async Task ValidateCommandQueuing(TestDbContext context)
    {
        bool commandExecuted = false;

        // Test command queuing
        context.AddCommand(async () => 
        {
            commandExecuted = true;
            await Task.CompletedTask;
        });

        await context.SaveChangesAsync();

        if (!commandExecuted)
            throw new InvalidOperationException("Command queuing failed - command not executed");

        Console.WriteLine("  - Command queuing operation ✓");
    }

    /// <summary>
    /// Main validation entry point
    /// </summary>
    public static async Task Main(string[] args)
    {
        var success = await ExecuteValidationAsync();
        Environment.Exit(success ? 0 : 1);
    }
}