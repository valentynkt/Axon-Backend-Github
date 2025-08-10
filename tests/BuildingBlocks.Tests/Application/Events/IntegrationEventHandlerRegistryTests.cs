using BuildingBlocks.Application.Events.Consumption;
using BuildingBlocks.Core.Abstractions.Events;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;

namespace BuildingBlocks.Tests.Application.Events;

public sealed class IntegrationEventHandlerRegistryTests
{
    [Fact]
    public void GetHandlers_WithConcreteEventType_ReturnsAdaptedHandlers()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<IIntegrationEventHandler<UserCreatedIntegrationEvent>, UserCreatedHandler1>();
        services.AddTransient<IIntegrationEventHandler<UserCreatedIntegrationEvent>, UserCreatedHandler2>();
        
        var serviceProvider = services.BuildServiceProvider();
        var registry = new IntegrationEventHandlerRegistry(serviceProvider);

        // Act
        var handlers = registry.GetHandlers(typeof(UserCreatedIntegrationEvent)).ToList();

        // Assert
        handlers.Should().HaveCount(2);
        handlers.Should().AllBeOfType<UntypedHandlerAdapter>();
    }

    [Fact]
    public async Task GetHandlers_WhenHandlersInvokedThroughBaseInterface_CallsCorrectHandlers()
    {
        // Arrange
        var handler1 = new UserCreatedHandler1();
        var handler2 = new UserCreatedHandler2();
        
        var services = new ServiceCollection();
        services.AddSingleton<IIntegrationEventHandler<UserCreatedIntegrationEvent>>(handler1);
        services.AddSingleton<IIntegrationEventHandler<UserCreatedIntegrationEvent>>(handler2);
        
        var serviceProvider = services.BuildServiceProvider();
        var registry = new IntegrationEventHandlerRegistry(serviceProvider);

        var @event = new UserCreatedIntegrationEvent("test-user-id", "test@example.com");
        var cancellationToken = CancellationToken.None;

        // Act
        var handlers = registry.GetHandlers(typeof(UserCreatedIntegrationEvent));
        
        // Execute all handlers through the base interface
        var tasks = handlers.Select(handler => handler.HandleAsync(@event, cancellationToken));
        await Task.WhenAll(tasks);

        // Assert
        handler1.WasHandled.Should().BeTrue();
        handler2.WasHandled.Should().BeTrue();
        handler1.HandledEvent.Should().Be(@event);
        handler2.HandledEvent.Should().Be(@event);
    }

    [Fact]
    public void GetHandlers_WithNonIntegrationEventType_ReturnsEmpty()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var registry = new IntegrationEventHandlerRegistry(serviceProvider);

        // Act
        var handlers = registry.GetHandlers(typeof(string));

        // Assert
        handlers.Should().BeEmpty();
    }

    [Fact]
    public void GetHandlers_WithNullEventType_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var registry = new IntegrationEventHandlerRegistry(serviceProvider);

        // Act & Assert
        var act = () => registry.GetHandlers(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetHandlers_WithNoRegisteredHandlers_ReturnsEmpty()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var registry = new IntegrationEventHandlerRegistry(serviceProvider);

        // Act
        var handlers = registry.GetHandlers(typeof(UserCreatedIntegrationEvent));

        // Assert
        handlers.Should().BeEmpty();
    }
}

// Test doubles
public sealed record UserCreatedIntegrationEvent(string UserId, string Email) : IIntegrationEvent
{
    public string Source => "TestService";
    public string? CorrelationId => null;
    public IReadOnlyDictionary<string, object>? Metadata => null;
    public Guid Id { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}

public sealed class UserCreatedHandler1 : IIntegrationEventHandler<UserCreatedIntegrationEvent>
{
    public bool WasHandled { get; private set; }
    public UserCreatedIntegrationEvent? HandledEvent { get; private set; }

    public Task HandleAsync(UserCreatedIntegrationEvent @event, CancellationToken cancellationToken)
    {
        WasHandled = true;
        HandledEvent = @event;
        return Task.CompletedTask;
    }
}

public sealed class UserCreatedHandler2 : IIntegrationEventHandler<UserCreatedIntegrationEvent>
{
    public bool WasHandled { get; private set; }
    public UserCreatedIntegrationEvent? HandledEvent { get; private set; }

    public Task HandleAsync(UserCreatedIntegrationEvent @event, CancellationToken cancellationToken)
    {
        WasHandled = true;
        HandledEvent = @event;
        return Task.CompletedTask;
    }
}