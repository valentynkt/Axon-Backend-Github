using BuildingBlocks.Application;
using BuildingBlocks.Core.Domain.Entities.Abstractions;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using NUnit.Framework.Internal;
using Shouldly;

namespace BuildingBlocks.Testing;

/// <summary>
/// Base class for testing concurrency scenarios with EF Core.
/// Provides helpers for creating separate DbContext instances to properly simulate
/// concurrent modifications from different users/sessions.
/// </summary>
public abstract class ConcurrencyTestBase<TContext> : PostgreSqlTestBase
    where TContext : DbContext
{
    /// <summary>
    /// Creates DbContext options for the test database.
    /// Override in derived classes to add module-specific configuration.
    /// </summary>
    protected abstract DbContextOptions<TContext> CreateContextOptions();

    /// <summary>
    /// Creates a new instance of the DbContext.
    /// Override in derived classes to provide proper constructor parameters.
    /// </summary>
    protected abstract TContext CreateContext(DbContextOptions<TContext> options);

    /// <summary>
    /// Creates two separate DbContext instances for simulating concurrent access.
    /// Each context represents a different user session or client connection.
    /// </summary>
    protected async Task<(TContext context1, TContext context2)> CreateConcurrentContextsAsync()
    {
        var context1 = CreateContext(CreateContextOptions());
        var context2 = CreateContext(CreateContextOptions());

        // Ensure both contexts are ready
        await Task.WhenAll(
            context1.Database.CanConnectAsync(),
            context2.Database.CanConnectAsync());

        return (context1, context2);
    }

    /// <summary>
    /// Result of a concurrent update simulation.
    /// </summary>
    public class ConcurrencyTestResult
    {
        public bool FirstUpdateSucceeded { get; init; }
        public bool SecondUpdateFailed { get; init; }
        public Exception? SecondUpdateException { get; init; }
        public bool WasConcurrencyException =>
            SecondUpdateException is BuildingBlocks.Core.Diagnostics.Exceptions.ConcurrencyException ||
            SecondUpdateException is DbUpdateConcurrencyException;
    }

    /// <summary>
    /// Simulates concurrent updates to the same aggregate from two different contexts.
    /// </summary>
    protected async Task<ConcurrencyTestResult> SimulateConcurrentUpdatesAsync<TAggregate, TId>(
        TId aggregateId,
        Func<TContext, IWriteRepository<TAggregate, TId>> createRepository,
        Func<TAggregate, UnitResult<Error>> modify1,
        Func<TAggregate, UnitResult<Error>> modify2)
        where TAggregate : class, IAggregateRoot<TId>
        where TId : notnull
    {
        // Create two separate contexts
        var (context1, context2) = await CreateConcurrentContextsAsync();

        try
        {
            var repo1 = createRepository(context1);
            var repo2 = createRepository(context2);

            // Load the same aggregate in both contexts
            var aggregate1 = await repo1.GetByIdAsync(aggregateId);
            var aggregate2 = await repo2.GetByIdAsync(aggregateId);

            aggregate1.ShouldNotBeNull("Aggregate not found in context1");
            aggregate2.ShouldNotBeNull("Aggregate not found in context2");

            // Apply modifications
            var result1 = modify1(aggregate1);
            var result2 = modify2(aggregate2);

            result1.IsSuccess.ShouldBeTrue("First modification should succeed");
            result2.IsSuccess.ShouldBeTrue("Second modification should succeed");

            // Update and save first context
            await repo1.UpdateAsync(aggregate1);
            await context1.SaveChangesAsync();

            // Try to update and save second context
            bool secondUpdateFailed = false;
            Exception? secondException = null;

            try
            {
                await repo2.UpdateAsync(aggregate2);
                await context2.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                secondUpdateFailed = true;
                secondException = ex;
            }

            return new ConcurrencyTestResult
            {
                FirstUpdateSucceeded = true,
                SecondUpdateFailed = secondUpdateFailed,
                SecondUpdateException = secondException
            };
        }
        finally
        {
            await context1.DisposeAsync();
            await context2.DisposeAsync();
        }
    }

    /// <summary>
    /// Simulates concurrent updates with detached entities.
    /// </summary>
    protected async Task<ConcurrencyTestResult> SimulateConcurrentDetachedUpdatesAsync<TAggregate, TId>(
        TId aggregateId,
        Func<TContext, IWriteRepository<TAggregate, TId>> createRepository,
        Func<TAggregate, UnitResult<Error>> modify1,
        Func<TAggregate, UnitResult<Error>> modify2)
        where TAggregate : class, IAggregateRoot<TId>
        where TId : notnull
    {
        TAggregate? detached1;
        TAggregate? detached2;

        // Load entities and immediately detach them
        using (var loadContext = CreateContext(CreateContextOptions()))
        {
            var loadRepo = createRepository(loadContext);

            var entity1 = await loadRepo.GetByIdAsync(aggregateId);
            entity1.ShouldNotBeNull();

            var entity2 = await loadRepo.GetByIdAsync(aggregateId);
            entity2.ShouldNotBeNull();

            // Detach entities
            loadContext.Entry(entity1).State = EntityState.Detached;
            detached1 = entity1;

            // Since GetByIdAsync with same ID returns same instance,
            // we need to reload in a fresh context
            loadContext.ChangeTracker.Clear();
            entity2 = await loadRepo.GetByIdAsync(aggregateId);
            entity2.ShouldNotBeNull();
            loadContext.Entry(entity2).State = EntityState.Detached;
            detached2 = entity2;
        }

        // Now use the detached entities in new contexts
        var (context1, context2) = await CreateConcurrentContextsAsync();

        try
        {
            var repo1 = createRepository(context1);
            var repo2 = createRepository(context2);

            // Apply modifications to detached entities
            var result1 = modify1(detached1);
            var result2 = modify2(detached2);

            result1.IsSuccess.ShouldBeTrue("First modification should succeed");
            result2.IsSuccess.ShouldBeTrue("Second modification should succeed");

            // Update and save first context with detached entity
            await repo1.UpdateAsync(detached1);
            await context1.SaveChangesAsync();

            // Try to update and save second context with detached entity
            bool secondUpdateFailed = false;
            Exception? secondException = null;

            try
            {
                await repo2.UpdateAsync(detached2);
                await context2.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                secondUpdateFailed = true;
                secondException = ex;
            }

            return new ConcurrencyTestResult
            {
                FirstUpdateSucceeded = true,
                SecondUpdateFailed = secondUpdateFailed,
                SecondUpdateException = secondException
            };
        }
        finally
        {
            await context1.DisposeAsync();
            await context2.DisposeAsync();
        }
    }

    /// <summary>
    /// Asserts that an action throws a concurrency exception.
    /// Note: The application wraps DbUpdateConcurrencyException in a custom ConcurrencyException.
    /// </summary>
    protected static void AssertConcurrencyException(Func<Task> action)
    {
        try
        {
            action().GetAwaiter().GetResult();
            throw new AssertionException("Expected concurrency exception but none was thrown");
        }
        catch (BuildingBlocks.Core.Diagnostics.Exceptions.ConcurrencyException)
        {
            // Expected - test passes
        }
        catch (DbUpdateConcurrencyException)
        {
            // Also acceptable if EF Core exception is not wrapped
        }
    }

    /// <summary>
    /// Asserts that a concurrency test result indicates proper optimistic concurrency handling.
    /// </summary>
    protected static void AssertOptimisticConcurrencyHandled(ConcurrencyTestResult result)
    {
        result.FirstUpdateSucceeded.ShouldBeTrue("First update should succeed");
        result.SecondUpdateFailed.ShouldBeTrue("Second update should fail due to concurrency");
        result.WasConcurrencyException.ShouldBeTrue("Should fail with DbUpdateConcurrencyException");
    }

    /// <summary>
    /// Creates a simple aggregate for testing basic concurrency scenarios.
    /// Override in derived classes to create module-specific test aggregates.
    /// </summary>
    protected abstract Task<TId> CreateAndSaveTestAggregateAsync<TAggregate, TId>()
        where TAggregate : class, IAggregateRoot<TId>
        where TId : notnull;
}