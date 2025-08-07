# Part 3: Chat Application Layer - Comprehensive Implementation Guide

## Overview
The Application Layer orchestrates use cases using railway-oriented programming, CQRS, and MediatR pipeline. Every operation returns `Result<T>` for complete error handling.

## Core Principles
- **Railway-Oriented CQRS**: All handlers return `Result<T>`
- **Clean Separation**: Commands modify state, Queries read state
- **Pipeline Behaviors**: Cross-cutting concerns via MediatR
- **DTO Mapping**: Clean boundaries between layers
- **Caching Strategy**: Read-through caching for queries

## 1. Commands - State Modifications

### 1.1 Start Conversation Command

```csharp
// Modules/Chat/Application/Commands/StartConversation/StartConversationCommand.cs
using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Results;

namespace Axon.Modules.Chat.Application.Commands.StartConversation;

public sealed record StartConversationCommand : ICommand<Result<ConversationDto>>
{
    public required string Title { get; init; }
    public required Guid UserId { get; init; }
    public required string InitialMessage { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}

// Modules/Chat/Application/Commands/StartConversation/StartConversationCommandHandler.cs
using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Results;
using Axon.Modules.Chat.Domain.Conversation;
using Axon.Modules.Chat.Domain.Conversation.ValueObjects;
using Axon.Modules.Chat.Domain.Conversation.Repositories;

namespace Axon.Modules.Chat.Application.Commands.StartConversation;

public sealed class StartConversationCommandHandler : ICommandHandler<StartConversationCommand, Result<ConversationDto>>
{
    private readonly IConversationWriteRepository _writeRepository;
    private readonly IConversationMapper _mapper;
    private readonly IEventBus _eventBus;
    private readonly ILogger<StartConversationCommandHandler> _logger;

    public StartConversationCommandHandler(
        IConversationWriteRepository writeRepository,
        IConversationMapper mapper,
        IEventBus eventBus,
        ILogger<StartConversationCommandHandler> logger)
    {
        _writeRepository = writeRepository;
        _mapper = mapper;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<Result<ConversationDto>> Handle(
        StartConversationCommand command, 
        CancellationToken cancellationToken)
    {
        return await ConversationTitle.Create(command.Title)
            .Bind(title => UserId.Create(command.UserId))
            .Map(userId => (title: title, userId: userId))
            .Bind(tuple => MessageContent.Create(command.InitialMessage)
                .Map(content => (tuple.title, tuple.userId, content)))
            .Bind(async tuple =>
            {
                var conversation = Conversation.Start(
                    ConversationId.New(),
                    tuple.title,
                    tuple.userId,
                    tuple.content,
                    command.Metadata);

                return await _writeRepository
                    .AddAsync(conversation, cancellationToken)
                    .Bind(async _ =>
                    {
                        // Publish domain events
                        foreach (var domainEvent in conversation.DomainEvents)
                        {
                            await _eventBus.PublishAsync(domainEvent, cancellationToken);
                        }
                        
                        _logger.LogInformation(
                            "Started conversation {ConversationId} for user {UserId}",
                            conversation.Id.Value, tuple.userId.Value);
                        
                        return Result.Success(_mapper.ToDto(conversation));
                    });
            });
    }
}

// Modules/Chat/Application/Commands/StartConversation/StartConversationCommandValidator.cs
using FluentValidation;

namespace Axon.Modules.Chat.Application.Commands.StartConversation;

public sealed class StartConversationCommandValidator : AbstractValidator<StartConversationCommand>
{
    public StartConversationCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required");

        RuleFor(x => x.InitialMessage)
            .NotEmpty().WithMessage("Initial message is required")
            .MaximumLength(4000).WithMessage("Message must not exceed 4000 characters");

        When(x => x.Metadata != null, () =>
        {
            RuleFor(x => x.Metadata)
                .Must(m => m!.Count <= 10)
                .WithMessage("Metadata cannot have more than 10 entries");
        });
    }
}
```

### 1.2 Send Message Command

