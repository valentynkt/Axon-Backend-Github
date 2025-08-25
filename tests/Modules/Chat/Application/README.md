# Chat Application Tests

This directory contains the comprehensive testing architecture for the Chat.Application module. The architecture follows SOLID principles and provides a future-proof foundation for all Chat application-layer testing needs.

## 🏗️ Architecture Overview

The testing architecture is organized into several key components:

```
tests/Modules/Chat/Application/
├── Common/                    # Base test infrastructure
│   ├── ApplicationTestBase.cs         # Base class for all application tests
│   ├── CommandHandlerTestBase.cs      # Specialized base for command handler tests
│   ├── QueryHandlerTestBase.cs        # Specialized base for query handler tests
│   └── IntegrationTestBase.cs         # Base for integration tests with real database
├── Builders/                  # Test data builders and factories
│   ├── CommandTestDataBuilder.cs      # Fluent builders for commands
│   ├── QueryTestDataBuilder.cs        # Fluent builders for queries
│   └── ApplicationTestDataFactory.cs  # High-level scenario factories
├── Extensions/                # Assertion extensions
│   └── ApplicationShouldlyExtensions.cs # Application-specific Shouldly extensions
├── Helpers/                   # Test utilities and helpers
│   ├── MockRepositoryHelpers.cs       # Repository mocking utilities
│   └── TestScenarioRunner.cs          # Complex scenario orchestration
├── Integration/               # Integration test base classes
│   └── ChatApplicationIntegrationTestBase.cs # Complete integration testing setup
└── GlobalUsings.cs           # Global using statements
```

## 🎯 Design Principles

### SOLID Principles Applied

1. **Single Responsibility**: Each base class has a single, well-defined purpose
2. **Open/Closed**: Extensible design allows adding new test types without modification
3. **Liskov Substitution**: All derived test classes can substitute their base classes
4. **Interface Segregation**: Separate interfaces for different testing concerns
5. **Dependency Inversion**: Tests depend on abstractions, not concrete implementations

### Key Design Patterns

- **Builder Pattern**: For fluent test data creation
- **Factory Pattern**: For complex test scenario creation
- **Template Method**: For standardized test execution flows
- **Strategy Pattern**: For different testing strategies (unit vs integration)

## 🧪 Test Types and Base Classes

### 1. ApplicationTestBase
Foundation class for all Chat application tests.

```csharp
public class MyApplicationTests : ApplicationTestBase
{
    [Test]
    public async Task SomeTest()
    {
        // Access to common services, mocks, and utilities
        var result = AssertSuccess(someOperation());
        // ... test logic
    }
}
```

**Provides:**
- Common mock setup (AuthService, Telemetry, Logger)
- Time control via FakeTimeProvider
- Service collection configuration
- Result pattern assertion helpers

### 2. CommandHandlerTestBase<TCommand, TResult, THandler>
Specialized for testing command handlers that modify state.

```csharp
public class StartConversationHandlerTests : CommandHandlerTestBase<StartConversationCommand, ConversationId, StartConversationHandler>
{
    protected override StartConversationHandler CreateHandler()
    {
        // Create handler with dependencies
    }

    protected override StartConversationCommand CreateValidCommand()
    {
        return CommandTestDataBuilder.StartConversation().WithTitle("Test").Build();
    }
    
    // Inherits standard test templates:
    // - Handle_WithValidCommand_ShouldReturnSuccess
    // - Handle_WithInvalidCommand_ShouldReturnFailure
    // - Handle_WithCancellation_ShouldHandleGracefully
}
```

### 3. QueryHandlerTestBase<TQuery, TResult, THandler>
Specialized for testing query handlers that read data.

```csharp
public class GetConversationsHandlerTests : QueryHandlerTestBase<GetConversationsQuery, Paged<ConversationListItem>, GetConversationsHandler>
{
    protected override GetConversationsHandler CreateHandler()
    {
        // Create handler with mocked dependencies
    }

    protected override GetConversationsQuery CreateValidQuery()
    {
        return QueryTestDataBuilder.GetConversations().WithFirstPage(10).Build();
    }
    
    // Inherits standard test templates including performance verification
}
```

### 4. IntegrationTestBase
For tests requiring real database connectivity using Testcontainers.

