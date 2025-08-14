using BuildingBlocks.Application.Configuration;
using BuildingBlocks.Application.Events.Collecting;
using BuildingBlocks.Application.Events.Dispatching;
using BuildingBlocks.Application.Events.Enveloping;
using BuildingBlocks.Application.Events.Notifications;
using BuildingBlocks.Application.Events.Publishing;
using BuildingBlocks.Application.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace BuildingBlocks.Tests.Application.Events;

/// <summary>
/// Basic sanity tests to verify the Application eventing pipeline is properly configured.
/// These tests validate service registration and basic component instantiation.
/// </summary>
public class EventingSanityTests
{
    [Fact]
    public void AddOutboxFacade_ShouldRegisterAllRequiredServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddOutboxFacade();
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Core eventing services should be registered
        serviceProvider.GetService<IOutboxService>().ShouldNotBeNull();
        serviceProvider.GetService<IDomainEventCollector>().ShouldNotBeNull();
        serviceProvider.GetService<IIntegrationEventDispatcher>().ShouldNotBeNull();
        serviceProvider.GetService<IPostCommitDomainEventPublisher>().ShouldNotBeNull();
        serviceProvider.GetService<IIntegrationEventPublisher>().ShouldNotBeNull();
        serviceProvider.GetService<IEnvelopeContextAccessor>().ShouldNotBeNull();
    }

    [Fact]
    public void AddOutboxFacadeWithTransactions_ShouldRegisterTransactionBehavior()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddOutboxFacadeWithTransactions();
        var serviceProvider = services.BuildServiceProvider();

        // Assert - All facade services plus transaction behavior
        serviceProvider.GetService<IOutboxService>().ShouldNotBeNull();
        serviceProvider.GetService<IDomainEventCollector>().ShouldNotBeNull();
        serviceProvider.GetService<IIntegrationEventDispatcher>().ShouldNotBeNull();
        serviceProvider.GetService<IPostCommitDomainEventPublisher>().ShouldNotBeNull();
        serviceProvider.GetService<IIntegrationEventPublisher>().ShouldNotBeNull();
        serviceProvider.GetService<IEnvelopeContextAccessor>().ShouldNotBeNull();
        
        // Transaction behavior should be registered
        var pipelineBehaviors = serviceProvider.GetServices<MediatR.IPipelineBehavior<object, object>>();
        pipelineBehaviors.ShouldNotBeEmpty();
    }

    [Fact]
    public void OutboxService_ShouldUseNoOpPublisherByDefault()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddOutboxFacade();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var publisher = serviceProvider.GetService<IIntegrationEventPublisher>();

        // Assert - Default should be NoOpIntegrationEventPublisher
        publisher.ShouldNotBeNull();
        publisher.ShouldBeOfType<NoOpIntegrationEventPublisher>();
    }

    [Fact]
    public void DomainEventCollector_ShouldBeRegisteredAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddOutboxFacade();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var collector1 = serviceProvider.GetService<IDomainEventCollector>();
        var collector2 = serviceProvider.GetService<IDomainEventCollector>();

        // Assert - Should be same instance (singleton)
        collector1.ShouldNotBeNull();
        collector2.ShouldNotBeNull();
        collector1.ShouldBeSameAs(collector2);
        collector1.ShouldBeOfType<EfDomainEventCollector>();
    }

    [Fact]
    public void EnvelopeContextAccessor_ShouldBeRegisteredAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddOutboxFacade();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var accessor1 = serviceProvider.GetService<IEnvelopeContextAccessor>();
        var accessor2 = serviceProvider.GetService<IEnvelopeContextAccessor>();

        // Assert - Should be same instance (singleton)
        accessor1.ShouldNotBeNull();
        accessor2.ShouldNotBeNull();
        accessor1.ShouldBeSameAs(accessor2);
        accessor1.ShouldBeOfType<AsyncLocalEnvelopeContextAccessor>();
    }

    [Fact]
    public void EnvelopeContextAccessor_ShouldSupportNestedContexts()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddOutboxFacade();
        var serviceProvider = services.BuildServiceProvider();
        var accessor = serviceProvider.GetRequiredService<IEnvelopeContextAccessor>();

        var context1 = new IntegrationEnvelopeContext("trace1", Guid.NewGuid(), "tenant1");
        var context2 = new IntegrationEnvelopeContext("trace2", Guid.NewGuid(), "tenant2");

        // Act & Assert - Should support nesting
        accessor.Current.ShouldBeNull();

        using (var scope1 = accessor.Push(context1))
        {
            accessor.Current.ShouldBe(context1);

            using (var scope2 = accessor.Push(context2))
            {
                accessor.Current.ShouldBe(context2);
            }

            // Should restore previous context after nested scope ends
            accessor.Current.ShouldBe(context1);
        }

        // Should restore null after all scopes end
        accessor.Current.ShouldBeNull();
    }

    [Fact]
    public void IntegrationEventDispatcher_ShouldAcceptDomainEventsCollection()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddOutboxFacade();
        var serviceProvider = services.BuildServiceProvider();
        var dispatcher = serviceProvider.GetRequiredService<IIntegrationEventDispatcher>();

        // Act & Assert - Should not throw when called with empty collection
        var task = dispatcher.SendAsync(new List<BuildingBlocks.Core.Domain.Events.IDomainEvent>());
        task.ShouldNotBeNull();
        task.IsCompleted.ShouldBeTrue(); // NoOp publisher completes synchronously
    }

    [Fact]
    public void PostCommitDomainEventPublisher_ShouldAcceptDomainEventsCollection()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddOutboxFacade();
        var serviceProvider = services.BuildServiceProvider();
        var publisher = serviceProvider.GetRequiredService<IPostCommitDomainEventPublisher>();

        // Act & Assert - Should not throw when called with empty collection
        var task = publisher.PublishAsync(new List<BuildingBlocks.Core.Domain.Events.IDomainEvent>());
        task.ShouldNotBeNull();
        task.IsCompleted.ShouldBeTrue(); // MediatR with empty collection completes synchronously
    }
}