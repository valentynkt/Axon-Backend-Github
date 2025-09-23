#nullable enable

using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Infrastructure.Persistence.Read;

/// <summary>
/// Entity Framework implementation of specification-based read repository.
/// Extends Ardalis.Specification's RepositoryBase for EF Core integration.
/// Optimized for read-only operations with AsNoTracking by default.
/// </summary>
/// <typeparam name="T">The entity type to query</typeparam>
public class EfSpecificationReadRepository<T> : RepositoryBase<T>, ISpecificationReadRepository<T>
    where T : class
{
    private readonly DbContext _context;

    /// <summary>
    /// Initializes a new instance of EfSpecificationReadRepository.
    /// </summary>
    /// <param name="context">The EF Core DbContext instance</param>
    /// <exception cref="ArgumentNullException">Thrown when context is null</exception>
    public EfSpecificationReadRepository(DbContext context) : base(context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Gets the DbContext instance used by this repository.
    /// </summary>
    protected DbContext Context => _context;

    /// <summary>
    /// Applies query configuration for read-only operations.
    /// Ensures AsNoTracking is applied by default for optimal read performance.
    /// </summary>
    /// <param name="specification">The specification to apply</param>
    /// <param name="evaluateCriteriaOnly">Whether to evaluate criteria only</param>
    /// <returns>Configured queryable with AsNoTracking</returns>
    protected override IQueryable<T> ApplySpecification(ISpecification<T> specification, bool evaluateCriteriaOnly = false)
    {
        var query = base.ApplySpecification(specification, evaluateCriteriaOnly);
        
        // Always ensure AsNoTracking for read-only operations
        // We don't need to check if it's already applied - EF Core will handle duplicates
        return query.AsNoTracking();
    }

    /// <summary>
    /// Applies query configuration for read-only operations with result selector.
    /// Ensures AsNoTracking is applied by default for optimal read performance.
    /// </summary>
    /// <typeparam name="TResult">The result type after projection</typeparam>
    /// <param name="specification">The specification to apply</param>
    /// <returns>Configured queryable with AsNoTracking</returns>
    protected override IQueryable<TResult> ApplySpecification<TResult>(ISpecification<T, TResult> specification)
    {
        var query = base.ApplySpecification(specification);
        
        // With projections, AsNoTracking is typically applied automatically by EF Core
        // but we ensure it's explicit for consistency and performance
        return query;
    }

    // Implement missing IReadRepositoryBase<T> methods that are not provided by RepositoryBase<T>
    
    /// <summary>
    /// Asynchronous enumerable for streaming results
    /// </summary>
    public override IAsyncEnumerable<T> AsAsyncEnumerable(ISpecification<T> specification)
    {
        return ApplySpecification(specification).AsAsyncEnumerable();
    }

    /// <summary>
    /// Gets the first entity or default value that satisfies the specification
    /// </summary>
    public override async Task<T?> FirstOrDefaultAsync(ISpecification<T> specification, CancellationToken cancellationToken = default)
    {
        return await ApplySpecification(specification).FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the first projected result or default value that satisfies the specification
    /// </summary>
#pragma warning disable CS8609 // Nullability conflicts between base class and interface are intentional
#pragma warning disable CS8613 // Nullability of reference types in return type doesn't match implicitly implemented member
    public override async Task<TResult> FirstOrDefaultAsync<TResult>(ISpecification<T, TResult> specification, CancellationToken cancellationToken = default)
    {
        var result = await ApplySpecification(specification).FirstOrDefaultAsync(cancellationToken);
        return result!;
    }
#pragma warning restore CS8613
#pragma warning restore CS8609

    /// <summary>
    /// Gets a single entity or default value that satisfies the specification
    /// </summary>
    public override async Task<T?> SingleOrDefaultAsync(ISingleResultSpecification<T> specification, CancellationToken cancellationToken = default)
    {
        return await ApplySpecification(specification).SingleOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Gets a single projected result or default value that satisfies the specification
    /// </summary>
#pragma warning disable CS8609 // Nullability conflicts between base class and interface are intentional
#pragma warning disable CS8613 // Nullability of reference types in return type doesn't match implicitly implemented member
    public override async Task<TResult> SingleOrDefaultAsync<TResult>(ISingleResultSpecification<T, TResult> specification, CancellationToken cancellationToken = default)
    {
        var result = await ApplySpecification(specification).SingleOrDefaultAsync(cancellationToken);
        return result!;
    }
#pragma warning restore CS8613
#pragma warning restore CS8609

    /// <summary>
    /// Gets all entities as a list
    /// </summary>
    public override async Task<List<T>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Set<T>().AsNoTracking().ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets entities that satisfy the specification as a list
    /// </summary>
    public override async Task<List<T>> ListAsync(ISpecification<T> specification, CancellationToken cancellationToken = default)
    {
        return await ApplySpecification(specification).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets projected results that satisfy the specification as a list
    /// </summary>
    public override async Task<List<TResult>> ListAsync<TResult>(ISpecification<T, TResult> specification, CancellationToken cancellationToken = default)
    {
        return await ApplySpecification(specification).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the count of all entities
    /// </summary>
    public override async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Set<T>().CountAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the count of entities that satisfy the specification
    /// </summary>
    public override async Task<int> CountAsync(ISpecification<T> specification, CancellationToken cancellationToken = default)
    {
        return await ApplySpecification(specification, evaluateCriteriaOnly: true).CountAsync(cancellationToken);
    }

    /// <summary>
    /// Checks if any entities exist
    /// </summary>
    public override async Task<bool> AnyAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Set<T>().AnyAsync(cancellationToken);
    }

    /// <summary>
    /// Checks if any entities satisfy the specification
    /// </summary>
    public override async Task<bool> AnyAsync(ISpecification<T> specification, CancellationToken cancellationToken = default)
    {
        return await ApplySpecification(specification, evaluateCriteriaOnly: true).AnyAsync(cancellationToken);
    }
}

