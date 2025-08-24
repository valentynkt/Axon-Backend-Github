#nullable enable

using Ardalis.Specification;
using Axon.Modules.Chat.Application.Common.Pagination;

namespace Axon.Modules.Chat.Application.Specifications.Base;

/// <summary>
/// Base specification for paginated entity queries without projections.
/// Automatically applies AsNoTracking and paging configuration for EF Core optimization.
/// </summary>
/// <typeparam name="T">The entity type to query</typeparam>
public abstract class PagedSpecification<T> : Specification<T>
    where T : class
{
    /// <summary>
    /// Initializes a new instance of the PagedSpecification class with paging configuration.
    /// Automatically applies AsNoTracking for read-only queries and configures Skip/Take for pagination.
    /// </summary>
    /// <param name="page">The page configuration containing number and size</param>
    protected PagedSpecification(Page page)
    {
        Query
            .AsNoTracking()
            .Skip(page.Skip)
            .Take(page.Size);
    }

    /// <summary>
    /// Gets the page configuration for this specification.
    /// </summary>
    protected Page Page { get; }

    /// <summary>
    /// Creates a specification builder for additional query configuration.
    /// Derived classes can use this to add filters, ordering, and includes.
    /// </summary>
    protected ISpecificationBuilder<T> ConfigureQuery() => Query;
}

/// <summary>
/// Base specification for paginated entity queries with result projections.
/// Automatically applies AsNoTracking and paging configuration, with support for SELECT projections.
/// </summary>
/// <typeparam name="T">The entity type to query</typeparam>
/// <typeparam name="TResult">The result type after projection</typeparam>
public abstract class PagedSpecification<T, TResult> : Specification<T, TResult>
    where T : class
{
    /// <summary>
    /// Initializes a new instance of the PagedSpecification class with paging and projection configuration.
    /// Automatically applies AsNoTracking for read-only queries and configures Skip/Take for pagination.
    /// </summary>
    /// <param name="page">The page configuration containing number and size</param>
    protected PagedSpecification(Page page)
    {
        Query
            .AsNoTracking()
            .Skip(page.Skip)
            .Take(page.Size);
            
        Page = page;
    }

    /// <summary>
    /// Gets the page configuration for this specification.
    /// </summary>
    protected Page Page { get; }

    /// <summary>
    /// Creates a specification builder for additional query configuration.
    /// Derived classes can use this to add filters, ordering, includes, and projections.
    /// </summary>
    protected ISpecificationBuilder<T, TResult> ConfigureQuery() => Query;
}

/// <summary>
/// Companion specification for counting total items without paging constraints.
/// Used together with PagedSpecification to provide total count information.
/// </summary>
/// <typeparam name="T">The entity type to count</typeparam>
public abstract class CountSpecification<T> : Specification<T>
    where T : class
{
    /// <summary>
    /// Initializes a new instance of the CountSpecification class.
    /// Automatically applies AsNoTracking for optimal count performance.
    /// </summary>
    protected CountSpecification()
    {
        Query.AsNoTracking();
    }

    /// <summary>
    /// Creates a specification builder for count query configuration.
    /// Should apply the same filters as the corresponding PagedSpecification, but without paging.
    /// </summary>
    protected ISpecificationBuilder<T> ConfigureQuery() => Query;
}