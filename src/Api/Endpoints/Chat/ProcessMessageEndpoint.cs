// File: /Users/valentynkit/Repos/Axon-Backend/src/Api/Endpoints/Chat/ProcessMessageEndpoint.cs
using Axon.Api.Contracts.Chat;
using BuildingBlocks.Web.Endpoints.Base;
using Axon.Modules.Chat.Application.Services;
using CSharpFunctionalExtensions;

namespace Axon.Api.Endpoints.Chat;

/// <summary>
/// Universal chat endpoint following REPR pattern.
/// - If <c>ConversationId</c> is null → starts a new conversation.
/// - Otherwise → appends the user's message to the existing conversation.
/// 
/// Clean Architecture: Uses command dispatcher for business logic separation.
/// </summary>
public sealed class ProcessMessageEndpoint : BaseResultEndpoint<ProcessMessageRequest, ProcessMessageResponse>
{
    private const string Route = "/api/v1/chat/turns";
    private const string Tag = "Chat";

    private readonly IChatCommandDispatcher _dispatcher;

    public ProcessMessageEndpoint(
        IChatCommandDispatcher dispatcher,
        ILogger<ProcessMessageEndpoint> logger) : base(logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public override void Configure()
    {
        Post(Route);
        AllowAnonymous();

        // Swagger / OpenAPI
        Summary(s =>
        {
            s.Summary = "Send a chat message (start or continue a conversation).";
            s.Description =
                "Pass a message and an optional ConversationId. " +
                "If omitted, a new conversation starts. Otherwise the message is appended.";
            s.ExampleRequest = new ProcessMessageRequest
            {
                Message = "Hello! What can you do?",
                UseMcpServers = true,
                ConversationId = null,
            };
            s.Responses[200] = "Message processed; assistant response returned.";
            s.Responses[400] = "Bad request (validation).";
            s.Responses[500] = "Processing failed (server error).";
        });

        Tags(Tag);
    }

    protected override async Task<Result<ProcessMessageResponse>> ExecuteAsync(ProcessMessageRequest request, CancellationToken cancellationToken)
    {
        var isNewConversation = request.ConversationId == null;
        Logger.LogInformation(
            "Processing chat message (newConversation={NewConversation}, conversationId={ConversationId})",
            isNewConversation,
            request.ConversationId);

        return await _dispatcher.ProcessMessageAsync(request, cancellationToken);
    }


}