```csharp
// Modules/Chat/Application/Commands/SendMessage/SendMessageCommand.cs
namespace Axon.Modules.Chat.Application.Commands.SendMessage;

public sealed record SendMessageCommand : ICommand<Result<MessageDto>>
{
    public required Guid ConversationId { get; init; }
    public required Guid UserId { get; init; }
    public required string Content { get; init; }
    public MessageRole Role { get; init; } = MessageRole.User;
    public List<string>? Attachments { get; init; }
}

// Modules/Chat/Application/Commands/SendMessage/SendMessageCommandHandler.cs
public sealed class SendMessageCommandHandler : ICommandHandler<SendMessageCommand, Result<MessageDto>>
{
    private readonly IConversationWriteRepository _writeRepository;
    private readonly IMessageValidator _messageValidator;
    private readonly IConversationMapper _mapper;
    private readonly IEventBus _eventBus;

    public async Task<Result<MessageDto>> Handle(
        SendMessageCommand command,
        CancellationToken cancellationToken)
    {
        return await ConversationId.Create(command.ConversationId)
            .BindAsync(async conversationId => 
                await _writeRepository.GetByIdAsync(conversationId, cancellationToken))
            .BindAsync(async conversation =>
            {
                // Validate message content
                var validationResult = await _messageValidator.ValidateAsync(
                    command.Content, 
                    command.Role,
                    cancellationToken);
                
                if (validationResult.IsFailure)
                    return Result<Conversation>.Failure(validationResult.Error);

                return await UserId.Create(command.UserId)
                    .Bind(userId => MessageContent.Create(command.Content))
                    .Bind(content => conversation.AddMessage(
                        MessageId.New(),
                        userId,
                        content,
                        command.Role,
                        command.Attachments))
                    .Map(_ => conversation);
            })
            .BindAsync(async conversation =>
            {
                var updateResult = await _writeRepository.UpdateAsync(
                    conversation, 
                    cancellationToken);
                
                if (updateResult.IsFailure)
                    return Result<MessageDto>.Failure(updateResult.Error);

                // Publish events
                foreach (var domainEvent in conversation.DomainEvents)
                {
                    await _eventBus.PublishAsync(domainEvent, cancellationToken);
                }

                var message = conversation.Messages.Last();
                return Result.Success(_mapper.ToDto(message));
            });
    }
}
```

### 1.3 Process AI Response Command

```csharp
// Modules/Chat/Application/Commands/ProcessAiResponse/ProcessAiResponseCommand.cs
namespace Axon.Modules.Chat.Application.Commands.ProcessAiResponse;

public sealed record ProcessAiResponseCommand : ICommand<Result<AiResponseDto>>
{
    public required Guid ConversationId { get; init; }
    public required string UserMessage { get; init; }
    public AiModel Model { get; init; } = AiModel.GPT4;
    public Dictionary<string, object>? Parameters { get; init; }
}

// Modules/Chat/Application/Commands/ProcessAiResponse/ProcessAiResponseCommandHandler.cs
public sealed class ProcessAiResponseCommandHandler : ICommandHandler<ProcessAiResponseCommand, Result<AiResponseDto>>
{
    private readonly IConversationWriteRepository _writeRepository;
    private readonly IAiService _aiService;
    private readonly IAiResponseProcessor _responseProcessor;
    private readonly ITokenCalculator _tokenCalculator;
    private readonly IConversationMapper _mapper;
    private readonly IEventBus _eventBus;
    private readonly ILogger<ProcessAiResponseCommandHandler> _logger;

    public async Task<Result<AiResponseDto>> Handle(
        ProcessAiResponseCommand command,
        CancellationToken cancellationToken)
    {
        return await ConversationId.Create(command.ConversationId)
            .BindAsync(async id => await _writeRepository.GetByIdAsync(id, cancellationToken))
            .BindAsync(async conversation =>
            {
                // Add user message first
                var userMessageResult = await MessageContent.Create(command.UserMessage)
                    .Bind(content => conversation.AddMessage(
                        MessageId.New(),
                        conversation.StartedBy,
                        content,
                        MessageRole.User));

                if (userMessageResult.IsFailure)
                    return Result<AiResponseDto>.Failure(userMessageResult.Error);

                // Prepare context for AI
                var context = PrepareConversationContext(conversation);
                
                // Call AI service
                var aiResult = await _aiService.GenerateResponseAsync(
                    context,
                    command.Model,
                    command.Parameters,
                    cancellationToken);

                if (aiResult.IsFailure)
                    return Result<AiResponseDto>.Failure(aiResult.Error);

                var aiResponse = aiResult.Value;

                // Process AI response
                var processedResult = await _responseProcessor.ProcessAsync(
                    aiResponse.Content,
                    conversation.Id,
                    cancellationToken);

                if (processedResult.IsFailure)
                    return Result<AiResponseDto>.Failure(processedResult.Error);

                // Calculate tokens
                var tokenUsage = _tokenCalculator.Calculate(
                    context,
                    aiResponse.Content,
                    command.Model);

                // Add AI message to conversation
                var aiMessageResult = await MessageContent.Create(processedResult.Value)
                    .Bind(content => conversation.AddMessage(
                        MessageId.New(),
                        UserId.System,
                        content,
                        MessageRole.Assistant,
                        metadata: new Dictionary<string, object>
                        {
                            ["model"] = command.Model.ToString(),
                            ["tokens"] = tokenUsage.TotalTokens,
                            ["processing_time_ms"] = aiResponse.ProcessingTimeMs
                        }));

                if (aiMessageResult.IsFailure)
                    return Result<AiResponseDto>.Failure(aiMessageResult.Error);

                // Update conversation
                var updateResult = await _writeRepository.UpdateAsync(
                    conversation,
                    cancellationToken);

                if (updateResult.IsFailure)
                    return Result<AiResponseDto>.Failure(updateResult.Error);

                // Publish events
                foreach (var domainEvent in conversation.DomainEvents)
                {
                    await _eventBus.PublishAsync(domainEvent, cancellationToken);
                }

                _logger.LogInformation(
                    "Processed AI response for conversation {ConversationId} using {Model}",
                    conversation.Id.Value, command.Model);

                return Result.Success(new AiResponseDto
                {
                    ConversationId = conversation.Id.Value,
                    Content = processedResult.Value,
                    Model = command.Model.ToString(),
                    TokensUsed = tokenUsage.TotalTokens,
                    ProcessingTimeMs = aiResponse.ProcessingTimeMs
                });
            });
    }

    private ConversationContext PrepareConversationContext(Conversation conversation)
    {
        return new ConversationContext
        {
            Messages = conversation.Messages
                .Select(m => new ContextMessage
                {
                    Role = m.Role.ToString(),
                    Content = m.Content.Value,
                    Timestamp = m.SentAt
                })
                .ToList(),
            SystemPrompt = "You are a helpful AI assistant.",
            MaxTokens = 4000
        };
    }
}
```

