using Axon.Modules.Chat.Application.Abstractions.AI;
using BuildingBlocks.Core.Abstractions.CQRS;
using BuildingBlocks.Core.Functional.Results;
using MediatR;

namespace Axon.Modules.Chat.Application.Commands.ProcessMessage;

/// <summary>
/// Simple handler for processing chat messages
/// </summary>
public sealed class ProcessMessageHandler : ICommandHandler<ProcessMessageCommand, ProcessMessageResponse>
{
    private readonly IAiClient _aiClient;
    private readonly IMessageRequestBuilder _requestBuilder;
    private readonly IResponseMappingService _responseMapper;
    
    public ProcessMessageHandler(
        IAiClient aiClient,
        IMessageRequestBuilder requestBuilder,
        IResponseMappingService responseMapper)
    {
        _aiClient = aiClient;
        _requestBuilder = requestBuilder;
        _responseMapper = responseMapper;
    }
    
    public async Task<Result<ProcessMessageResponse>> Handle(
        ProcessMessageCommand request, 
        CancellationToken cancellationToken)
    {
        // Build AI request
        var buildResult = _requestBuilder.BuildAiRequest(request.Message, request.PreviousResponseId);
        if (buildResult.IsFailure)
        {
            return Result<ProcessMessageResponse>.Failure(buildResult.Error);
        }
        
        var (aiRequest, _) = buildResult.Value;
        
        // Process with AI
        var result = await _aiClient.ProcessMessageAsync(aiRequest, cancellationToken);
        
        if (result.IsFailure)
        {
            return Result<ProcessMessageResponse>.Failure(result.Error);
        }
        
        // Validate and process response
        var validationResult = _responseMapper.ValidateAndProcessResponse(result.Value);
        if (validationResult.IsFailure)
        {
            return Result<ProcessMessageResponse>.Failure(validationResult.Error);
        }
        
        // Create response
        var response = new ProcessMessageResponse(
            Response: validationResult.Value.Content,
            ConversationId: request.ConversationId,
            ToolExecutions: validationResult.Value.ToolExecutions);
        
        return Result<ProcessMessageResponse>.Success(response);
    }
}