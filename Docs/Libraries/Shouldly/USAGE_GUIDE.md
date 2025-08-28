---
id: AXON-20250730-Libraries-Shouldly-USAGE_GUIDE
title: Shouldly Usage Guide for Axon Backend
module: Libraries
feature: Shouldly
owner: docs-grounder
status: draft
created: 2025-07-30
updated: 2025-07-30
version: 1
---

# Shouldly Usage Guide for .NET 10 Development

## Overview

Shouldly is a fluent assertion framework that provides natural language syntax and superior error messages for .NET testing. This guide covers practical usage patterns for Axon Backend's Clean Architecture + CQRS implementation.

## Installation

```bash
# Add to test projects only
dotnet add package Shouldly --version 4.3.0
```

```xml
<!-- In test project .csproj -->
<PackageReference Include="Shouldly" Version="4.3.0" />
```

## Basic Syntax Patterns

### Equality Assertions
```csharp
using Shouldly;

// Basic equality
var result = calculator.Add(2, 3);
result.ShouldBe(5);

// Null checks
var user = userService.GetUser(id);
user.ShouldNotBeNull();
user.Name.ShouldNotBeNullOrEmpty();

// Boolean assertions
user.IsActive.ShouldBeTrue();
user.IsDeleted.ShouldBeFalse();
```

### Numeric Comparisons
```csharp
var score = game.CalculateScore();
score.ShouldBeGreaterThan(0);
score.ShouldBeLessThanOrEqualTo(100);
score.ShouldBeInRange(1, 100);

// Floating-point with tolerance
var result = 0.1 + 0.2;
result.ShouldBe(0.3, 0.00001);
```### String Assertions
```csharp
var message = "Hello, World!";
message.ShouldContain("World");
message.ShouldStartWith("Hello");
message.ShouldEndWith("!");
message.ShouldNotBeNullOrWhiteSpace();

// Case-insensitive
message.ShouldContain("WORLD", Case.Insensitive);
```

### Collection Assertions
```csharp
var items = new[] { "apple", "banana", "cherry" };

// Basic collection checks
items.ShouldNotBeEmpty();
items.Length.ShouldBe(3);
items.ShouldContain("banana");
items.ShouldNotContain("grape");

// All elements condition
items.ShouldAllBe(item => item.Length > 0);

// Predicate-based
items.ShouldContain(item => item.StartsWith("a"));

// Order-sensitive comparison
var expected = new[] { "apple", "banana", "cherry" };
items.ShouldBe(expected);

// Order-insensitive comparison
var unordered = new[] { "cherry", "apple", "banana" };
items.ShouldBe(unordered, ignoreOrder: true);
```### Exception Testing
```csharp
// Exception should be thrown
Action action = () => service.ProcessInvalidData(null);
action.ShouldThrow<ArgumentNullException>();

// With message verification
var exception = action.ShouldThrow<ArgumentNullException>();
exception.Message.ShouldContain("cannot be null");

// Exception should NOT be thrown
Action validAction = () => service.ProcessValidData(data);
validAction.ShouldNotThrow();

// Async exception testing
Func<Task> asyncAction = () => service.ProcessAsync(invalidData);
await asyncAction.ShouldThrowAsync<ValidationException>();
await validAsyncAction.ShouldNotThrowAsync();
```

### Type Assertions
```csharp
object result = factory.Create();
result.ShouldBeOfType<Customer>();
result.ShouldBeAssignableTo<IEntity>();
result.ShouldNotBeOfType<Product>();
```

## CQRS & Result Pattern Integration

