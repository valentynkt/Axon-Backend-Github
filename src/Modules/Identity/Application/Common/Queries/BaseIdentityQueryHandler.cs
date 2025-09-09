using BuildingBlocks.Application.Services;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;

namespace Axon.Modules.Identity.Application.Common.Queries;

/// <summary>
/// Base handler for Identity module queries with common authentication and validation logic
/// </summary>
/// <typeparam name="TQuery">The query type</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
public abstract class BaseIdentityQueryHandler<TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse, Error>>
    where TQuery : IdentityBaseQuery<TResponse>
    where TResponse : notnull
{
    private readonly ICurrentUserService _currentUserService;

    protected BaseIdentityQueryHandler(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public abstract Task<Result<TResponse, Error>> Handle(TQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the current user service for accessing authenticated user information
    /// </summary>
    protected ICurrentUserService CurrentUserService => _currentUserService;
}