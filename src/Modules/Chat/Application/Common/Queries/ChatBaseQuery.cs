using BuildingBlocks.Core.Abstractions.CQRS;

namespace Axon.Modules.Chat.Application.Common.Queries;

/// <summary>
/// Base class for all Chat module queries.
/// Provides common interfaces for all Chat queries: authenticated and query functionality.
/// Child classes should also implement IPaginatedRequest with their specific pagination properties.
/// </summary>
/// <typeparam name="TResponse">The type of response the query returns</typeparam>
public abstract record ChatBaseQuery<TResponse> : RequestBase, IQuery<TResponse>, IAuthenticatedRequest
    where TResponse : notnull;