```csharp
public class MyIntegrationTests : ChatApplicationIntegrationTestBase
{
    [Test]
    public async Task CompleteWorkflow_ShouldPersistCorrectly()
    {
        // Real database, real handlers, full integration
        var command = CommandTestDataBuilder.StartConversation().Build();
        var result = await ExecuteCommand(command);
        
        result.ShouldBeSuccess();
        // Verify in database...
    }
}
```

## 🏭 Test Data Builders

### Command Builders
Fluent builders for creating test commands:

```csharp
// Simple usage
var command = CommandTestDataBuilder.StartConversation()
    .WithTitle("My Test Conversation")
    .Build();

// Complex scenarios
var command = CommandTestDataBuilder.AppendUserMessage()
    .ForConversation(conversationId)
    .WithContent("Test message")
    .Build();

// Edge cases
var command = CommandTestDataBuilder.StartConversation()
    .WithTooLongTitle()  // Exceeds validation limits
    .Build();
```

### Query Builders
Fluent builders for creating test queries:

```csharp
// Pagination testing
var query = QueryTestDataBuilder.GetConversations()
    .WithPagination(2, 20)
    .SortByUpdatedAt(SortDirection.Desc)
    .WithActiveConversationsOnly()
    .Build();

// Filtering scenarios
var query = QueryTestDataBuilder.GetConversations()
    .WithTitleFilter("important")
    .WithRecentConversations(days: 7)
    .Build();
```

### Scenario Factories
High-level factories for complex test scenarios:

```csharp
// Complete conversation scenario
var scenario = ApplicationTestDataFactory.CreateConversationScenario()
    .WithNewConversation("Test Chat")
    .WithUserMessage("Hello")
    .WithAssistantResponse("Hi there!")
    .WithConversationCompletion()
    .Build();

// Performance testing scenario
var scenario = ApplicationTestDataFactory.CreatePerformanceScenario()
    .WithDataSetSize(1000)
    .WithMaxExecutionTime(TimeSpan.FromMilliseconds(500))
    .WithConversationListQuery()
    .Build();
```

## 🔧 Extensions and Helpers

### Shouldly Extensions
Application-specific assertions:

```csharp
// Result pattern assertions
result.ShouldBeSuccess();
result.ShouldBeFailureWithMessage("Expected error message");
result.ShouldBeErrorOfType<ValidationError>();

// Pagination assertions
pagedResult.ShouldHavePagination(1, 10)
          .ShouldHaveTotalCount(100)
          .ShouldBeSortedBy(x => x.CreatedAt, ascending: false);

// Performance assertions
await operation.ShouldCompleteWithin(TimeSpan.FromSeconds(1));
```

### Mock Repository Helpers
Fluent repository mocking:

```csharp
var mockRepo = MockRepositoryHelpers.CreateMockConversationReadRepository()
    .ReturnsConversation(conversationId, testConversation)
    .ReturnsPagedConversations(conversations, totalCount: 100)
    .Build();

// Verification helpers
mockRepo.ShouldHaveRetrievedConversation(conversationId);
```

## 🚀 Usage Examples

### 1. Simple Command Handler Test

```csharp
[TestFixture]
public class StartConversationHandlerTests : CommandHandlerTestBase<StartConversationCommand, ConversationId, StartConversationHandler>
{
    private IConversationWriteRepository _mockRepository = null!;

    protected override StartConversationHandler CreateHandler()
    {
        _mockRepository = MockRepositoryHelpers.CreateMockConversationWriteRepository();
        return new StartConversationHandler(_mockRepository, MockAuthService, MockLogger);
    }

    protected override StartConversationCommand CreateValidCommand()
    {
        return CommandTestDataBuilder.StartConversation()
            .WithTitle("Valid Conversation")
            .Build();
    }

    protected override StartConversationCommand CreateInvalidCommand()
    {
        return CommandTestDataBuilder.StartConversation()
            .WithTooLongTitle()
            .Build();
    }

    [Test]
    public async Task Handle_ValidCommand_ShouldCreateConversation()
    {
        // Arrange
        var command = CreateValidCommand();

        // Act
        var result = await ExecuteCommand(command);

        // Assert
        result.ShouldBeSuccess();
        _mockRepository.ShouldHaveAddedConversation(result.Value);
    }
}
```

### 2. Integration Test Example