### 1.4 Archive Conversation Command

```csharp
// Modules/Chat/Application/Commands/ArchiveConversation/ArchiveConversationCommand.cs
namespace Axon.Modules.Chat.Application.Commands.ArchiveConversation;

public sealed record ArchiveConversationCommand : ICommand<Result<Unit>>
{
    public required Guid ConversationId { get; init; }
    public required Guid UserId { get; init; }
    public string? Reason { get; init; }
}

// Modules/Chat/Application/Commands/ArchiveConversation/ArchiveConversationCommandHandler.cs
public sealed class ArchiveConversationCommandHandler : ICommandHandler<ArchiveConversationCommand, Result<Unit>>
{
    private readonly IConversationWriteRepository _writeRepository;
    private readonly IArchivePolicy _archivePolicy;
    private readonly IEventBus _eventBus;

    public async Task<Result<Unit>> Handle(
        ArchiveConversationCommand command,
        CancellationToken cancellationToken)
    {
        return await ConversationId.Create(command.ConversationId)
            .BindAsync(async id => await _writeRepository.GetByIdAsync(id, cancellationToken))
            .BindAsync(async conversation =>
            {
                // Check if user can archive
                if (conversation.StartedBy.Value != command.UserId && 
                    !conversation.Participants.Any(p => p.UserId.Value == command.UserId))
                {
                    return Result<Conversation>.Failure(
                        ConversationErrors.UnauthorizedAccess(command.ConversationId));
                }

                // Apply archive policy
                var policyResult = await _archivePolicy.CanArchiveAsync(
                    conversation,
                    cancellationToken);

                if (policyResult.IsFailure)
                    return Result<Conversation>.Failure(policyResult.Error);

                // Archive conversation
                var archiveResult = conversation.Archive(command.Reason);
                if (archiveResult.IsFailure)
                    return Result<Conversation>.Failure(archiveResult.Error);

                return Result.Success(conversation);
            })
            .BindAsync(async conversation =>
            {
                var updateResult = await _writeRepository.UpdateAsync(
                    conversation,
                    cancellationToken);

                if (updateResult.IsFailure)
                    return Result<Unit>.Failure(updateResult.Error);

                // Publish events
                foreach (var domainEvent in conversation.DomainEvents)
                {
                    await _eventBus.PublishAsync(domainEvent, cancellationToken);
                }

                return Result.Success(Unit.Value);
            });
    }
}
```

## 2. Queries - State Reads

### 2.1 Get Conversation Query

