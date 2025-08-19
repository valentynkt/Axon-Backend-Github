using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.Events;
using Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Time;
using BuildingBlocks.Core.Abstractions.Time;
using Axon.Modules.Chat.Domain.ValueObjects;
using BuildingBlocks.Core.Domain.Events;
using BuildingBlocks.Core.Domain.Primitives;
using BuildingBlocks.Core.Functional.Results;

namespace Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Builders;

/// <summary>
/// Fluent builder for creating conversations in tests with ergonomic API.
/// Provides defaults and safety while allowing full customization.
/// All operations go through aggregate methods (no state poking).
/// </summary>
public sealed class ConversationBuilder
{
    private readonly UserId _ownerId;
    private readonly IClock _clock;
    private string? _title;
    private Conversation? _conversation;
    
    // Track events for quick assertions
    private readonly List<IDomainEvent> _capturedEvents = new();

    // Default to stable test values
    public ConversationBuilder(
        UserId? ownerId = null, 
        IClock? clock = null, 
        string? title = null)
    {
        _ownerId = ownerId ?? UserId.New();
        _clock = clock ?? FixedClock.At("2025-01-01T00:00:00Z");
        _title = title; // null = empty title (default behavior)
    }

    /// <summary>
    /// Creates a new builder with specified owner.
    /// </summary>
    public static ConversationBuilder WithOwner(UserId ownerId) => new(ownerId);

    /// <summary>
    /// Creates a new builder with specified clock.
    /// </summary>
    public static ConversationBuilder WithClock(IClock clock) => new(clock: clock);

    /// <summary>
    /// Creates a new builder with specified title.
    /// </summary>
    public static ConversationBuilder WithTitle(string? title) => new(title: title);

    /// <summary>
    /// Creates a new builder with all defaults.
    /// </summary>
    public static ConversationBuilder New() => new();

    /// <summary>
    /// Updates the owner for this builder.
    /// </summary>
    public ConversationBuilder SetOwner(UserId ownerId)
    {
        if (_conversation != null)
            throw new InvalidOperationException("Cannot change owner after conversation is started");
        return new ConversationBuilder(ownerId, _clock, _title);
    }

    /// <summary>
    /// Updates the clock for this builder.
    /// </summary>
    public ConversationBuilder SetClock(IClock clock)
    {
        if (_conversation != null)
            throw new InvalidOperationException("Cannot change clock after conversation is started");
        return new ConversationBuilder(_ownerId, clock, _title);
    }

    /// <summary>
    /// Updates the title for this builder.
    /// </summary>
    public ConversationBuilder SetTitle(string? title)
    {
        if (_conversation != null)
            throw new InvalidOperationException("Cannot change initial title after conversation is started");
        return new ConversationBuilder(_ownerId, _clock, title);
    }    /// <summary>
    /// Starts the conversation. Must be called before any append operations.
    /// Returns the started conversation for further operations.
    /// </summary>
    public ConversationBuilder Start()
    {
        if (_conversation != null)
            throw new InvalidOperationException("Conversation already started");

        var result = Conversation.Start(_ownerId, _title, _clock);
        if (result.IsFailure)
            throw new InvalidOperationException($"Failed to start conversation: {result.Error.Message}");

        _conversation = result.Value;
        _capturedEvents.AddRange(_conversation.GetDomainEvents());
        return this;
    }

    /// <summary>
    /// Appends a user message with the specified content.
    /// Returns the sequence number of the appended message.
    /// Throws if the operation fails (for test convenience).
    /// </summary>
    public int AppendUser(string content)
    {
        EnsureStarted();
        
        var contentResult = MessageContent.Create(content);
        if (contentResult.IsFailure)
            throw new InvalidOperationException($"Invalid content: {contentResult.Error.Message}");

        var result = _conversation!.AppendUserMessage(contentResult.Value, _clock);
        if (result.IsFailure)
            throw new InvalidOperationException($"Failed to append user message: {result.Error.Message}");

        _capturedEvents.AddRange(_conversation.GetDomainEvents());
        return result.Value.Sequence;
    }

    /// <summary>
    /// Appends an assistant message with the specified content.
    /// Returns the sequence number of the appended message.
    /// Throws if the operation fails (for test convenience).
    /// </summary>
    public int AppendAssistant(string content)
    {
        EnsureStarted();
        
        var contentResult = MessageContent.Create(content);
        if (contentResult.IsFailure)
            throw new InvalidOperationException($"Invalid content: {contentResult.Error.Message}");

        var result = _conversation!.AppendAssistantMessage(contentResult.Value, _clock);
        if (result.IsFailure)
            throw new InvalidOperationException($"Failed to append assistant message: {result.Error.Message}");

        _capturedEvents.AddRange(_conversation.GetDomainEvents());
        return result.Value.Sequence;
    }

