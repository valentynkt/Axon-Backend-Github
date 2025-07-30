# Clean Test Architecture Design for NUnit Migration

## Overview

This document outlines the clean test architecture design for migrating to NUnit with comprehensive Domain and Application layer coverage.

## Test Architecture Principles

### 1. Business-Oriented Test Design
- Tests should read like business specifications
- Use domain language in test names and structure
- Focus on behavior, not implementation details
- Organize tests by business capabilities

### 2. Clean Test Structure
```
tests/
├── Shared/                          # Common test infrastructure
│   ├── TestBase/                    # Abstract base classes
│   ├── Builders/                    # Test data builders (mother objects)
│   ├── Factories/                   # Domain entity/VO factories
│   └── Extensions/                  # Test utility extensions
├── Unit/                           # Pure unit tests
│   ├── Domain/                     # Domain entity & value object tests
│   └── Application/                # Command/query handler tests
├── Integration/                    # Integration tests
│   ├── Api/                        # Endpoint tests
│   └── Infrastructure/             # External service tests
└── ArchitectureTests/              # Architecture compliance tests
```

### 3. Test Categories and Organization
- **Unit**: Fast, isolated tests (< 100ms)
- **Integration**: Tests with external dependencies (< 5s)  
- **Business**: Business rule validation tests
- **Performance**: Performance and load tests
- **Smoke**: Critical path tests

## Test Factory Pattern

### Domain Entity Factories
```csharp
public static class ConversationFactory
{
    public static Conversation Valid() => new(ConversationId.New(), UserId.New());
    
    public static Conversation WithMessages(int count) 
    {
        var conversation = Valid();
        for (int i = 0; i < count; i++)
        {
            conversation.AddMessage($"Message {i + 1}", UserId.New());
        }
        return conversation;
    }
}
```

### Test Data Builders (Mother Objects)
```csharp
public class ProcessMessageCommandBuilder
{
    private string _message = "Default test message";
    private string? _mcpServerUrl;
    private Dictionary<string, string>? _headers;
    
    public ProcessMessageCommandBuilder WithMessage(string message)
    {
        _message = message;
        return this;
    }
    
    public ProcessMessageCommandBuilder WithMcpServer(string url)
    {
        _mcpServerUrl = url;
        return this;
    }
    
    public ProcessMessageCommand Build() => new(_message, _mcpServerUrl, _headers);
}
```

## Business-Oriented Test Naming

### Conventions
- **Pattern**: `[MethodUnderTest]_Given[Scenario]_Should[ExpectedBehavior]`
- **Domain Tests**: `[BusinessRule]_Given[BusinessScenario]_Should[BusinessOutcome]`
- **Handler Tests**: `[UseCaseName]_When[Condition]_Then[Result]`

### Examples
```csharp
[Test]
public void AddMessage_GivenValidContent_ShouldSucceedAndAddToConversation()

[Test] 
public void ProcessMessage_WhenMcpServerIsUnavailable_ThenShouldReturnFailureResult()

[Test]
public void MessageId_GivenEmptyGuid_ShouldFailValidationWithAppropriateError()
```

## Layer-Specific Test Patterns

### Domain Layer Tests
- **Focus**: Business rules, invariants, value object behavior
- **Isolation**: No external dependencies, pure logic
- **Coverage**: All business rules, edge cases, validation scenarios

### Application Layer Tests  
- **Focus**: Use case orchestration, command/query handling
- **Mocking**: External services (IAiClient, repositories)
- **Coverage**: Happy paths, error scenarios, validation logic

### Infrastructure Layer Tests
- **Focus**: External integrations, data access
- **Pattern**: Integration tests with real or test doubles
- **Coverage**: Configuration, error handling, data mapping

## Base Classes and Utilities

### Abstract Test Base Classes
```csharp
public abstract class DomainTestBase
{
    protected static ConversationId ValidConversationId() => ConversationId.New();
    protected static UserId ValidUserId() => UserId.New();
}

public abstract class ApplicationTestBase
{
    protected Mock<IAiClient> AiClientMock { get; private set; } = null!;
    
    [SetUp]
    public virtual void BaseSetUp()
    {
        AiClientMock = new Mock<IAiClient>();
    }
}
```

### Test Extensions
```csharp
public static class ResultTestExtensions
{
    public static void ShouldBeSuccess<T>(this Result<T> result)
    {
        result.IsSuccess.Should().BeTrue();
    }
    
    public static void ShouldBeFailureWith<T>(this Result<T> result, ErrorType expectedType)
    {
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(expectedType);
    }
}
```

## Test Organization Strategy

### By Feature/Module
```
tests/Unit/Chat/
├── Domain/
│   ├── Entities/ConversationTests.cs
│   ├── ValueObjects/MessageIdTests.cs
│   └── ValueObjects/ConversationIdTests.cs
├── Application/
│   ├── Commands/ProcessMessageHandlerTests.cs
│   └── Validators/ProcessMessageValidatorTests.cs
└── TestSupport/
    ├── ChatDomainFactory.cs
    └── ChatCommandBuilder.cs
```

### Test Fixture Organization
- One test fixture per class/handler being tested
- Group related test methods within fixtures
- Use nested classes for scenario grouping when appropriate

## Coverage Strategy

### Domain Layer (Target: 100%)
- All public methods on entities
- All value object creation/validation paths
- All business rule enforcement
- All domain error scenarios

### Application Layer (Target: 95%)
- All command/query handlers
- All validation scenarios
- All external service integration points
- Error handling and logging

### Infrastructure Layer (Target: 80%)
- Configuration validation
- External service clients
- Data mapping logic
- Connection/timeout scenarios

## Performance Considerations

### Fast Test Execution
- Domain tests: < 10ms each
- Application tests: < 100ms each
- Integration tests: < 5s each

### Parallel Execution
- Mark non-parallelizable tests explicitly
- Avoid shared state between tests
- Use test-specific data/mocks

## Migration Strategy

1. **Setup Infrastructure** - Base classes, factories, builders
2. **Migrate Domain Tests** - Value objects and entities first
3. **Migrate Application Tests** - Handlers and validators
4. **Add Missing Coverage** - Fill gaps identified during migration
5. **Performance Optimization** - Parallel execution, fast setup/teardown

This architecture ensures tests are maintainable, readable, and provide comprehensive coverage while following clean architecture principles.