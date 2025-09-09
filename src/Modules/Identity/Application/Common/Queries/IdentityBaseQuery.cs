using BuildingBlocks.Core.Abstractions.CQRS;

namespace Axon.Modules.Identity.Application.Common.Queries;

/// <summary>
/// Base class for all Identity module queries.
/// Provides common functionality for authenticated query execution.
/// </summary>
/// <typeparam name="TResponse">The type of response returned by the query</typeparam>
public abstract record IdentityBaseQuery<TResponse> : RequestBase, IQuery<TResponse>, IAuthenticatedRequest
    where TResponse : notnull;