using Axon.Api.Contracts.V1.Chat;
using Axon.Modules.Chat.Application.Queries.GetConversationMessages;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Web.Endpoints.Base;
using CSharpFunctionalExtensions;
using FastEndpoints;
using Mapster;
using MediatR;

namespace Axon.Api.Endpoints.V1.Chat.Queries.GetConversationMessages;

/// <summary>
/// Endpoint for retrieving paginated messages from a specific conversation.
/// Uses BaseQueryEndpoint pattern for clean query handling with Result pattern integration.
/// </summary>
public sealed class GetConversationMessagesEndpoint : BaseQueryEndpoint<GetConversationMessagesRequestDto, GetConversationMessagesResponseDto>
{
    private const string Route = "/api/v1/chat/conversations/{conversationId}/messages";
    private const string Tag = "Chat";
    
    private readonly IMediator _mediator;

    public GetConversationMessagesEndpoint(IMediator mediator, ILogger<GetConversationMessagesEndpoint> logger) 
        : base(logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    protected override string GetRoute() => Route;

    protected override string[] GetTags() => [Tag];

    protected override Action<EndpointSummary> GetSummary() => s =>
    {
        s.Summary = "Get paginated messages for a conversation";
        s.Description = "Retrieves messages from a specific conversation with pagination support. " +
                       "Requires authentication and ownership of the conversation. " +
                       "Messages are returned in chronological order by sequence number.";
        
        s.Params["conversationId"] = "The unique identifier of the conversation (GUID format, required).";
        s.Params["pageNumber"] = "Page number (1-based). Defaults to 1.";
        s.Params["pageSize"] = "Number of messages per page. Defaults to system default.";
        s.Params["includeDeleted"] = "Include soft-deleted messages in the results. Defaults to false.";
        
        s.Response<GetConversationMessagesResponseDto>(200, "Messages retrieved successfully");
        s.Response(400, "Invalid request parameters (validation errors)");
        s.Response(401, "User not authenticated");
        s.Response(403, "User does not own the conversation");
        s.Response(404, "Conversation not found");
        s.Response(500, "Internal server error");
        
        s.ExampleRequest = new GetConversationMessagesRequestDto
        {
            ConversationId = Guid.Parse("123e4567-e89b-12d3-a456-426614174000"),
            PageNumber = 1,
            PageSize = 50,
            IncludeDeleted = false
        };
    };

    protected override async Task<Result<GetConversationMessagesResponseDto, Error>> HandleQueryAsync(
        GetConversationMessagesRequestDto request, 
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        // Validate ConversationId is not empty (additional safety check)
        if (request.ConversationId == Guid.Empty)
        {
            return Result.Failure<GetConversationMessagesResponseDto, Error>(
                Error.Validation("ConversationId cannot be empty", "Chat.Validation.InvalidConversationId"));
        }

        // Map request to query using configured Mapster profile
        var query = request.Adapt<GetConversationMessagesQuery>();
        
        // Send query through MediatR pipeline
        // Validation happens automatically via GetConversationMessagesValidator in the pipeline
        var result = await _mediator.Send(query, ct);
        
        // Map result to response DTO if successful
        return result.IsSuccess 
            ? Result.Success<GetConversationMessagesResponseDto, Error>(result.Value.Adapt<GetConversationMessagesResponseDto>())
            : Result.Failure<GetConversationMessagesResponseDto, Error>(result.Error);
    }
}