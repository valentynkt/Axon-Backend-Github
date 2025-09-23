// /BuildingBlocks/Application/Behaviors/PaginationBehavior.cs
#nullable enable
using BuildingBlocks.Core.Abstractions.CQRS;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// Pipeline behavior that validates pagination parameters for requests implementing IPaginatedRequest.
/// Ensures PageNumber and PageSize are within valid ranges before proceeding with request processing.
/// For non-paginated requests, this behavior is bypassed.
/// </summary>
/// <typeparam name="TRequest">The request type</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
public sealed class PaginationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, Result<TResponse, Error>>
    where TRequest : IRequest<Result<TResponse, Error>>
{
    private const int MinPageNumber = 1;
    private const int MaxPageNumber = 10000;
    private const int MinPageSize = 1;
    private const int MaxPageSize = 100;

    public async Task<Result<TResponse, Error>> Handle(
        TRequest request,
        RequestHandlerDelegate<Result<TResponse, Error>> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        // Only apply pagination validation to requests that implement IPaginatedRequest
        if (request is not IPaginatedRequest paginatedRequest)
        {
            return await next();
        }

        // Validate page number
        if (paginatedRequest.PageNumber < MinPageNumber || paginatedRequest.PageNumber > MaxPageNumber)
        {
            return Result.Failure<TResponse, Error>(
                Error.Validation("PAGINATION_001", $"Page number must be between {MinPageNumber} and {MaxPageNumber}."));
        }

        // Validate page size
        if (paginatedRequest.PageSize < MinPageSize || paginatedRequest.PageSize > MaxPageSize)
        {
            return Result.Failure<TResponse, Error>(
                Error.Validation("PAGINATION_002", $"Page size must be between {MinPageSize} and {MaxPageSize}."));
        }

        // Pagination parameters are valid, proceed with the request
        return await next();
    }
}