```csharp
// Modules/Chat/Application/Queries/GetConversation/GetConversationQuery.cs
namespace Axon.Modules.Chat.Application.Queries.GetConversation;

public sealed record GetConversationQuery : IQuery<Result<ConversationDetailDto>>
{
    public required Guid ConversationId { get; init; }
    public required Guid UserId { get; init; }
    public bool IncludeMessages { get; init; } = true;
}

// Modules/Chat/Application/Queries/GetConversation/GetConversationQueryHandler.cs
public sealed class GetConversationQueryHandler : IQueryHandler<GetConversationQuery, Result<ConversationDetailDto>>
{
    private readonly IConversationReadRepository _readRepository;
    private readonly IConversationMapper _mapper;
    private readonly ICacheService _cache;
    private readonly ILogger<GetConversationQueryHandler> _logger;

    public async Task<Result<ConversationDetailDto>> Handle(
        GetConversationQuery query,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"conversation:{query.ConversationId}:detail:{query.IncludeMessages}";
        
        // Try cache first
        var cached = await _cache.GetAsync<ConversationDetailDto>(cacheKey, cancellationToken);
        if (cached.IsSome)
        {
            _logger.LogDebug("Cache hit for conversation {ConversationId}", query.ConversationId);
            return Result.Success(cached.Value);
        }

        // Load from repository
        return await ConversationId.Create(query.ConversationId)
            .BindAsync(async id => await _readRepository.GetByIdAsync(id, cancellationToken))
            .BindAsync(async conversation =>
            {
                // Check access
                if (!HasAccess(conversation, query.UserId))
                {
                    return Result<ConversationDetailDto>.Failure(
                        ConversationErrors.UnauthorizedAccess(query.ConversationId));
                }

                var dto = _mapper.ToDetailDto(conversation, query.IncludeMessages);
                
                // Cache result
                await _cache.SetAsync(
                    cacheKey, 
                    dto, 
                    TimeSpan.FromMinutes(5), 
                    cancellationToken);

                return Result.Success(dto);
            });
    }

    private bool HasAccess(ConversationReadModel conversation, Guid userId)
    {
        return conversation.StartedBy == userId ||
               conversation.Participants.Any(p => p.UserId == userId);
    }
}
```

### 2.2 List User Conversations Query

```csharp
// Modules/Chat/Application/Queries/ListUserConversations/ListUserConversationsQuery.cs
namespace Axon.Modules.Chat.Application.Queries.ListUserConversations;

public sealed record ListUserConversationsQuery : IQuery<Result<PagedResult<ConversationSummaryDto>>>
{
    public required Guid UserId { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public ConversationStatus? Status { get; init; }
    public DateTime? Since { get; init; }
    public string? SortBy { get; init; } = "LastMessageAt";
    public bool Descending { get; init; } = true;
}

// Modules/Chat/Application/Queries/ListUserConversations/ListUserConversationsQueryHandler.cs
public sealed class ListUserConversationsQueryHandler : 
    IQueryHandler<ListUserConversationsQuery, Result<PagedResult<ConversationSummaryDto>>>
{
    private readonly IConversationReadRepository _readRepository;
    private readonly IConversationMapper _mapper;
    private readonly ICacheService _cache;

    public async Task<Result<PagedResult<ConversationSummaryDto>>> Handle(
        ListUserConversationsQuery query,
        CancellationToken cancellationToken)
    {
        var cacheKey = GenerateCacheKey(query);
        
        // Try cache
        var cached = await _cache.GetAsync<PagedResult<ConversationSummaryDto>>(
            cacheKey, 
            cancellationToken);
            
        if (cached.IsSome)
            return Result.Success(cached.Value);

        // Build specification
        var spec = new UserConversationsSpecification(
            UserId.Create(query.UserId).Value,
            query.Status,
            query.Since,
            query.PageNumber,
            query.PageSize,
            query.SortBy,
            query.Descending);

        // Query repository
        var result = await _readRepository.GetPagedAsync(spec, cancellationToken);
        
        if (result.IsFailure)
            return Result<PagedResult<ConversationSummaryDto>>.Failure(result.Error);

        var pagedResult = new PagedResult<ConversationSummaryDto>
        {
            Items = result.Value.Items.Select(_mapper.ToSummaryDto).ToList(),
            PageNumber = result.Value.PageNumber,
            PageSize = result.Value.PageSize,
            TotalCount = result.Value.TotalCount,
            TotalPages = result.Value.TotalPages
        };

        // Cache result
        await _cache.SetAsync(
            cacheKey,
            pagedResult,
            TimeSpan.FromMinutes(2),
            cancellationToken);

        return Result.Success(pagedResult);
    }

    private string GenerateCacheKey(ListUserConversationsQuery query)
    {
        return $"conversations:user:{query.UserId}:" +
               $"page:{query.PageNumber}:size:{query.PageSize}:" +
               $"status:{query.Status}:since:{query.Since?.Ticks}:" +
               $"sort:{query.SortBy}:{query.Descending}";
    }
}
```

