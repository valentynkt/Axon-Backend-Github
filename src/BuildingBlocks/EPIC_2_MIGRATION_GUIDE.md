# Epic 2 Domain Enhancement - Migration Guide

## Overview

This guide provides step-by-step instructions for migrating existing domain models to Epic 2 Domain Enhancement patterns. The migration focuses on:

1. **Backward Compatibility** - Ensuring existing functionality continues to work
2. **Incremental Migration** - Migrating one aggregate at a time
3. **Performance Optimization** - Leveraging new patterns for better performance
4. **Epic 5 Integration** - Full integration with pipeline behaviors

## Migration Checklist

### Phase 1: Foundation Update ✅
- [x] Update CQRS base classes
- [x] Update Result/Error patterns
- [x] Add functional foundation

### Phase 2: Domain Model Migration 🔄
- [ ] Update base aggregate and entity classes
- [ ] Migrate value objects to Epic 2 patterns
- [ ] Update business rules framework
- [ ] Integrate specifications framework

### Phase 3: CQRS Integration 📋
- [ ] Update command handlers for Epic 2 patterns
- [ ] Update query handlers with specifications
- [ ] Integrate domain validation with pipeline behaviors
- [ ] Add caching support for specifications

### Phase 4: Testing & Validation 📋
- [ ] Create migration tests
- [ ] Performance benchmarks
- [ ] Integration tests

## Migration Patterns

### 1. Aggregate Migration Pattern

**Before (Old Pattern):**
```csharp
public sealed record Conversation : BaseAggregate<ConversationId>
{
    public Result<Message> AppendUserMessage(string content, string requestingUserId)
    {
        // Basic validation
        if (string.IsNullOrWhiteSpace(content))
            return Error.Validation("Content required");
            
        // Create message
        var message = Message.Create(content, Id, MessageRole.User, _messages.Count + 1);
        _messages.Add(message.Value);
        
        return message;
    }
}
```

**After (Epic 2 Pattern):**
```csharp
public sealed partial class Conversation : AggregateRoot<ConversationId>
{
    public Result<Message> AppendUserMessage(string content, string requestingUserId)
    {
        // 1. Business rules validation FIRST
        var rules = new RuleBuilder()
            .Must(IsActive, "CONVERSATION_INACTIVE", "Conversation must be active")
            .Must(BelongsToUser(requestingUserId), "NOT_OWNER", "User must own conversation")
            .NotEmpty(content, nameof(content))
            .Must(content.Length <= 100_000, "CONTENT_TOO_LONG", "Content exceeds maximum length")
            .AddRule(new ConversationMessageLimitRule(_messages.Count))
            .Build();
            
        if (rules.IsFailure)
            return rules;
        
        // 2. Create value objects with validation
        var messageContentResult = MessageContent.Create(content);
        if (messageContentResult.IsFailure)
            return messageContentResult.Error;
            
        // 3. Apply state change (all preconditions validated)
        return ApplyChange(() =>
        {
            var message = Message.Create(
                messageContentResult.Value,
                Id,
                MessageRole.User,
                _messages.Count + 1
            ).Value;
            
            _messages.Add(message);
            
            // 4. Raise domain events after successful state change
            RaiseDomainEvent(new UserMessageAppendedDomainEvent(Id, message.Id, requestingUserId));
        });
    }
    
    protected override IEnumerable<IBusinessRule> GetInvariants()
    {
        yield return new ConversationMustHaveOwnerRule(OwnerId);
        yield return new MessageSequenceIntegrityRule(_messages);
    }
}
```

### 2. Command Migration Pattern

**Before (Old Pattern):**
```csharp
public sealed record ProcessMessageCommand(
    string Message,
    Guid? ConversationId = null,
    string? UserId = null) : IRequest<Result<ProcessMessageResponse>>;
```

