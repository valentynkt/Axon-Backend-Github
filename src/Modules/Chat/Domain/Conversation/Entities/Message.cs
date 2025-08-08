using Axon.Modules.Chat.Domain.Conversation.ValueObjects;
using Axon.Shared.Common;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional.Validation;

namespace Axon.Modules.Chat.Domain.Conversation.Entities;

/// <summary>
/// Message entity (child of Conversation aggregate).
/// Minimal, immutable-after-create state; no domain events here (Conversation raises them).
/// </summary>
public sealed record Message : BaseAuditableEntity<MessageId>
{
    /// <summary>Owning conversation.</summary>
    public ConversationId ConversationId { get; private set; }

    /// <summary>Actor role (e.g., User). Conversation enforces who may append.</summary>
    public MessageRole Role { get; private set; }

    /// <summary>Plain text content. Trimmed; max 100k chars.</summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>
    /// 1-based, monotonically increasing ordinal inside its Conversation
    /// (assigned by Conversation).
    /// </summary>
    public int Sequence { get; private set; }

    /// <summary>Optional metadata bag (domain-agnostic; infra maps to JSONB).</summary>
    public Dictionary<string, object?>? Metadata { get; private set; }

    // EF Core / serializers
    private Message() { ConversationId = default!; Role = default!; }

    private Message(
        MessageId id,
        ConversationId conversationId,
        MessageRole role,
        string content,
        int sequence,
        Dictionary<string, object?>? metadata)
    {
        Id = id;
        ConversationId = conversationId;
        Role = role;
        Content = content;
        Sequence = sequence;
        Metadata = metadata;
    }

    /// <summary>
    /// Factory: validates content and sequence; Conversation controls ownership/role.
    /// </summary>
    public static Result<Message> Create(
        string content,
        ConversationId conversationId,
        MessageRole role,
        int sequence,
        Dictionary<string, object?>? metadata = null)
    {
        content = (content ?? string.Empty).Trim();
        if (content.Length == 0)
            return Error.Validation("Content cannot be empty.");
        if (content.Length > 100_000)
            return Error.Validation("Content cannot exceed 100,000 characters.");
        if (sequence <= 0)
            return Error.Validation("Sequence must be a positive integer.");

        var message = new Message(
            id: MessageId.New(),
            conversationId: conversationId,
            role: role,
            content: content,
            sequence: sequence,
            metadata: metadata);

        return Result<Message>.Success(message);
    }

    /// <summary>
    /// Epic 2 entity validation implementation.
    /// </summary>
    public override Validation<Unit> Validate()
    {
        var errors = new List<Error>();
        
        // Core entity validation
        if (ConversationId == null || ConversationId.Value == Guid.Empty)
            errors.Add(Error.Validation("Message must belong to a conversation", "MESSAGE_NO_CONVERSATION"));
            
        if (Role == null)
            errors.Add(Error.Validation("Message must have a role", "MESSAGE_NO_ROLE"));
            
        if (Content == null)
        {
            errors.Add(Error.Validation("Message must have content", "MESSAGE_NO_CONTENT"));
        }
        else
        {
            // Validate content using value object validation
            var contentValidation = Content.Validate();
            if (contentValidation.IsInvalid)
                errors.AddRange(contentValidation.Errors);
        }
        
        if (Sequence <= 0)
            errors.Add(Error.Validation("Message sequence must be positive", "MESSAGE_INVALID_SEQUENCE"));
            
        // Metadata validation
        if (Metadata != null && Metadata.Count > 100)
            errors.Add(Error.Validation("Message cannot have more than 100 metadata entries", "MESSAGE_TOO_MUCH_METADATA"));
        
        return errors.Any() 
            ? Validation<Unit>.Invalid(errors)
            : Validation<Unit>.Valid(Unit.Value);
    }
    
    /// <summary>
    /// Gets message preview for display purposes.
    /// </summary>
    public string GetPreview(int maxLength = 100)
    {
        return Content.GetPreview(maxLength);
    }
    
    /// <summary>
    /// Gets message content metrics.
    /// </summary>
    public MessageContentMetrics GetContentMetrics()
    {
        return Content.GetMetrics();
    }
    
    /// <summary>
    /// Checks if message contains specific text.
    /// </summary>
    public bool Contains(string searchText, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return false;
            
        return Content.Value.Contains(searchText, comparison);
    }
    
    /// <summary>
    /// Updates metadata - only operation allowed after creation.
    /// </summary>
    public Result<Message> WithUpdatedMetadata(Dictionary<string, object?> newMetadata)
    {
        if (newMetadata.Count > 100)
            return Result<Message>.Failure(
                Error.Validation("Message cannot have more than 100 metadata entries", "MESSAGE_TOO_MUCH_METADATA"));
        
        return Result<Message>.Success(this with { Metadata = newMetadata });
    }
}