### 2.3 Search Conversations Query

```csharp
// Modules/Chat/Application/Queries/SearchConversations/SearchConversationsQuery.cs
namespace Axon.Modules.Chat.Application.Queries.SearchConversations;

public sealed record SearchConversationsQuery : IQuery<Result<SearchResult<ConversationSearchDto>>>
{
    public required string SearchTerm { get; init; }
    public required Guid UserId { get; init; }
    public List<ConversationStatus>? StatusFilters { get; init; }
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }
    public int MaxResults { get; init; } = 50;
}

// Modules/Chat/Application/Queries/SearchConversations/SearchConversationsQueryHandler.cs
public sealed class SearchConversationsQueryHandler : 
    IQueryHandler<SearchConversationsQuery, Result<SearchResult<ConversationSearchDto>>>
{
    private readonly IConversationSearchService _searchService;
    private readonly IConversationMapper _mapper;

    public async Task<Result<SearchResult<ConversationSearchDto>>> Handle(
        SearchConversationsQuery query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            return Result<SearchResult<ConversationSearchDto>>.Failure(
                ValidationErrors.InvalidSearchTerm());
        }

        var searchRequest = new ConversationSearchRequest
        {
            UserId = UserId.Create(query.UserId).Value,
            SearchTerm = query.SearchTerm,
            StatusFilters = query.StatusFilters,
            DateFrom = query.DateFrom,
            DateTo = query.DateTo,
            MaxResults = query.MaxResults
        };

        var searchResult = await _searchService.SearchAsync(
            searchRequest,
            cancellationToken);

        if (searchResult.IsFailure)
            return Result<SearchResult<ConversationSearchDto>>.Failure(searchResult.Error);

        var results = new SearchResult<ConversationSearchDto>
        {
            Query = query.SearchTerm,
            TotalHits = searchResult.Value.TotalHits,
            Results = searchResult.Value.Results
                .Select(_mapper.ToSearchDto)
                .ToList(),
            Facets = searchResult.Value.Facets,
            ExecutionTimeMs = searchResult.Value.ExecutionTimeMs
        };

        return Result.Success(results);
    }
}
```

## 3. DTOs and Mapping

### 3.1 DTOs

```csharp
// Modules/Chat/Application/DTOs/ConversationDto.cs
namespace Axon.Modules.Chat.Application.DTOs;

public sealed record ConversationDto
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required string Status { get; init; }
    public required Guid StartedBy { get; init; }
    public required DateTime StartedAt { get; init; }
    public DateTime? LastMessageAt { get; init; }
    public int MessageCount { get; init; }
    public int ParticipantCount { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}

public sealed record ConversationDetailDto : ConversationDto
{
    public List<MessageDto> Messages { get; init; } = [];
    public List<ParticipantDto> Participants { get; init; } = [];
    public DateTime? ArchivedAt { get; init; }
    public string? ArchiveReason { get; init; }
    public TokenUsageDto? TokenUsage { get; init; }
}

public sealed record ConversationSummaryDto
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required string Status { get; init; }
    public DateTime LastMessageAt { get; init; }
    public string? LastMessagePreview { get; init; }
    public int UnreadCount { get; init; }
}

public sealed record MessageDto
{
    public required Guid Id { get; init; }
    public required Guid ConversationId { get; init; }
    public required Guid UserId { get; init; }
    public required string Content { get; init; }
    public required string Role { get; init; }
    public required DateTime SentAt { get; init; }
    public DateTime? EditedAt { get; init; }
    public List<string>? Attachments { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}

public sealed record ParticipantDto
{
    public required Guid UserId { get; init; }
    public required DateTime JoinedAt { get; init; }
    public DateTime? LeftAt { get; init; }
    public required string Role { get; init; }
}

public sealed record TokenUsageDto
{
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public int TotalTokens { get; init; }
    public decimal EstimatedCost { get; init; }
}

public sealed record AiResponseDto
{
    public required Guid ConversationId { get; init; }
    public required string Content { get; init; }
    public required string Model { get; init; }
    public int TokensUsed { get; init; }
    public long ProcessingTimeMs { get; init; }
}
```

### 3.2 Mapper Implementation