```csharp
[TestFixture]
public class ConversationWorkflowIntegrationTests : ChatApplicationIntegrationTestBase
{
    [Test]
    public async Task CompleteConversationWorkflow_ShouldPersistAllData()
    {
        // Arrange
        await EnsureDatabaseCreatedAsync();
        
        // Act - Execute complete workflow
        var scenario = await CreateConversationScenario("Complete Workflow");
        var result = await scenario.ExecuteCompleteConversationFlow(
            title: "Integration Test Conversation",
            userMessageCount: 3,
            assistantMessageCount: 3);

        // Assert
        result.Success.ShouldBeTrue();
        
        // Verify data persistence
        var conversations = await ConversationReadRepository.ListAsync(
            new ConversationsForOwnerSpec(TestUserId, Page.First(10), ConversationSortBy.CreatedAt, SortDirection.Desc));
        
        conversations.ShouldHaveCount(1);
        conversations[0].Status.ShouldBe(ConversationStatus.Completed);
    }
}
```

### 3. Performance Test Example

```csharp
[TestFixture]
public class ConversationQueryPerformanceTests : ChatApplicationIntegrationTestBase
{
    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        await base.OneTimeSetUp();
        await SeedRealisticDataset(conversationCount: 1000, messagesPerConversation: 10);
    }

    [Test]
    public async Task GetConversations_WithLargeDataset_ShouldMeetPerformanceRequirements()
    {
        // Arrange
        var query = QueryTestDataBuilder.GetConversations()
            .WithFirstPage(50)
            .SortByUpdatedAt()
            .Build();

        // Act & Assert
        var (result, executionTime) = await MeasureCommandPerformance(query);
        
        result.ShouldBeSuccess();
        executionTime.ShouldBeLessThan(TimeSpan.FromMilliseconds(500));
        
        var pagedResult = result.Value;
        pagedResult.ShouldHavePagination(1, 50)
                  .ShouldHaveItemsWithinPageSize()
                  .ShouldBeSortedBy(x => x.UpdatedAt, ascending: false);
    }
}
```

## 🔄 Best Practices

### 1. Test Organization
- **Group related tests**: Use nested classes or separate files for logical grouping
- **Consistent naming**: Follow `MethodName_Scenario_ExpectedResult` pattern
- **Clear test data**: Use builders with descriptive method names

### 2. Mock Usage
- **Use provided helpers**: Leverage `MockRepositoryHelpers` for consistency
- **Verify interactions**: Always verify that mocks were called as expected
- **Reset mocks**: Ensure clean state between tests

### 3. Async Testing
- **Proper cancellation**: Test cancellation token handling
- **Timeout scenarios**: Test behavior under timeouts
- **Exception handling**: Verify exception propagation

### 4. Performance Testing
- **Realistic data**: Use `SeedRealisticDataset` for performance tests
- **Multiple measurements**: Run performance tests multiple times
- **Environment considerations**: Account for CI/CD environment differences

### 5. Integration Testing
- **Database isolation**: Each test should be independent
- **Proper cleanup**: Ensure database state is cleaned between tests
- **Container management**: Let Testcontainers handle container lifecycle

## 📊 Testing Strategy

### Unit Tests (70%)
- Command/Query handlers
- Business logic validation
- Error handling scenarios
- Mock-based testing

### Integration Tests (20%)
- Full workflow testing
- Database interactions
- Service integration
- Performance validation

### End-to-End Tests (10%)
- Complete user scenarios
- Cross-module integration
- Real environment testing

## 🛠️ Extending the Architecture

### Adding New Command Tests
1. Create test class inheriting from `CommandHandlerTestBase<,,>`
2. Implement abstract methods for handler and command creation
3. Add specific test cases as needed

### Adding New Query Tests
1. Create test class inheriting from `QueryHandlerTestBase<,,>`
2. Implement abstract methods for handler and query creation
3. Focus on read-only scenarios and performance

### Adding New Integration Tests
1. Inherit from `ChatApplicationIntegrationTestBase`
2. Use real database and full service stack
3. Test complete workflows and data persistence

## 📝 Notes

- **Build Integration**: All tests integrate with the existing Domain test infrastructure
- **Performance**: Integration tests use PostgreSQL via Testcontainers for realistic performance
- **Extensibility**: Architecture designed for easy addition of new test types and scenarios
- **Maintainability**: Consistent patterns and clear separation of concerns
- **Documentation**: Each class and method includes comprehensive XML documentation

This architecture provides a solid foundation for comprehensive testing of the Chat application layer while maintaining flexibility for future requirements and scenarios.