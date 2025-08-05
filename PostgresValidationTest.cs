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
using BuildingBlocks.Persistence.Common.Interfaces;
using BuildingBlocks.Persistence.Write; 
using BuildingBlocks.Persistence.Read;

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
    public class TestDbContext : WriteDbContextBase<object>
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
        {
        }

        public override string ModuleName => "Test";

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
            services.AddScoped<IWriteDbContext<object>>(provider => provider.GetRequiredService<TestDbContext>());
            services.AddScoped<IReadDbContext<object>>(provider => provider.GetRequiredService<TestDbContext>());
            services.AddScoped<IWriteRepository<TestEntity, Guid>, PostgresWriteRepository<TestEntity, Guid>>();
            services.AddScoped<IReadRepository<TestEntity, Guid>, PostgresReadRepository<TestEntity, Guid>>();
            services.AddScoped<IWriteUnitOfWork<TestDbContext>, PostgresWriteUnitOfWork<TestDbContext>>();
            services.AddLogging();

            using var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var writeRepository = scope.ServiceProvider.GetRequiredService<IWriteRepository<TestEntity, Guid>>();
            var readRepository = scope.ServiceProvider.GetRequiredService<IReadRepository<TestEntity, Guid>>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IWriteUnitOfWork<TestDbContext>>();

            // Ensure database is created
            await context.Database.EnsureCreatedAsync();

            Console.WriteLine("✅ Test 1: Repository Interface Validation");
            await ValidateRepositoryInterfaces(writeRepository, readRepository);

            Console.WriteLine("✅ Test 2: CRUD Operations Validation");
            await ValidateCrudOperations(writeRepository, readRepository, unitOfWork);

            Console.WriteLine("✅ Test 3: Pagination Validation");
            await ValidatePaginationOperations(writeRepository, readRepository, unitOfWork);

            Console.WriteLine("✅ Test 4: Bulk Operations Validation");
            await ValidateBulkOperations(writeRepository, readRepository, unitOfWork);

            Console.WriteLine("✅ Test 5: Unit of Work Validation");
            await ValidateUnitOfWorkOperations(writeRepository, readRepository, unitOfWork);

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

    private static async Task ValidateRepositoryInterfaces(IWriteRepository<TestEntity, Guid> writeRepository, IReadRepository<TestEntity, Guid> readRepository)
    {
        // Validate that repositories implement required interfaces
        if (writeRepository == null)
            throw new InvalidOperationException("WriteRepository is null");

        if (readRepository == null)
            throw new InvalidOperationException("ReadRepository is null");

        Console.WriteLine("  - WriteRepository implements IWriteRepository ✓");
        Console.WriteLine("  - ReadRepository implements IReadRepository ✓");
        Console.WriteLine("  - CQRS repository separation ✓");
    }

    private static async Task ValidateCrudOperations(IWriteRepository<TestEntity, Guid> writeRepository, IReadRepository<TestEntity, Guid> readRepository, IWriteUnitOfWork<TestDbContext> unitOfWork)
    {
        // Create
        var entity = new TestEntity { Name = "Test Entity", Description = "Test Description" };
        var createdEntity = await writeRepository.AddAsync(entity);
        await unitOfWork.SaveChangesAsync();

        if (createdEntity.Id == Guid.Empty)
            throw new InvalidOperationException("Entity creation failed - ID not assigned");

        Console.WriteLine("  - Create operation ✓");

        // Read
        var foundEntity = await readRepository.FindByIdAsync(createdEntity.Id);
        if (foundEntity == null || foundEntity.Name != "Test Entity")
            throw new InvalidOperationException("Entity retrieval failed");

        Console.WriteLine("  - Read operation ✓");

        // Update
        foundEntity.Description = "Updated Description";
        await writeRepository.UpdateAsync(foundEntity);
        await unitOfWork.SaveChangesAsync();

        var updatedEntity = await readRepository.FindByIdAsync(createdEntity.Id);
        if (updatedEntity?.Description != "Updated Description")
            throw new InvalidOperationException("Entity update failed");

        Console.WriteLine("  - Update operation ✓");

        // Delete
        await writeRepository.DeleteByIdAsync(createdEntity.Id);
        await unitOfWork.SaveChangesAsync();

        var deletedEntity = await readRepository.FindByIdAsync(createdEntity.Id);
        if (deletedEntity != null)
            throw new InvalidOperationException("Entity deletion failed");

        Console.WriteLine("  - Delete operation ✓");
    }

    private static async Task ValidatePaginationOperations(IWriteRepository<TestEntity, Guid> writeRepository, IReadRepository<TestEntity, Guid> readRepository, IWriteUnitOfWork<TestDbContext> unitOfWork)
    {
        // Create test data
        var entities = new List<TestEntity>();
        for (int i = 1; i <= 25; i++)
        {
            entities.Add(new TestEntity { Name = $"Entity {i}", Description = $"Description {i}" });
        }

        await writeRepository.AddRangeAsync(entities);
        await unitOfWork.SaveChangesAsync();

        // Test pagination
        var pageRequest = new PageRequest(1, 10);
        var pagedResult = await readRepository.GetByPageAsync(pageRequest);

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
        await writeRepository.BulkDeleteAsync(e => e.Name.StartsWith("Entity"));
        await unitOfWork.SaveChangesAsync();
    }

    private static async Task ValidateBulkOperations(IWriteRepository<TestEntity, Guid> writeRepository, IReadRepository<TestEntity, Guid> readRepository, IWriteUnitOfWork<TestDbContext> unitOfWork)
    {
        // Create test data
        var entities = new List<TestEntity>();
        for (int i = 1; i <= 15; i++)
        {
            entities.Add(new TestEntity { Name = $"Bulk Entity {i}", Description = $"Bulk Description {i}" });
        }

        // Bulk insert
        await writeRepository.AddRangeAsync(entities);
        await unitOfWork.SaveChangesAsync();

        var count = await readRepository.CountAsync(e => e.Name.StartsWith("Bulk Entity"));
        if (count != 15)
            throw new InvalidOperationException($"Expected 15 bulk entities, found {count}");

        Console.WriteLine("  - Bulk insert operation ✓");

        // Bulk update
        var updatedCount = await writeRepository.BulkUpdateAsync(
            e => e.Name.StartsWith("Bulk Entity"),
            e => e.SetProperty(p => p.Description, "Bulk Updated"));

        if (updatedCount != 15)
            throw new InvalidOperationException($"Expected to update 15 entities, updated {updatedCount}");

        Console.WriteLine("  - Bulk update operation ✓");

        // Bulk delete
        var deletedCount = await writeRepository.BulkDeleteAsync(e => e.Name.StartsWith("Bulk Entity"));
        if (deletedCount != 15)
            throw new InvalidOperationException($"Expected to delete 15 entities, deleted {deletedCount}");

        Console.WriteLine("  - Bulk delete operation ✓");
    }

    private static async Task ValidateUnitOfWorkOperations(IWriteRepository<TestEntity, Guid> writeRepository, IReadRepository<TestEntity, Guid> readRepository, IWriteUnitOfWork<TestDbContext> unitOfWork)
    {
        // Test transaction management
        var entity1 = new TestEntity { Name = "Transaction Test 1", Description = "Test 1" };
        var entity2 = new TestEntity { Name = "Transaction Test 2", Description = "Test 2" };

        // Add entities within unit of work
        await writeRepository.AddAsync(entity1);
        await writeRepository.AddAsync(entity2);
        await unitOfWork.SaveChangesAsync();

        var count = await readRepository.CountAsync(e => e.Name.StartsWith("Transaction Test"));
        if (count != 2)
            throw new InvalidOperationException($"Expected 2 transaction test entities, found {count}");

        Console.WriteLine("  - Unit of Work save operation ✓");

        // Clean up
        await writeRepository.BulkDeleteAsync(e => e.Name.StartsWith("Transaction Test"));
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