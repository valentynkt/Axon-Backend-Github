# Moq 4.20.72 Implementation Guide for Axon Backend

## Overview

This guide provides comprehensive documentation for using Moq 4.20.72 in the Axon Backend project's Clean Architecture implementation. Moq is the primary mocking framework for isolating units under test from their dependencies, enabling focused and reliable unit testing.

## Package Information

- **Version**: 4.20.72 (Latest stable)
- **.NET 10 Compatibility**: ✅ Full support including new preview features
- **Target Frameworks**: .NET 6.0+, .NET Standard 2.0, .NET Framework 4.6.2+
- **NuGet**: `<PackageReference Include="Moq" Version="4.20.72" />`

### Installation

```xml
<!-- In test projects -->
<PackageReference Include="Moq" Version="4.20.72" />
<PackageReference Include="xunit" Version="2.4.2" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.4.5" />
```

## Clean Architecture Integration

### Dependency Injection Mocking Pattern

Moq excels in Clean Architecture scenarios where dependency injection is paramount. The framework integrates seamlessly with the project's DI patterns:

```csharp
// Example: Testing Application layer command handler
public class ProcessMessageHandlerTests
{
    private readonly Mock<IAiClient> _aiClientMock;
    private readonly Mock<IConversationRepository> _repositoryMock;
    private readonly ProcessMessageHandler _handler;

    public ProcessMessageHandlerTests()
    {
        // Use Strict behavior for better test reliability
        _aiClientMock = new Mock<IAiClient>(MockBehavior.Strict);
        _repositoryMock = new Mock<IConversationRepository>(MockBehavior.Strict);
        
        _handler = new ProcessMessageHandler(_aiClientMock.Object, _repositoryMock.Object);
    }

    [Fact]
    public async Task Handle_ValidMessage_ReturnsProcessedResult()
    {
        // Arrange
        var command = new ProcessMessageCommand("Test message", Guid.NewGuid());
        var expectedResult = "Processed: Test message";
        
        _aiClientMock
            .Setup(x => x.ProcessAsync(command.Message, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(expectedResult));
            
        // Act
        var result = await _handler.Handle(command, CancellationToken.None);
        
        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedResult, result.Value);
        
        // Verify interactions
        _aiClientMock.Verify(x => x.ProcessAsync(command.Message, It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

## Core Mocking Patterns

### 1. Basic Setup and Returns

```csharp
// Simple value return
mock.Setup(x => x.GetById(It.IsAny<int>()))
    .Returns(expectedEntity);

// Conditional setup with specific arguments
mock.Setup(x => x.GetById(42))
    .Returns(specificEntity);

// Multiple setups for different scenarios
mock.Setup(x => x.GetById(It.Is<int>(id => id > 0)))
    .Returns(validEntity);
mock.Setup(x => x.GetById(It.Is<int>(id => id <= 0)))
    .Returns((Entity)null);
```

### 2. Async Method Mocking

Modern async patterns using `ReturnsAsync()`:

```csharp
// Task<T> methods - preferred approach
mock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
    .ReturnsAsync(expectedResult);

// Alternative approach using Task.FromResult
mock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
    .Returns(Task.FromResult(expectedResult));

// Task (void) methods
mock.Setup(x => x.SaveAsync(It.IsAny<Entity>()))
    .Returns(Task.CompletedTask);

// Complex async with Result<T> pattern (Axon Backend specific)
mock.Setup(x => x.CreateAsync(It.IsAny<CreateEntityCommand>()))
    .ReturnsAsync(Result.Success(new EntityDto(Guid.NewGuid(), "Test")));
```

### 3. Exception Handling

```csharp
// Throwing exceptions
mock.Setup(x => x.GetById(It.IsAny<int>()))
    .Throws<ArgumentException>();

// Conditional exceptions
mock.Setup(x => x.GetById(It.Is<int>(id => id < 0)))
    .Throws(new ArgumentOutOfRangeException(nameof(id)));

// Async exceptions
mock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
    .ThrowsAsync(new EntityNotFoundException());
```

## Advanced Mocking Techniques

### 1. Callback Patterns

```csharp
// Capture arguments for verification
Entity capturedEntity = null;
mock.Setup(x => x.Save(It.IsAny<Entity>()))
    .Callback<Entity>(entity => capturedEntity = entity);

// Complex callback with async
mock.Setup(x => x.ProcessAsync(It.IsAny<string>()))
    .Callback<string>(message => Console.WriteLine($"Processing: {message}"))
    .ReturnsAsync("Processed");

// Multiple parameter callbacks
mock.Setup(x => x.Update(It.IsAny<Guid>(), It.IsAny<Entity>()))
    .Callback<Guid, Entity>((id, entity) => 
    {
        // Validation logic or state tracking
        Assert.Equal(id, entity.Id);
    });