### Testing Command Handlers
```csharp
[Test]
public async Task ProcessMessageCommand_WithValidInput_ReturnsSuccess()
{
    // Arrange
    var command = new ProcessMessageCommand("Hello", Guid.NewGuid());
    var handler = new ProcessMessageHandler(_mockAiClient.Object);

    // Act
    var result = await handler.Handle(command, CancellationToken.None);

    // Assert - Shouldly with Result pattern
    result.ShouldNotBeNull();
    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldNotBeNullOrEmpty();
    result.Value.ShouldContain("processed");
}

[Test]
public async Task ProcessMessageCommand_WithInvalidInput_ReturnsFailure()
{
    // Arrange
    var command = new ProcessMessageCommand("", Guid.Empty);
    var handler = new ProcessMessageHandler(_mockAiClient.Object);

    // Act
    var result = await handler.Handle(command, CancellationToken.None);

    // Assert
    result.ShouldNotBeNull();
    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("VALIDATION_ERROR");
    result.Error.Message.ShouldContain("invalid");
}
```### Testing Query Handlers
```csharp
[Test]
public async Task GetConversationQuery_WithValidId_ReturnsConversation()
{
    // Arrange
    var conversationId = ConversationId.New();
    var query = new GetConversationQuery(conversationId.Value);
    var handler = new GetConversationHandler(_mockRepository.Object);

    // Act
    var result = await handler.Handle(query, CancellationToken.None);

    // Assert
    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldNotBeNull();
    result.Value.Id.ShouldBe(conversationId.Value);
    result.Value.Messages.ShouldNotBeEmpty();
}
```

### Testing Domain Entities
```csharp
[Test]
public void Conversation_AddMessage_ShouldAddToMessages()
{
    // Arrange
    var conversation = new Conversation(ConversationId.New(), UserId.New());
    var messageContent = "Test message";

    // Act
    var result = conversation.AddMessage(messageContent, UserId.New());

    // Assert
    result.IsSuccess.ShouldBeTrue();
    conversation.Messages.Count.ShouldBe(1);
    conversation.Messages.First().Content.ShouldBe(messageContent);
}

[Test]
public void MessageId_Create_WithEmptyGuid_ShouldReturnError()
{
    // Arrange & Act
    var result = MessageId.Create(Guid.Empty);

    // Assert
    result.IsFailure.ShouldBeTrue();
    result.Error.Type.ShouldBe(ErrorType.Validation);
    result.Error.Message.ShouldContain("empty");
}
```

## Multiple Assertions
```csharp
[Test]
public void UserRegistration_WithValidData_ShouldCreateUser()
{
    // Arrange
    var userData = new UserRegistrationData("john@test.com", "password123");
    
    // Act
    var result = userService.Register(userData);

    // Assert - All conditions must pass
    result.ShouldSatisfyAllConditions(
        () => result.IsSuccess.ShouldBeTrue(),
        () => result.Value.ShouldNotBeNull(),
        () => result.Value.Email.ShouldBe("john@test.com"),
        () => result.Value.Id.ShouldNotBe(Guid.Empty),
        () => result.Value.CreatedAt.ShouldBeInRange(
            DateTime.UtcNow.AddMinutes(-1), 
            DateTime.UtcNow.AddMinutes(1))
    );
}
```## API Integration Testing
```csharp
[Test]
public async Task ProcessMessage_WithValidRequest_ReturnsOkResult()
{
    // Arrange
    var request = new ProcessMessageRequest 
    { 
        Message = "Hello AI", 
        UserId = Guid.NewGuid() 
    };

    // Act
    var response = await _client.PostAsJsonAsync("/api/chat/process", request);
    var result = await response.Content.ReadFromJsonAsync<ProcessMessageResponse>();

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    result.ShouldNotBeNull();
    result.ProcessedMessage.ShouldNotBeNullOrEmpty();
    result.ProcessedMessage.ShouldContain("processed");
    result.Timestamp.ShouldBeInRange(
        DateTime.UtcNow.AddMinutes(-1), 
        DateTime.UtcNow);
}
```

## Best Practices for Axon Backend

