// /BuildingBlocks/Application/Behaviors/AuthenticationBehavior.cs
#nullable enable
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Abstractions.CQRS;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// Pipeline behavior that handles authentication for requests implementing IAuthenticatedRequest.
/// Ensures the current user is authenticated before proceeding with request processing.
/// For non-authenticated requests, this behavior is bypassed.
/// </summary>
/// <typeparam name="TRequest">The request type</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
public sealed class AuthenticationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, Result<TResponse, Error>>
    where TRequest : IRequest<Result<TResponse, Error>>
{
    private readonly ICurrentUserService _currentUserService;

    public AuthenticationBehavior(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public async Task<Result<TResponse, Error>> Handle(
        TRequest request,
        RequestHandlerDelegate<Result<TResponse, Error>> next,
        CancellationToken cancellationToken)
    {
        // Only apply authentication to requests that implement IAuthenticatedRequest
        if (request is not IAuthenticatedRequest)
        {
            return await next();
        }

        // Check if user is authenticated
        if (string.IsNullOrEmpty(_currentUserService.AxonUserId))
        {
            return Result.Failure<TResponse, Error>(
                Error.Unauthorized("User must be authenticated to access this resource.", "Chat.Auth.Unauthenticated"));
        }

        // User is authenticated, proceed with the request
        return await next();
    }
}