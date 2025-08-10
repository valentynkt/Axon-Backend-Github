using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Conversation;
using Axon.Modules.Chat.Domain.Conversation.ValueObjects;
using Axon.Modules.Chat.Domain.Specifications;
using Axon.Modules.Chat.Domain.ValueObjects;
using BuildingBlocks.Core.Abstractions.Pagination;
using BuildingBlocks.Core.Diagnostics;
using BuildingBlocks.Core.Domain.CQRS;
using BuildingBlocks.Core.Domain.Specifications;
using BuildingBlocks.Core.Domain.Specifications.CommonSpecs;
using BuildingBlocks.Core.Functional.Results;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Application.Queries.GetConversationHistory;

/// <summary>
/// Epic 2 enhanced query demonstrating specifications with Epic 5 caching integration.
/// This query will go through the Epic 5 pipeline with caching support:
/// 
/// 1. ObservabilityBehavior - telemetry and tracing
/// 2. LoggingBehavior - structured logging  
/// 3. ValidationBehavior - FluentValidation
/// 4. CachingBehavior - cache check/store using Epic 2 cache keys and tags
/// 5. RetryBehavior - retry on failures
/// 6. Handler execution with Epic 2 specifications
/// </summary>
public sealed record GetConversationHistoryQueryEpic2 : CacheableDomainQueryBase<PagedResult<ConversationHistoryDto>>
{
    public UserId UserId { get; init; } = default!;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public string? SearchTerm { get; init; }
    public ConversationStatus? Status { get; init; }

    public override string GetCacheKey()
    {
        return $"ConversationHistory:{UserId}:{Page}:{PageSize}:{FromDate:yyyyMMdd}:{ToDate:yyyyMMdd}:{SearchTerm}:{Status}";
    }

    public override TimeSpan? GetCacheDuration() => TimeSpan.FromMinutes(5);

    public override IEnumerable<string> GetCacheTags() 
    {
        yield return $"User:{UserId}";
        yield return "Conversations";
        yield return "ConversationHistory";
    }
}

/// <summary>
/// FluentValidation validator for structural validation.
/// </summary>
public sealed class GetConversationHistoryQueryEpic2Validator : AbstractValidator<GetConversationHistoryQueryEpic2>
{
    public GetConversationHistoryQueryEpic2Validator()
    {
        RuleFor(x => x.UserId)
            .NotNull()
            .WithMessage("User ID is required");

        RuleFor(x => x.Page)
            .GreaterThan(0)
            .WithMessage("Page must be greater than 0");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100");

        RuleFor(x => x.FromDate)
            .LessThan(x => x.ToDate)
            .WithMessage("From date must be before to date")
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue);

        RuleFor(x => x.SearchTerm)
            .MaximumLength(100)
            .WithMessage("Search term cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.SearchTerm));
    }
}

/// <summary>
/// Epic 2 query handler demonstrating specifications with caching.
/// Shows proper use of Epic 2 specifications for complex queries.
/// </summary>
public sealed class GetConversationHistoryQueryEpic2Handler : IRequestHandler<GetConversationHistoryQueryEpic2, Result<PagedResult<ConversationHistoryDto>>>
{
    private readonly IConversationReadRepository _conversationRepository;
    private readonly ILogger<GetConversationHistoryQueryEpic2Handler> _logger;

    public GetConversationHistoryQueryEpic2Handler(
        IConversationReadRepository conversationRepository,
        ILogger<GetConversationHistoryQueryEpic2Handler> logger)
    {
        _conversationRepository = conversationRepository;
        _logger = logger;
    }

    public async Task<Result<PagedResult<ConversationHistoryDto>>> Handle(
        GetConversationHistoryQueryEpic2 request, 
        CancellationToken cancellationToken)
    {
        // CachingBehavior will check cache first
        // This handler only runs if cache miss occurs

        _logger.LogDebug(
            "Fetching conversation history for user {UserId}, page {Page}, size {PageSize}",
            request.UserId, 
            request.Page, 
            request.PageSize);

        try
        {
            // Build Epic 2 specification using composition
            var specification = BuildSpecification(request);

            // Execute query with specification
            var conversations = await _conversationRepository.FindPagedAsync(
                specification, 
                request.Page, 
                request.PageSize, 
                cancellationToken);

            // Map to DTOs
            var dtos = conversations.Items.Select(MapToDto).ToList();

            var result = new PagedResult<ConversationHistoryDto>
            {
                Items = dtos,
                TotalCount = conversations.TotalCount,
                Page = conversations.Page,
                PageSize = conversations.PageSize,
                TotalPages = conversations.TotalPages
            };

            _logger.LogDebug(
                "Found {Count} conversations for user {UserId}",
                dtos.Count, 
                request.UserId);

            return Result<PagedResult<ConversationHistoryDto>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error fetching conversation history for user {UserId}",
                request.UserId);

            return Result<PagedResult<ConversationHistoryDto>>.Failure(
                Error.Unexpected("Failed to retrieve conversation history", "CONVERSATION_HISTORY_ERROR"));
        }
    }

    /// <summary>
    /// Builds Epic 2 specification using composition pattern.
    /// Demonstrates proper use of specification chaining.
    /// </summary>
    private static Specification<Conversation> BuildSpecification(GetConversationHistoryQueryEpic2 request)
    {
        // Start with user filter (always required)
        Specification<Conversation> spec = new ConversationsByOwnerSpec(request.UserId);

        // Add optional filters using specification composition
        if (request.Status.HasValue)
        {
            spec = spec.And(new ConversationsByStatusSpec(request.Status.Value));
        }
        else
        {
            // Default to active conversations if no status specified
            spec = spec.And(CommonSpecifications.Active<Conversation>());
        }

        if (request.FromDate.HasValue || request.ToDate.HasValue)
        {
            var fromDate = request.FromDate ?? DateTime.MinValue;
            var toDate = request.ToDate ?? DateTime.MaxValue;
            spec = spec.And(new ConversationsCreatedBetweenSpec(fromDate, toDate));
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            spec = spec.And(new ConversationTitleSearchSpec(request.SearchTerm));
        }

        return spec;
    }

    /// <summary>
    /// Maps domain model to DTO.
    /// </summary>
    private ConversationHistoryDto MapToDto(Conversation conversation)
    {
        return new ConversationHistoryDto
        {
            Id = conversation.Id.Value,
            Title = conversation.Title,
            Status = conversation.Status,
            MessageCount = conversation.MessageCount,
            CreatedAt = conversation.CreatedAt,
            UpdatedAt = conversation.UpdatedAt,
            CompletedAt = conversation.CompletedAt,
            LastMessage = GetLastMessagePreview(conversation)
        };
    }

    /// <summary>
    /// Gets preview of last message for display in history.
    /// </summary>
    private static string? GetLastMessagePreview(Conversation conversation)
    {
        var lastMessage = conversation.Messages.LastOrDefault();
        return lastMessage?.GetPreview(100);
    }
}

/// <summary>
/// DTO for conversation history display.
/// </summary>
public sealed record ConversationHistoryDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public ConversationStatus Status { get; init; }
    public int MessageCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? LastMessage { get; init; }
}

/// <summary>
/// Repository interface for conversation read operations.
/// </summary>
public interface IConversationReadRepository
{
    Task<PagedResult<Conversation>> FindPagedAsync(
        Specification<Conversation> specification,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<List<Conversation>> FindAsync(
        Specification<Conversation> specification,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        Specification<Conversation> specification,
        CancellationToken cancellationToken = default);
}