```

### 2. Property Mocking

```csharp
// Property setup
mock.Setup(x => x.IsActive).Returns(true);
mock.SetupProperty(x => x.Name); // Auto-implemented property behavior

// Property with getter and setter
mock.SetupGet(x => x.CurrentUser).Returns(testUser);
mock.SetupSet(x => x.CurrentUser = It.IsAny<User>());

// Property tracking changes
mock.SetupAllProperties(); // All properties become trackable
```

### 3. Sequence and Ordered Calls

```csharp
// Using MockSequence for ordered verification
var sequence = new MockSequence();
mock.InSequence(sequence).Setup(x => x.Initialize());
mock.InSequence(sequence).Setup(x => x.Process());
mock.InSequence(sequence).Setup(x => x.Cleanup());
```

## Interface Mocking for Clean Architecture

### Repository Pattern Mocking

```csharp
public class ConversationRepositoryMockBuilder
{
    private readonly Mock<IConversationRepository> _mock;
    
    public ConversationRepositoryMockBuilder()
    {
        _mock = new Mock<IConversationRepository>(MockBehavior.Strict);
    }
    
    public ConversationRepositoryMockBuilder WithGetByIdAsync(ConversationId id, Conversation conversation)
    {
        _mock.Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);
        return this;
    }
    
    public ConversationRepositoryMockBuilder WithSaveAsync()
    {
        _mock.Setup(x => x.SaveAsync(It.IsAny<Conversation>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return this;
    }
    
    public IConversationRepository Build() => _mock.Object;
    public Mock<IConversationRepository> BuildMock() => _mock;
}

// Usage in tests
var repository = new ConversationRepositoryMockBuilder()
    .WithGetByIdAsync(conversationId, testConversation)
    .WithSaveAsync()
    .Build();
```

### Service Layer Mocking

```csharp
// Domain service mocking
public class MessageProcessingServiceTests
{
    private readonly Mock<IAiClient> _aiClientMock;
    private readonly Mock<IMessageValidator> _validatorMock;
    private readonly Mock<ILogger<MessageProcessingService>> _loggerMock;
    
    public MessageProcessingServiceTests()
    {
        _aiClientMock = new Mock<IAiClient>(MockBehavior.Strict);
        _validatorMock = new Mock<IMessageValidator>(MockBehavior.Strict);
        _loggerMock = new Mock<ILogger<MessageProcessingService>>();
    }
    
    [Fact]
    public async Task ProcessMessage_WithValidInput_ReturnsSuccess()
    {
        // Arrange
        var message = "Test message";
        var validationResult = ValidationResult.Success();
        var processedMessage = "AI processed: Test message";
        
        _validatorMock
            .Setup(x => x.Validate(message))
            .Returns(validationResult);
            
        _aiClientMock
            .Setup(x => x.ProcessAsync(message, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(processedMessage));
        
        var service = new MessageProcessingService(
            _aiClientMock.Object, 
            _validatorMock.Object, 
            _loggerMock.Object);
        
        // Act
        var result = await service.ProcessMessageAsync(message, CancellationToken.None);
        
        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(processedMessage, result.Value);
        
        // Verify all expected interactions occurred
        _validatorMock.Verify(x => x.Validate(message), Times.Once);
        _aiClientMock.Verify(x => x.ProcessAsync(message, It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

## Behavior Verification Patterns

### 1. Method Call Verification

```csharp
// Basic verification
mock.Verify(x => x.Method(), Times.Once);
mock.Verify(x => x.Method(), Times.Never);
mock.Verify(x => x.Method(), Times.AtLeastOnce);
mock.Verify(x => x.Method(), Times.Between(1, 3, Range.Inclusive));

// Verify with specific arguments
mock.Verify(x => x.Save(It.Is<Entity>(e => e.Id == expectedId)), Times.Once);

// Verify async methods
mock.Verify(x => x.SaveAsync(It.IsAny<Entity>(), It.IsAny<CancellationToken>()), Times.Once);
```

### 2. Property Access Verification

```csharp
// Verify property getter was called
mock.VerifyGet(x => x.Property, Times.Once);

// Verify property setter was called
mock.VerifySet(x => x.Property = It.IsAny<string>(), Times.Once);

// Verify specific property value was set
mock.VerifySet(x => x.Property = "expected value", Times.Once);
```

### 3. Comprehensive Verification

```csharp
// Verify all configured setups were called
mock.VerifyAll();

// Verify no other calls were made beyond setups
mock.VerifyNoOtherCalls();
```

## Mock Configuration and Lifetime Management

### 1. MockBehavior Configuration

```csharp
// Strict behavior (recommended) - throws on unconfigured calls
var strictMock = new Mock<IService>(MockBehavior.Strict);

// Loose behavior (default) - returns default values for unconfigured calls
var looseMock = new Mock<IService>(MockBehavior.Loose);

// Default behavior (same as Loose)
var defaultMock = new Mock<IService>(MockBehavior.Default);
```

### 2. Mock Factory Pattern

```csharp
public static class MockFactory
{
    public static Mock<T> CreateStrictMock<T>(MockBehavior behavior = MockBehavior.Strict) 
        where T : class
    {
        return new Mock<T>(behavior);
    }
    
    public static Mock<T> CreateLooseMock<T>() where T : class
    {
        return new Mock<T>(MockBehavior.Loose);
    }
}
```

### 3. Base Test Class Pattern

```csharp
public abstract class TestBase : IDisposable
{
    protected readonly List<Mock> _mocks = new();
    
    protected Mock<T> CreateMock<T>(MockBehavior behavior = MockBehavior.Strict) where T : class
    {
        var mock = new Mock<T>(behavior);
        _mocks.Add(mock);
        return mock;
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Verify all mocks if needed
            foreach (var mock in _mocks)
            {
                // Optional: Add verification logic
            }
            _mocks.Clear();
        }
    }
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
```

## Integration with xUnit Framework

### 1. Test Class Structure

```csharp
public class ServiceTests : IDisposable
{
    private readonly Mock<IDependency> _dependencyMock;
    private readonly Service _sut; // System Under Test
    
    public ServiceTests()
    {
        _dependencyMock = new Mock<IDependency>(MockBehavior.Strict);
        _sut = new Service(_dependencyMock.Object);
    }
    
    [Fact]
    public async Task Method_Scenario_ExpectedResult()
    {
        // Arrange
        _dependencyMock
            .Setup(x => x.DoSomethingAsync())
            .ReturnsAsync("result");
        
        // Act
        var result = await _sut.ProcessAsync();
        
        // Assert
        Assert.Equal("expected", result);
        _dependencyMock.Verify(x => x.DoSomethingAsync(), Times.Once);
    }
    
    public void Dispose()
    {
        _dependencyMock?.Reset();
    }
}
```

### 2. Theory Tests with Mock Setup

```csharp
[Theory]
[InlineData("input1", "expected1")]
[InlineData("input2", "expected2")]
public async Task ProcessInput_VariousInputs_ReturnsExpectedResults(string input, string expected)
{
    // Arrange
    _dependencyMock
        .Setup(x => x.Transform(input))
        .Returns(expected);
    
    // Act
    var result = await _sut.ProcessInputAsync(input);
    
    // Assert
    Assert.Equal(expected, result);
}
```

### 3. Test Fixtures for Shared Setup

```csharp
public class ServiceTestFixture : IDisposable
{
    public Mock<ISharedDependency> SharedDependencyMock { get; }
    
    public ServiceTestFixture()
    {
        SharedDependencyMock = new Mock<ISharedDependency>(MockBehavior.Strict);
        // Common setup
        SharedDependencyMock
            .Setup(x => x.GetConfiguration())
            .Returns(new Configuration());
    }
    
    public void Dispose()
    {
        SharedDependencyMock?.Reset();
    }
}

public class ServiceTests : IClassFixture<ServiceTestFixture>
{
    private readonly ServiceTestFixture _fixture;
    
    public ServiceTests(ServiceTestFixture fixture)
    {
        _fixture = fixture;
    }
    
    [Fact]
    public void Test_UsesSharedFixture()
    {
        // Use _fixture.SharedDependencyMock
    }
}
```

## Performance Considerations

### 1. Mock Creation Overhead

```csharp
// ❌ Avoid creating mocks in test methods
[Fact]
public void BadExample()
{
    var mock = new Mock<IService>(); // Created for each test run
    // test logic
}

// ✅ Create mocks in constructor or field initialization
private readonly Mock<IService> _serviceMock = new(MockBehavior.Strict);
```

### 2. Memory Management

```csharp
// ✅ Reset mocks between tests to prevent memory leaks
public void Dispose()
{
    _serviceMock?.Reset();
    _repositoryMock?.Reset();
}

// ✅ Use weak event patterns for long-running tests
mock.Setup(x => x.Event += It.IsAny<EventHandler>());
```

### 3. Setup Optimization

```csharp
// ❌ Avoid overly generic setups
mock.Setup(x => x.Method(It.IsAny<object>())).Returns(anything);

// ✅ Use specific setups for better performance and clarity
mock.Setup(x => x.Method(It.Is<Entity>(e => e.IsValid))).Returns(validResult);
```

## Best Practices for Maintainable Test Mocks

### 1. Mock Organization

```csharp
// ✅ Group related mocks in test classes
public class OrderProcessingServiceTests
{
    private readonly Mock<IOrderRepository> _orderRepositoryMock;
    private readonly Mock<IPaymentService> _paymentServiceMock;
    private readonly Mock<IInventoryService> _inventoryServiceMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    
    // Group setup methods
    private void SetupValidOrder(Order order) { /* setup logic */ }
    private void SetupPaymentSuccess() { /* setup logic */ }
    private void SetupInventoryAvailable() { /* setup logic */ }
}
```

### 2. Readable Test Names and Structure

```csharp
[Fact]
public async Task ProcessOrder_WhenPaymentFails_ShouldReturnPaymentError()
{
    // Arrange
    var order = CreateValidOrder();
    SetupInventoryAvailable();
    _paymentServiceMock
        .Setup(x => x.ProcessPaymentAsync(It.IsAny<Payment>()))
        .ReturnsAsync(Result.Failure(PaymentError.InsufficientFunds));
    
    // Act
    var result = await _orderService.ProcessOrderAsync(order);
    
    // Assert
    Assert.True(result.IsFailure);
    Assert.Equal(PaymentError.InsufficientFunds, result.Error);
    
    // Verify no inventory was reserved since payment failed
    _inventoryServiceMock.Verify(
        x => x.ReserveItemsAsync(It.IsAny<List<OrderItem>>()),
        Times.Never);
}
```

### 3. Mock Data Builders

```csharp
public class OrderBuilder
{
    private Order _order = new();
    
    public OrderBuilder WithId(Guid id)
    {
        _order.Id = id;
        return this;
    }
    
    public OrderBuilder WithCustomer(Customer customer)
    {
        _order.Customer = customer;
        return this;
    }
    
    public OrderBuilder WithItems(params OrderItem[] items)
    {
        _order.Items.AddRange(items);
        return this;
    }
    
    public Order Build() => _order;
    
    public static implicit operator Order(OrderBuilder builder) => builder.Build();
}

// Usage in tests
var order = new OrderBuilder()
    .WithId(Guid.NewGuid())
    .WithCustomer(testCustomer)
    .WithItems(item1, item2);
```

## Common Pitfalls and Solutions

### 1. Strict vs Loose Mock Behavior

```csharp
// ❌ Problem: Unexpected behavior with loose mocks
var looseMock = new Mock<IService>(); // Defaults to Loose
var result = looseMock.Object.GetValue(); // Returns null/default without warning

// ✅ Solution: Use strict mocks for better test reliability
var strictMock = new Mock<IService>(MockBehavior.Strict);
// Will throw MockException if GetValue() is called without setup
```

### 2. Over-mocking Value Objects

```csharp
// ❌ Don't mock value objects or DTOs
var mockDto = new Mock<OrderDto>(); // Value object - should not be mocked

// ✅ Create actual instances of value objects
var orderDto = new OrderDto(orderId, customerId, orderDate);
```

### 3. Verification Order Issues

```csharp
// ❌ Problem: Verifying in wrong order can cause test failures
mock.Verify(x => x.Method2(), Times.Once); // This might not have been called yet
mock.Verify(x => x.Method1(), Times.Once);

// ✅ Solution: Use MockSequence or verify at the end
var sequence = new MockSequence();
mock.InSequence(sequence).Setup(x => x.Method1());
mock.InSequence(sequence).Setup(x => x.Method2());
```

## Troubleshooting Guide

### Common Error Messages

1. **MockException: "Method was not called"**
   - Ensure setup matches actual method signature exactly
   - Check parameter matchers (`It.IsAny<T>()` vs specific values)

2. **MockException: "Strict mock behavior requires setups"**
   - Add setup for the method being called
   - Consider if the method should actually be called in this test scenario

3. **InvalidOperationException: "Mock type has already been configured"**
   - Avoid calling setup methods after obtaining mock.Object
   - Reset mock between tests if reusing

### Debugging Tips

```csharp
// Enable mock call tracking
mock.Setup(x => x.Method())
    .Callback(() => Console.WriteLine("Method was called"))
    .Returns(result);

// Verify specific call patterns
mock.Verify(x => x.Method(
    It.Is<string>(s => s.Contains("expected")),
    It.IsAny<int>()), Times.Once);
```

---

## Summary

Moq 4.20.72 provides robust mocking capabilities essential for testing Clean Architecture applications. Key takeaways:

1. **Use MockBehavior.Strict** by default for more reliable tests
2. **Leverage ReturnsAsync()** for modern async/await patterns
3. **Structure mocks using builders** for complex test scenarios
4. **Verify interactions explicitly** to ensure proper behavior
5. **Organize mocks by domain** to maintain test readability
6. **Reset mocks properly** to prevent memory leaks in test suites

This implementation guide enables comprehensive unit testing of the Axon Backend's Clean Architecture layers while maintaining high code quality and test reliability.