```csharp
// Modules/Chat/Application/Mapping/ConversationMapper.cs
namespace Axon.Modules.Chat.Application.Mapping;

public interface IConversationMapper
{
    ConversationDto ToDto(Conversation conversation);
    ConversationDetailDto ToDetailDto(Conversation conversation, bool includeMessages = true);
    ConversationSummaryDto ToSummaryDto(ConversationReadModel conversation);
    MessageDto ToDto(Message message);
    ConversationSearchDto ToSearchDto(ConversationSearchResult result);
}

public sealed class ConversationMapper : IConversationMapper
{
    public ConversationDto ToDto(Conversation conversation)
    {
        return new ConversationDto
        {
            Id = conversation.Id.Value,
            Title = conversation.Title.Value,
            Status = conversation.Status.ToString(),
            StartedBy = conversation.StartedBy.Value,
            StartedAt = conversation.StartedAt,
            LastMessageAt = conversation.LastMessageAt,
            MessageCount = conversation.Messages.Count,
            ParticipantCount = conversation.Participants.Count,
            Metadata = conversation.Metadata
        };
    }

    public ConversationDetailDto ToDetailDto(Conversation conversation, bool includeMessages = true)
    {
        var dto = new ConversationDetailDto
        {
            Id = conversation.Id.Value,
            Title = conversation.Title.Value,
            Status = conversation.Status.ToString(),
            StartedBy = conversation.StartedBy.Value,
            StartedAt = conversation.StartedAt,
            LastMessageAt = conversation.LastMessageAt,
            MessageCount = conversation.Messages.Count,
            ParticipantCount = conversation.Participants.Count,
            Metadata = conversation.Metadata,
            Participants = conversation.Participants
                .Select(p => new ParticipantDto
                {
                    UserId = p.UserId.Value,
                    JoinedAt = p.JoinedAt,
                    LeftAt = p.LeftAt,
                    Role = p.Role.ToString()
                })
                .ToList()
        };

        if (includeMessages)
        {
            dto.Messages = conversation.Messages
                .Select(ToDto)
                .ToList();
        }

        if (conversation.Status == ConversationStatus.Archived)
        {
            dto.ArchivedAt = conversation.ArchivedAt;
            dto.ArchiveReason = conversation.ArchiveReason;
        }

        if (conversation.TokenUsage != null)
        {
            dto.TokenUsage = new TokenUsageDto
            {
                InputTokens = conversation.TokenUsage.InputTokens,
                OutputTokens = conversation.TokenUsage.OutputTokens,
                TotalTokens = conversation.TokenUsage.TotalTokens,
                EstimatedCost = conversation.TokenUsage.EstimatedCost
            };
        }

        return dto;
    }

    public MessageDto ToDto(Message message)
    {
        return new MessageDto
        {
            Id = message.Id.Value,
            ConversationId = message.ConversationId.Value,
            UserId = message.UserId.Value,
            Content = message.Content.Value,
            Role = message.Role.ToString(),
            SentAt = message.SentAt,
            EditedAt = message.EditedAt,
            Attachments = message.Attachments,
            Metadata = message.Metadata
        };
    }

    public ConversationSummaryDto ToSummaryDto(ConversationReadModel conversation)
    {
        return new ConversationSummaryDto
        {
            Id = conversation.Id,
            Title = conversation.Title,
            Status = conversation.Status,
            LastMessageAt = conversation.LastMessageAt,
            LastMessagePreview = conversation.LastMessage?.Substring(0, Math.Min(100, conversation.LastMessage.Length)),
            UnreadCount = conversation.UnreadCount
        };
    }

    public ConversationSearchDto ToSearchDto(ConversationSearchResult result)
    {
        return new ConversationSearchDto
        {
            Id = result.Id,
            Title = result.Title,
            Status = result.Status,
            MatchedContent = result.MatchedContent,
            Highlights = result.Highlights,
            Score = result.Score
        };
    }
}
```

## 4. Pipeline Behaviors

### 4.1 Validation Behavior

```csharp
// Modules/Chat/Application/Behaviors/ValidationBehavior.cs
namespace Axon.Modules.Chat.Application.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IResult
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    private readonly ILogger<ValidationBehavior<TRequest, TResponse>> _logger;

    public ValidationBehavior(
        IEnumerable<IValidator<TRequest>> validators,
        ILogger<ValidationBehavior<TRequest, TResponse>> logger)
    {
        _validators = validators;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);
        
        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count != 0)
        {
            _logger.LogWarning(
                "Validation failed for {RequestType}: {Errors}",
                typeof(TRequest).Name,
                string.Join(", ", failures.Select(f => f.ErrorMessage)));

            var error = ValidationError.Multiple(
                failures.Select(f => new ValidationError(f.PropertyName, f.ErrorMessage)));

            // Create failure result - this is a bit tricky with generics
            var resultType = typeof(TResponse).GetGenericArguments()[0];
            var failureMethod = typeof(Result<>)
                .MakeGenericType(resultType)
                .GetMethod("Failure", new[] { typeof(Error) });
                
            return (TResponse)failureMethod!.Invoke(null, new object[] { error })!;
        }

        return await next();
    }
}
```