    /// <summary>
    /// Attempts to append a user message, returning the result without throwing.
    /// Useful for testing failure scenarios.
    /// </summary>
    public Result<int> TryAppendUser(string content)
    {
        EnsureStarted();
        
        var contentResult = MessageContent.Create(content);
        if (contentResult.IsFailure)
            return Result<int>.Failure(contentResult.Error);

        var result = _conversation!.AppendUserMessage(contentResult.Value, _clock);
        if (result.IsSuccess)
        {
            _capturedEvents.AddRange(_conversation.GetDomainEvents());
            return Result<int>.Success(result.Value.Sequence);
        }
        
        return Result<int>.Failure(result.Error);
    }    /// <summary>
    /// Attempts to append an assistant message, returning the result without throwing.
    /// Useful for testing failure scenarios.
    /// </summary>
    public Result<int> TryAppendAssistant(string content)
    {
        EnsureStarted();
        
        var contentResult = MessageContent.Create(content);
        if (contentResult.IsFailure)
            return Result<int>.Failure(contentResult.Error);

        var result = _conversation!.AppendAssistantMessage(contentResult.Value, _clock);
        if (result.IsSuccess)
        {
            _capturedEvents.AddRange(_conversation.GetDomainEvents());
            return Result<int>.Success(result.Value.Sequence);
        }
        
        return Result<int>.Failure(result.Error);
    }

    /// <summary>
    /// Completes the conversation.
    /// Throws if the operation fails (for test convenience).
    /// </summary>
    public ConversationBuilder Complete()
    {
        EnsureStarted();
        
        var result = _conversation!.Complete(_clock);
        if (result.IsFailure)
            throw new InvalidOperationException($"Failed to complete conversation: {result.Error.Message}");

        _capturedEvents.AddRange(_conversation.GetDomainEvents());
        return this;
    }

    /// <summary>
    /// Attempts to complete the conversation, returning the result without throwing.
    /// Useful for testing failure scenarios.
    /// </summary>
    public Result<bool> TryComplete()
    {
        EnsureStarted();
        
        var result = _conversation!.Complete(_clock);
        if (result.IsSuccess)
        {
            _capturedEvents.AddRange(_conversation.GetDomainEvents());
            return Result<bool>.Success(true);
        }
        
        return Result<bool>.Failure(result.Error);
    }

    /// <summary>
    /// Attempts to update the conversation title.
    /// </summary>
    public Result<bool> TryUpdateTitle(string newTitle)
    {
        EnsureStarted();
        
        var result = _conversation!.UpdateTitle(newTitle, _clock);
        if (result.IsSuccess)
        {
            _capturedEvents.AddRange(_conversation.GetDomainEvents());
            return Result<bool>.Success(true);
        }
        
        return Result<bool>.Failure(result.Error);
    }    // Properties for accessing the built conversation and its state
    
    /// <summary>
    /// Gets the built conversation. Throws if not started yet.
    /// </summary>
    public Conversation Conversation
    {
        get
        {
            EnsureStarted();
            return _conversation!;
        }
    }

    /// <summary>
    /// Gets the current message count.
    /// </summary>
    public int MessageCount => _conversation?.MessageCount ?? 0;

    /// <summary>
    /// Gets all captured domain events from operations.
    /// </summary>
    public IReadOnlyList<IDomainEvent> CapturedEvents => _capturedEvents.AsReadOnly();

    /// <summary>
    /// Gets the last captured event, or null if no events.
    /// </summary>
    public IDomainEvent? LastEvent => _capturedEvents.LastOrDefault();

    /// <summary>
    /// Gets the last captured event of the specified type, or null.
    /// </summary>
    public T? LastEvent<T>() where T : class, IDomainEvent => 
        _capturedEvents.OfType<T>().LastOrDefault();

    /// <summary>
    /// Gets all events of the specified type.
    /// </summary>
    public IEnumerable<T> Events<T>() where T : class, IDomainEvent => 
        _capturedEvents.OfType<T>();

    /// <summary>
    /// Clears captured events (useful for testing specific operations).
    /// </summary>
    public ConversationBuilder ClearEvents()
    {
        _capturedEvents.Clear();
        return this;
    }

    // Helper methods

    private void EnsureStarted()
    {
        if (_conversation == null)
            throw new InvalidOperationException("Conversation must be started first. Call Start() method.");
    }

    /// <summary>
    /// Creates a builder that starts immediately with default settings.
    /// </summary>
    public static ConversationBuilder Started() => New().Start();

    /// <summary>
    /// Creates a builder with a conversation that has the specified number of alternating messages.
    /// Pattern: User, Assistant, User, Assistant, etc.
    /// </summary>
    public static ConversationBuilder WithMessages(int count)
    {
        var builder = Started();
        
        for (int i = 0; i < count; i++)
        {
            string content = $"Message {i + 1}";
            if (i % 2 == 0)
                builder.AppendUser(content);
            else
                builder.AppendAssistant(content);
        }
        
        return builder;
    }
}