**After (Epic 2 Pattern):**
```csharp
public sealed record ProcessMessageCommand : DomainCommandBase<ProcessMessageResponse>, IRetryableOperation
{
    public string Message { get; init; } = string.Empty;
    public ConversationId? ConversationId { get; init; }
    public UserId UserId { get; init; }
    
    public override Type GetAggregateType() => typeof(Conversation);
    
    public override Validation<Unit> ValidateDomainRules()
    {
        var rules = new RuleBuilder()
            .NotEmpty(Message, nameof(Message))
            .Must(Message.Length <= 100_000, "MESSAGE_TOO_LONG", "Message exceeds maximum length")
            .NotNull(UserId, nameof(UserId))
            .Build();
            
        return rules.IsSuccess 
            ? Validation<Unit>.Valid(Unit.Value)
            : Validation<Unit>.Invalid(rules.Error);
    }
    
    public string? GetRetryPolicyName() => "StandardRetry";
}
```

### 3. Query Migration Pattern

**Before (Old Pattern):**
```csharp
public sealed record GetConversationHistoryQuery(
    Guid ConversationId,
    string? UserId) : IRequest<Result<GetConversationHistoryResponse>>;
```

**After (Epic 2 Pattern):**
```csharp
public sealed record GetConversationHistoryQuery : CacheableDomainQueryBase<GetConversationHistoryResponse>
{
    public ConversationId ConversationId { get; init; }
    public UserId UserId { get; init; }
    public int PageSize { get; init; } = 20;
    public int Page { get; init; } = 1;
    
    public override string GetCacheKey() => 
        $"ConversationHistory:{ConversationId}:{UserId}:{PageSize}:{Page}";
        
    public override TimeSpan? GetCacheDuration() => TimeSpan.FromMinutes(5);
    
    public override IEnumerable<string> GetCacheTags() 
    {
        yield return $"Conversation:{ConversationId}";
        yield return $"User:{UserId}";
    }
}
```

## Business Rules Migration

### Custom Business Rules
```csharp
// Before: Inline validation
if (_messages.Count >= 1000)
    return Error.Validation("Too many messages");

// After: Explicit business rule
public sealed record ConversationMessageLimitRule : BusinessRule
{
    private readonly int _currentCount;
    private readonly int _maxMessages;
    
    public ConversationMessageLimitRule(int currentCount, int maxMessages = 1000)
    {
        _currentCount = currentCount;
        _maxMessages = maxMessages;
    }
    
    public override string Code => "CONVERSATION_MESSAGE_LIMIT_EXCEEDED";
    public override string Message => $"Conversation cannot exceed {_maxMessages} messages";
    
    public override bool IsBroken() => _currentCount >= _maxMessages;
}
```

### Specification Migration
```csharp
// Before: Manual filtering in repository
public async Task<List<Conversation>> GetActiveConversationsByUser(string userId)
{
    return await _context.Conversations
        .Where(c => c.UserId == userId && c.Status == ConversationStatus.Active)
        .ToListAsync();
}

// After: Specification-based filtering
public sealed class ActiveConversationsByUserSpec : Specification<Conversation>
{
    private readonly UserId _userId;
    
    public ActiveConversationsByUserSpec(UserId userId)
    {
        _userId = userId;
    }
    
    public override Expression<Func<Conversation, bool>> ToExpression()
    {
        return c => c.OwnerId == _userId && c.Status == ConversationStatus.Active;
    }
}

// Usage in repository
public async Task<List<Conversation>> FindAsync(Specification<Conversation> specification)
{
    var query = _context.Conversations.AsQueryable();
    query = query.Where(specification.ToExpression());
    return await query.ToListAsync();
}
```

## Performance Considerations

### 1. Lazy Loading of Business Rules
```csharp
public sealed partial class Conversation : AggregateRoot<ConversationId>
{
    // Cache expensive business rule calculations
    private readonly Lazy<IEnumerable<IBusinessRule>> _cachedInvariants;
    
    private Conversation()
    {
        _cachedInvariants = new Lazy<IEnumerable<IBusinessRule>>(GetInvariants);
    }
    
    protected override IEnumerable<IBusinessRule> GetInvariants() => _cachedInvariants.Value;
}
```