### 4.2 Logging Behavior

```csharp
// Modules/Chat/Application/Behaviors/LoggingBehavior.cs
namespace Axon.Modules.Chat.Application.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
    private readonly ICurrentUserService _currentUserService;

    public LoggingBehavior(
        ILogger<LoggingBehavior<TRequest, TResponse>> logger,
        ICurrentUserService currentUserService)
    {
        _logger = logger;
        _currentUserService = currentUserService;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var userId = _currentUserService.UserId;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Handling {RequestName} for user {UserId}",
            requestName, userId);

        try
        {
            var response = await next();
            
            stopwatch.Stop();
            
            _logger.LogInformation(
                "Handled {RequestName} for user {UserId} in {ElapsedMs}ms",
                requestName, userId, stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _logger.LogError(ex,
                "Error handling {RequestName} for user {UserId} after {ElapsedMs}ms",
                requestName, userId, stopwatch.ElapsedMilliseconds);
                
            throw;
        }
    }
}
```

### 4.3 Caching Behavior

```csharp
// Modules/Chat/Application/Behaviors/CachingBehavior.cs
namespace Axon.Modules.Chat.Application.Behaviors;

public sealed class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, ICacheableQuery
{
    private readonly ICacheService _cache;
    private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;

    public CachingBehavior(ICacheService cache, ILogger<CachingBehavior<TRequest, TResponse>> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!request.BypassCache)
        {
            var cacheKey = request.CacheKey;
            var cached = await _cache.GetAsync<TResponse>(cacheKey, cancellationToken);
            
            if (cached.IsSome)
            {
                _logger.LogDebug("Cache hit for key {CacheKey}", cacheKey);
                return cached.Value;
            }
        }

        var response = await next();

        if (request.CacheTime > TimeSpan.Zero)
        {
            await _cache.SetAsync(
                request.CacheKey,
                response,
                request.CacheTime,
                cancellationToken);
                
            _logger.LogDebug(
                "Cached response for key {CacheKey} with TTL {TTL}",
                request.CacheKey, request.CacheTime);
        }

        return response;
    }
}

public interface ICacheableQuery
{
    string CacheKey { get; }
    TimeSpan CacheTime { get; }
    bool BypassCache { get; }
}
```

### 4.4 Performance Monitoring Behavior

```csharp
// Modules/Chat/Application/Behaviors/PerformanceBehavior.cs
namespace Axon.Modules.Chat.Application.Behaviors;

public sealed class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IMetrics _metrics;
    private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;
    private const int SlowRequestThresholdMs = 500;

    public PerformanceBehavior(IMetrics metrics, ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    {
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        var requestName = typeof(TRequest).Name;

        try
        {
            var response = await next();
            
            timer.Stop();
            
            RecordMetrics(requestName, timer.ElapsedMilliseconds, true);

            if (timer.ElapsedMilliseconds > SlowRequestThresholdMs)
            {
                _logger.LogWarning(
                    "Slow request detected: {RequestName} took {ElapsedMs}ms",
                    requestName, timer.ElapsedMilliseconds);
            }

            return response;
        }
        catch (Exception)
        {
            timer.Stop();
            RecordMetrics(requestName, timer.ElapsedMilliseconds, false);
            throw;
        }
    }

    private void RecordMetrics(string requestName, long elapsedMs, bool success)
    {
        _metrics.RecordRequestDuration(requestName, elapsedMs);
        _metrics.IncrementRequestCount(requestName, success ? "success" : "failure");
        
        if (!success)
        {
            _metrics.IncrementErrorCount(requestName);
        }
    }
}
```

## 5. Application Services

### 5.1 AI Service Interface

```csharp
// Modules/Chat/Application/Services/IAiService.cs
namespace Axon.Modules.Chat.Application.Services;

public interface IAiService
{
    Task<Result<AiResponse>> GenerateResponseAsync(
        ConversationContext context,
        AiModel model,
        Dictionary<string, object>? parameters,
        CancellationToken cancellationToken);
}

public sealed record AiResponse
{
    public required string Content { get; init; }
    public required long ProcessingTimeMs { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}

public sealed record ConversationContext
{
    public required List<ContextMessage> Messages { get; init; }
    public required string SystemPrompt { get; init; }
    public int MaxTokens { get; init; } = 4000;
}

public sealed record ContextMessage
{
    public required string Role { get; init; }
    public required string Content { get; init; }
    public DateTime Timestamp { get; init; }
}
```

