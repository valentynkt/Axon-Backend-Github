#nullable enable

using Ardalis.Specification;

namespace BuildingBlocks.Application;

/// <summary>
/// Specification-based read repository abstraction extending Ardalis.Specification.
/// Provides query-only operations with specification pattern support for complex queries.
/// Complements the Expression-based IReadRepository for scenarios requiring specification patterns.
/// </summary>
/// <typeparam name="T">The entity type to query</typeparam>
public interface ISpecificationReadRepository<T> : IReadRepositoryBase<T>
    where T : class
{
}

/// <summary>
/// Non-generic marker interface for specification-based read repositories.
/// Useful for dependency injection registration and service discovery.
/// Allows modules to define their own specific repository interfaces.
/// </summary>
public interface ISpecificationReadRepositoryMarker
{
}