### 2. Specification Composition
```csharp
// Efficient specification composition
var complexSpec = CommonSpecifications.Active<Conversation>()
    .And(new ConversationsByUserSpec(userId))
    .And(CommonSpecifications.CreatedAfter<Conversation>(DateTime.UtcNow.AddDays(-30)));

// Single database query with composed conditions
var conversations = await _repository.FindAsync(complexSpec);
```

### 3. Caching Integration
```csharp
// Automatic cache invalidation through tags
public sealed record GetConversationsQuery : CacheableDomainQueryBase<List<ConversationDto>>
{
    public override IEnumerable<string> GetCacheTags() 
    {
        yield return $"User:{UserId}";
        yield return "Conversations";
    }
}

// Cache invalidation in command handlers
public async Task Handle(AddMessageCommand request)
{
    // ... handle command
    
    // Automatic cache invalidation through Epic 5 pipeline
    await _cacheInvalidator.InvalidateByTagsAsync(new[] 
    { 
        $"Conversation:{request.ConversationId}",
        $"User:{request.UserId}"
    });
}
```

## Common Migration Issues

### Issue 1: Null Reference in Business Rules
**Problem:** Business rules accessing null properties during construction.
**Solution:** Use lazy evaluation and defensive checks.

```csharp
// Bad
public override bool IsBroken() => _conversation.Messages.Count > 1000; // NullRef if Messages is null

// Good
public override bool IsBroken() => _messageCount > _maxMessages;
```

### Issue 2: Circular Dependencies in Domain Events
**Problem:** Domain events referencing aggregates that aren't fully constructed.
**Solution:** Use primitive values in events.

```csharp
// Bad
public sealed record ConversationCreatedEvent(Conversation Conversation);

// Good
public sealed record ConversationCreatedEvent(ConversationId ConversationId, UserId UserId);
```

### Issue 3: Performance Issues with Large Aggregates
**Problem:** Loading entire aggregate for simple operations.
**Solution:** Use specifications for queries, keep aggregates focused.

```csharp
// Bad - loads full aggregate
var conversation = await _repository.GetByIdAsync(id);
var messageCount = conversation.Messages.Count;

// Good - use specification for count
var countSpec = new ConversationMessageCountSpec(id);
var messageCount = await _repository.CountAsync(countSpec);
```

## Testing Migration

### Unit Tests for Business Rules
```csharp
[Fact]
public void ConversationMessageLimitRule_Should_Be_Broken_When_Exceeds_Limit()
{
    // Arrange
    var rule = new ConversationMessageLimitRule(currentCount: 1000, maxMessages: 999);
    
    // Act
    var isBroken = rule.IsBroken();
    
    // Assert
    Assert.True(isBroken);
    Assert.Equal("CONVERSATION_MESSAGE_LIMIT_EXCEEDED", rule.Code);
}
```

### Integration Tests for Specifications
```csharp
[Fact]
public async Task ActiveConversationsByUserSpec_Should_Filter_Correctly()
{
    // Arrange
    var userId = UserId.New();
    var spec = new ActiveConversationsByUserSpec(userId);
    
    // Act
    var conversations = await _repository.FindAsync(spec);
    
    // Assert
    Assert.All(conversations, c => 
    {
        Assert.Equal(userId, c.OwnerId);
        Assert.Equal(ConversationStatus.Active, c.Status);
    });
}
```

## Migration Timeline

1. **Week 1**: Update base classes and infrastructure
2. **Week 2**: Migrate core domain models (Conversation, Message)
3. **Week 3**: Update CQRS handlers and integration
4. **Week 4**: Performance optimization and testing
5. **Week 5**: Production deployment and monitoring

## Support and Resources

- **Epic 2 Examples**: `/src/BuildingBlocks/Core/Domain/Examples/`
- **Migration Tests**: `/tests/Domain/Migration/`
- **Performance Benchmarks**: `/benchmarks/Domain/`
- **Integration Examples**: `/src/BuildingBlocks/Core/Domain/Examples/IntegratedUsageExamples.cs`

## Next Steps

1. Review this guide with the development team
2. Set up migration environment and tests
3. Begin incremental migration starting with Chat domain
4. Monitor performance and iterate based on results
5. Update documentation and team training materials