### 5.2 Token Calculator

```csharp
// Modules/Chat/Application/Services/ITokenCalculator.cs
namespace Axon.Modules.Chat.Application.Services;

public interface ITokenCalculator
{
    TokenUsage Calculate(ConversationContext context, string response, AiModel model);
}

public sealed class TokenCalculator : ITokenCalculator
{
    private readonly Dictionary<AiModel, decimal> _costPerToken = new()
    {
        [AiModel.GPT4] = 0.00003m,
        [AiModel.GPT35Turbo] = 0.000002m,
        [AiModel.Claude3] = 0.000025m
    };

    public TokenUsage Calculate(ConversationContext context, string response, AiModel model)
    {
        // Simple estimation - real implementation would use proper tokenizer
        var inputTokens = EstimateTokens(SerializeContext(context));
        var outputTokens = EstimateTokens(response);
        var totalTokens = inputTokens + outputTokens;
        var estimatedCost = totalTokens * _costPerToken.GetValueOrDefault(model, 0.00001m);

        return new TokenUsage(inputTokens, outputTokens, totalTokens, estimatedCost);
    }

    private int EstimateTokens(string text)
    {
        // Rough estimation: 1 token ≈ 4 characters
        return (int)Math.Ceiling(text.Length / 4.0);
    }

    private string SerializeContext(ConversationContext context)
    {
        return string.Join("\n", context.Messages.Select(m => $"{m.Role}: {m.Content}"));
    }
}
```

### 5.3 Cache Service

```csharp
// Modules/Chat/Application/Services/ICacheService.cs
namespace Axon.Modules.Chat.Application.Services;

public interface ICacheService
{
    Task<Option<T>> GetAsync<T>(string key, CancellationToken cancellationToken);
    Task SetAsync<T>(string key, T value, TimeSpan expiry, CancellationToken cancellationToken);
    Task RemoveAsync(string key, CancellationToken cancellationToken);
    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken);
}
```

### 5.4 Search Service

```csharp
// Modules/Chat/Application/Services/IConversationSearchService.cs
namespace Axon.Modules.Chat.Application.Services;

public interface IConversationSearchService
{
    Task<Result<ConversationSearchResponse>> SearchAsync(
        ConversationSearchRequest request,
        CancellationToken cancellationToken);
}

public sealed record ConversationSearchRequest
{
    public required UserId UserId { get; init; }
    public required string SearchTerm { get; init; }
    public List<ConversationStatus>? StatusFilters { get; init; }
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }
    public int MaxResults { get; init; } = 50;
}

public sealed record ConversationSearchResponse
{
    public required string Query { get; init; }
    public required int TotalHits { get; init; }
    public required List<ConversationSearchResult> Results { get; init; }
    public Dictionary<string, List<FacetValue>>? Facets { get; init; }
    public required long ExecutionTimeMs { get; init; }
}

public sealed record ConversationSearchResult
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required string Status { get; init; }
    public required string MatchedContent { get; init; }
    public List<string> Highlights { get; init; } = [];
    public required float Score { get; init; }
}
```

## 6. Registration and Configuration

```csharp
// Modules/Chat/Application/ApplicationModule.cs
namespace Axon.Modules.Chat.Application;

public static class ApplicationModule
{
    public static IServiceCollection AddChatApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register MediatR
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(ApplicationModule).Assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(PerformanceBehavior<,>));
            cfg.AddOpenBehavior(typeof(CachingBehavior<,>));
        });

        // Register validators
        services.AddValidatorsFromAssembly(typeof(ApplicationModule).Assembly);

        // Register mappers
        services.AddSingleton<IConversationMapper, ConversationMapper>();

        // Register application services
        services.AddScoped<ITokenCalculator, TokenCalculator>();

        return services;
    }
}
```

## Summary

This Application Layer implementation provides:

1. **Complete Railway-Oriented CQRS**: Every handler returns `Result<T>` with proper error propagation
2. **Rich Command Handlers**: Handle complex business operations with full validation
3. **Optimized Query Handlers**: Implement caching and efficient data retrieval
4. **Pipeline Behaviors**: Cross-cutting concerns handled consistently
5. **Clean DTOs**: Proper boundaries between layers
6. **Comprehensive Mapping**: Transform between domain and DTOs

The layer is ready for:
- Integration with Infrastructure layer
- API endpoint implementation
- Future event sourcing migration
- Performance optimization
- Observability integration