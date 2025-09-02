#nullable enable

using BuildingBlocks.Application;

namespace Axon.Modules.Identity.Application.Contracts.Persistence;

/// <summary>
/// Identity module-specific read repository abstraction extending Ardalis.Specification.
/// Provides query-only operations with specification pattern support for complex queries.
/// </summary>
/// <typeparam name="T">The entity type to query</typeparam>
public interface IReadRepository<T> : ISpecificationReadRepository<T>
    where T : class
{
}

/// <summary>
/// Non-generic marker interface for read repositories in the Identity module.
/// Useful for dependency injection registration and service discovery.
/// </summary>
public interface IIdentityReadRepository : ISpecificationReadRepositoryMarker
{
}