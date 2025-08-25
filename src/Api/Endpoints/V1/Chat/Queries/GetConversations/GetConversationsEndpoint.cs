using Axon.Api.Contracts.V1.Chat;
using Axon.Modules.Chat.Application.Queries.GetConversations;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Web.Endpoints.Base;
using CSharpFunctionalExtensions;
using FastEndpoints;
using Mapster;
using MediatR;

namespace Axon.Api.Endpoints.V1.Chat.Queries.GetConversations;

/// <summary>
/// Endpoint for retrieving paginated list of conversations with filtering and sorting.
/// Uses BaseQueryEndpoint pattern for clean query handling with Result pattern integration.
/// </summary>
public sealed class GetConversationsEndpoint : BaseQueryEndpoint<GetConversationsRequestDto, GetConversationsResponseDto>
{
    private const string Route = "/api/v1/chat/conversations";
    private const string Tag = "Chat";
    
    private readonly IMediator _mediator;

    public GetConversationsEndpoint(IMediator mediator, ILogger<GetConversationsEndpoint> logger) 
        : base(logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    protected override string GetRoute() => Route;

    protected override string[] GetTags() => [Tag];

    protected override Action<EndpointSummary> GetSummary() => s =>
    {
        s.Summary = "Get paginated list of conversations";
        s.Description = "Retrieves conversations with pagination, sorting, and optional title filtering. " +
                       "All query parameters are optional with sensible defaults.";
        
        s.Params["pageNumber"] = "Page number (1-based). Defaults to 1.";
        s.Params["pageSize"] = "Items per page. Defaults to system default.";
        s.Params["sortBy"] = "Sort field: UpdatedAt, CreatedAt, Title. Defaults to UpdatedAt.";
        s.Params["sortDirection"] = "Sort direction: Asc, Desc. Defaults to Desc.";
        s.Params["titleContains"] = "Filter by title containing this text (case-insensitive).";
        
        s.Response<GetConversationsResponseDto>(200, "Conversations retrieved successfully");
        s.Response(400, "Invalid request parameters (validation errors)");
        s.Response(500, "Internal server error");
        
        s.ExampleRequest = new GetConversationsRequestDto
        {
            PageNumber = 1,
            PageSize = 20,
            SortBy = "UpdatedAt",
            SortDirection = "Desc",
            TitleContains = "project"
        };
    };

    protected override async Task<Result<GetConversationsResponseDto, Error>> HandleQueryAsync(
        GetConversationsRequestDto request, 
        CancellationToken ct)
    {
        // Map request to query using configured Mapster profile
        // This handles nullable parameter resolution and enum parsing
        var query = request.Adapt<GetConversationsQuery>();
        
        // Send query through MediatR pipeline
        // Validation happens automatically via GetConversationsValidator in the pipeline
        var result = await _mediator.Send(query, ct);
        
        // Map result to response DTO if successful
        return result.IsSuccess 
            ? Result.Success<GetConversationsResponseDto, Error>(result.Value.Adapt<GetConversationsResponseDto>())
            : Result.Failure<GetConversationsResponseDto, Error>(result.Error);
    }
}