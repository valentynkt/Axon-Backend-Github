# Chat Domain Testing Foundation

This testing foundation provides comprehensive infrastructure for testing the Chat Domain module in the Axon Backend. It follows Clean Architecture principles and implements advanced testing patterns using NUnit, Shouldly, NSubstitute, and Bogus.

## Architecture Overview

The testing foundation is organized to mirror the domain structure and provide reusable components:

```
tests/Modules/Chat/Domain/
├── Aggregates/           # Tests for aggregate roots
├── Entities/             # Tests for domain entities  
├── ValueObjects/         # Tests for value objects
├── Rules/                # Tests for business rules
├── Common/               # Shared base classes and utilities
├── Builders/             # Test data builders (Builder pattern)
├── Extensions/           # Custom Shouldly assertions
├── TestDoubles/          # Fake implementations and test data generators
└── GlobalUsings.cs       # Common imports
```

## Key Components

### 1. Test Builders (`Builders/`)

**ConversationBuilder** - Fluent builder for creating Conversation aggregates:
```csharp
var conversation = ConversationBuilder.New()
    .WithOwner(userId)
    .WithTitle("Test Conversation")
    .WithAlternatingMessages(4)
    .ThatShouldBeCompleted()
    .Build();
```

**MessageBuilder** - Builder for Message entities:
```csharp
var message = MessageBuilder.NewUserMessage()
    .WithContent("Hello World")
    .WithSequence(1)
    .Build();
```

### 2. Base Test Classes (`Common/`)

**DomainTestBase** - Base for all domain tests:
- Provides FakeTimeProvider for deterministic testing
- Common assertion helpers for Result patterns
- Utilities for StrongId creation

**AggregateTestBase<TAggregate, TId>** - Specialized for aggregate testing:
- Domain event verification methods
- Invariant checking utilities
- Event ordering and timing assertions

**ValueObjectTestBase<TValueObject>** - For value object testing:
- Equality contract verification
- Immutability testing
- ToString() and GetHashCode() validation

**EventTestBase** - For domain event testing:
- Event property validation
- Chronological ordering verification
- Event reconstruction testing

### 3. Custom Assertions (`Extensions/`)

**ChatDomainShouldlyExtensions** - Domain-specific fluent assertions:
```csharp
conversation.ShouldBeActive();
conversation.ShouldHaveMessageCount(3);
conversation.ShouldBelongTo(userId);
message.ShouldBeUserMessage();
result.ShouldBeSuccess();
```

**ResultTestExtensions** - Result pattern testing utilities:
```csharp
var value = result.ExtractValue();
result.ShouldBeSuccessWithValue(v => v.ShouldNotBeNull());
results.AllShouldBeSuccessful();
```

### 4. Test Doubles (`TestDoubles/`)

**FakeTimeProvider** - Controllable time for testing:
```csharp
var timeProvider = new FakeTimeProvider();
timeProvider.Advance(TimeSpan.FromHours(1));
```

**TestDataGenerator** - Realistic test data using Bogus:
```csharp
var title = TestDataGenerator.GenerateConversationTitle();
var messages = TestDataGenerator.GenerateConversationFlow(5);
```

**StrongIdTestHelpers** - Utilities for StronglyTypedId testing:
```csharp
var conversationIds = StrongIdTestHelpers.CreateConversationIds(10);
StrongIdTestHelpers.AssertAllIdsAreUnique(conversationIds);
```

### 5. Test Infrastructure (`Common/`)

**TestConstants** - Centralized test data constants
**BusinessRuleTestHelpers** - Business rule testing utilities
**DomainEventTestHelpers** - Domain event testing patterns
**ChatDomainTestFactory** - Central factory for test objects

## Usage Patterns

### Testing Aggregates

```csharp
[TestFixture]
public class ConversationTests : AggregateTestBase<Conversation, ConversationId>
{
    [Test]
    public void StartNewConversation_Should_RaiseConversationStartedEvent()
    {
        // Arrange
        var userId = CreateUserId();
        var title = "Test Conversation";
        
        // Act
        var result = Conversation.StartNewConversation(userId, title, TimeProvider);
        
        // Assert
        var conversation = result.ShouldBeSuccess();
        conversation.ShouldBeActive();
        conversation.ShouldBelongTo(userId);
        
        AssertDomainEventRaised<ConversationStartedEvent>(conversation);
    }
}
```

### Testing Business Rules

```csharp
[Test]
public void ConversationMustHaveOwnerRule_Should_BeBroken_When_OwnerIsDefault()
{
    // Arrange
    var rule = new ConversationMustHaveOwnerRule(default(UserId));
    
    // Act & Assert
    BusinessRuleTestHelpers.AssertRuleIsBroken(rule);
    BusinessRuleTestHelpers.AssertRuleHasMessage(rule, "Conversation must have an owner");
}
```

### Testing Value Objects

```csharp
[TestFixture]
public class ConversationTitleTests : ValueObjectTestBase<ConversationTitle>
{
    [Test]
    public void Create_Should_TrimWhitespace()
    {
        // Act
        var result = ConversationTitle.Create("  Test Title  ");
        
        // Assert
        var title = result.ShouldBeSuccess();
        title.Value.ShouldBe("Test Title");
    }
}
```

### Testing Domain Events

```csharp
[TestFixture]
public class ConversationEventsTests : EventTestBase
{
    [Test]
    public void ConversationStartedEvent_Should_HaveValidProperties()
    {
        // Arrange & Act
        var domainEvent = DomainEventTestHelpers.MockEvents.CreateConversationStarted();
        
        // Assert
        AssertWellFormedDomainEvent(domainEvent);
        domainEvent.ConversationId.ShouldNotBeNull();
        domainEvent.OwnerId.ShouldNotBeNull();
    }
}
```

## Advanced Features

### Test Data Generation
- **Realistic Data**: Uses Bogus to generate realistic conversation titles, message content
- **Edge Cases**: Includes boundary testing with max-length strings, empty values
- **Deterministic Testing**: Support for reproducible test data with seeds

### Domain Event Testing
- **Event Capturing**: Utilities to capture events during aggregate operations
- **Event Sequencing**: Verification of event ordering and timing
- **Event Patterns**: Common patterns like conversation lifecycle verification

### Result Pattern Testing
- **Fluent Assertions**: Extension methods for Result<T, TError> testing
- **Error Extraction**: Easy access to success values and error details
- **Batch Testing**: Utilities for testing collections of Results

### Business Rule Testing
- **Rule Validation**: Comprehensive testing of IBusinessRule implementations
- **Edge Case Testing**: Automated testing of boundary conditions
- **Error Message Validation**: Verification of meaningful error messages

## Best Practices

1. **Use Builders**: Always use test builders for creating domain objects
2. **Test Time**: Use FakeTimeProvider for predictable time-dependent behavior
3. **Domain Events**: Verify that aggregates raise appropriate domain events
4. **Business Rules**: Test both valid and invalid scenarios for each rule
5. **Realistic Data**: Use TestDataGenerator for realistic test scenarios
6. **Cleanup**: Base classes handle proper test cleanup and isolation

## Integration with CI/CD

The test foundation is designed to work seamlessly with:
- **Continuous Integration**: Deterministic tests with no external dependencies
- **Parallel Execution**: Isolated test scenarios that can run concurrently
- **Performance Testing**: Support for large data sets and performance scenarios
- **Coverage Analysis**: Comprehensive coverage of domain logic and edge cases

This foundation provides everything needed to create maintainable, readable, and comprehensive tests for the Chat Domain following industry best practices and Clean Architecture principles.