### 1. Use Descriptive Test Names
```csharp
// ✅ Good - Clear intent
[Test]
public void ProcessMessage_WithEmptyMessage_ShouldReturnValidationError()

// ❌ Avoid - Unclear purpose  
[Test]
public void TestProcessMessage()
```

### 2. Leverage Custom Messages
```csharp
// Add context for complex assertions
result.IsSuccess.ShouldBeTrue("Expected successful processing of valid message");
user.Permissions.ShouldContain("READ_MESSAGES", "User should have basic read permissions");
```

### 3. Test Edge Cases with Clear Assertions
```csharp
[Test]
public void ConversationId_WithMaxGuid_ShouldBeValid()
{
    // Arrange
    var maxGuid = new Guid("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF");
    
    // Act
    var result = ConversationId.Create(maxGuid);
    
    // Assert
    result.IsSuccess.ShouldBeTrue("Maximum GUID value should be acceptable");
    result.Value.Value.ShouldBe(maxGuid);
}
```

### 4. Domain-Specific Extension Methods
```csharp
// Create extension methods for common domain assertions
public static class ShouldlyExtensions
{
    public static void ShouldBeValidResult<T>(this Result<T> result)
    {
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
    }

    public static void ShouldBeValidationError(this Result result, string expectedCode)
    {
        result.ShouldNotBeNull();
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation); 
        result.Error.Code.ShouldBe(expectedCode);
    }
}

// Usage
var result = handler.Handle(command);
result.ShouldBeValidResult();
```## Configuration for Build Servers

For optimal error messages in CI/CD, ensure "full" PDB files:

### Visual Studio Configuration
1. Go to Project Properties → Build → Advanced → Debug
2. Set Debug Info to "full" (not "pdb-only")

### MSBuild Configuration
```xml
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <DebugType>full</DebugType>
  <DebugSymbols>true</DebugSymbols>
</PropertyGroup>
```

## Migration from Traditional Assertions

### From NUnit
```csharp
// Before (NUnit)
Assert.That(result, Is.EqualTo(expected));
Assert.That(list, Is.Not.Empty);
Assert.That(() => action(), Throws.TypeOf<InvalidOperationException>());

// After (Shouldly)
result.ShouldBe(expected);
list.ShouldNotBeEmpty();
action.ShouldThrow<InvalidOperationException>();
```

### From xUnit
```csharp
// Before (xUnit)
Assert.Equal(expected, actual);
Assert.NotNull(value);
Assert.Throws<ArgumentException>(() => method());

// After (Shouldly)
actual.ShouldBe(expected);
value.ShouldNotBeNull();
((Action)(() => method())).ShouldThrow<ArgumentException>();
```

## Common Patterns Summary

| Scenario | Shouldly Syntax | Notes |
|----------|----------------|-------|
| Equality | `actual.ShouldBe(expected)` | Most common assertion |
| Null check | `value.ShouldNotBeNull()` | Better than `!= null` |
| Collection empty | `list.ShouldNotBeEmpty()` | Clear intent |
| Exception | `action.ShouldThrow<TException>()` | Returns exception for further assertions |
| Multiple conditions | `obj.ShouldSatisfyAllConditions(...)` | All assertions run even if early ones fail |
| Async exception | `await asyncAction.ShouldThrowAsync<T>()` | For async methods |
| String contains | `text.ShouldContain("substring")` | Case-sensitive by default |
| Numeric range | `value.ShouldBeInRange(min, max)` | Inclusive range check |

## Performance Considerations

- Shouldly uses reflection for error message generation
- Minimal performance impact for test scenarios
- Error message generation only occurs on assertion failure
- Consider custom messages for frequently failing assertions during development

## Next Steps

1. Install Shouldly in test projects: `dotnet add package Shouldly`
2. Start with basic equality assertions in existing tests
3. Gradually adopt more specific assertion methods
4. Create domain-specific extension methods for common patterns
5. Configure build servers for optimal error messages

For more examples, see the [official Shouldly documentation](https://docs.shouldly.org/).