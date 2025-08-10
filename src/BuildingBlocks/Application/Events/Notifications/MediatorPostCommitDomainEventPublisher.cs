using System.Collections.Concurrent;
using System.Linq.Expressions;
using BuildingBlocks.Core.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Application.Events.Notifications;

/// <summary>
/// Wraps domain events into DomainEventNotification{T} and publishes via MediatR.
/// Reflection-free on hot path using compiled factory cache.
/// </summary>
public sealed class MediatorPostCommitDomainEventPublisher : IPostCommitDomainEventPublisher
{
    private readonly IMediator _mediator;
    private readonly ILogger<MediatorPostCommitDomainEventPublisher> _logger;

    private delegate INotification Factory(IDomainEvent e);

    private static readonly ConcurrentDictionary<Type, Factory> FactoryCache = new();

    public MediatorPostCommitDomainEventPublisher(IMediator mediator, ILogger<MediatorPostCommitDomainEventPublisher> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task PublishAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default)
    {
        if (events is null) return;

        var published = 0;
        foreach (var e in events)
        {
            if (e is null) continue;
            var notification = CreateNotification(e);
            await _mediator.Publish(notification, ct).ConfigureAwait(false);
            published++;
        }

        if (published > 0)
            _logger.LogDebug("Published {Count} DomainEventNotification(s) via MediatR.", published);
    }

    public Task PublishAsync(IDomainEvent @event, CancellationToken ct = default)
    {
        if (@event is null) return Task.CompletedTask;
        var notification = CreateNotification(@event);
        return _mediator.Publish(notification, ct);
    }

    private static INotification CreateNotification(IDomainEvent e)
    {
        var t = e.GetType();
        var factory = FactoryCache.GetOrAdd(t, BuildFactory);
        return factory(e);
    }

    private static Factory BuildFactory(Type eventType)
    {
        // Build: e => new DomainEventNotification<eventType>((eventType)e)
        var ctorType = typeof(DomainEventNotification<>).MakeGenericType(eventType);
        var ctor = ctorType.GetConstructor(new[] { eventType }) 
                   ?? throw new InvalidOperationException($"Missing ctor({eventType.Name}) on {ctorType.Name}");

        var param = Expression.Parameter(typeof(IDomainEvent), "e");
        var cast = Expression.Convert(param, eventType);
        var @new = Expression.New(ctor, cast);
        var lambda = Expression.Lambda<Factory>(Expression.Convert(@new, typeof(INotification)), param);
        return lambda.Compile();
    }
}