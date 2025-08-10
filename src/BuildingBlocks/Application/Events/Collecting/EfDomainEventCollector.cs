using System.Collections.Concurrent;
using System.Linq.Expressions;
using BuildingBlocks.Core.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Application.Events.Collecting;

/// <summary>
/// EF Core-based collector that "duck types" aggregates exposing:
///   IReadOnlyCollection{IDomainEvent} DomainEvents property
///   void ClearDomainEvents() method
/// Uses compiled expression delegates cached per aggregate CLR type.
/// </summary>
public sealed class EfDomainEventCollector : IDomainEventCollector
{
    private readonly ILogger<EfDomainEventCollector> _logger;

    private sealed record Accessors(
        Func<object, IReadOnlyCollection<IDomainEvent>> GetEvents,
        Action<object> Clear);

    private static readonly ConcurrentDictionary<Type, Accessors?> Cache = new();

    public EfDomainEventCollector(ILogger<EfDomainEventCollector> logger) => _logger = logger;

    public IReadOnlyList<IDomainEvent> Collect(DbContext dbContext, bool clear = true)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var aggregates = dbContext.ChangeTracker
            .Entries()
            .Select(e => e.Entity)
            .Where(e => e is not null)
            .Distinct()
            .ToList();

        if (aggregates.Count == 0)
            return Array.Empty<IDomainEvent>();

        var events = new List<IDomainEvent>(capacity: 16);

        foreach (var agg in aggregates)
        {
            var acc = GetAccessors(agg.GetType());
            if (acc is null) continue;

            var evts = acc.GetEvents(agg);
            if (evts is null || evts.Count == 0) continue;

            events.AddRange(evts);
            if (clear) acc.Clear(agg);
        }

        if (events.Count > 0)
        {
            _logger.LogDebug("Collected {Count} domain events from {AggCount} tracked objects.", events.Count, aggregates.Count);
        }

        return events;
    }

    private static Accessors? GetAccessors(Type type)
    {
        if (Cache.TryGetValue(type, out var cached)) return cached;

        // Find DomainEvents property
        var prop = type.GetProperty("DomainEvents", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (prop is null || !typeof(IEnumerable<IDomainEvent>).IsAssignableFrom(prop.PropertyType))
        {
            Cache[type] = null;
            return null;
        }

        // Find ClearDomainEvents method
        var clear = type.GetMethod("ClearDomainEvents", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (clear is null || clear.GetParameters().Length != 0)
        {
            Cache[type] = null;
            return null;
        }

        // Build getter: (object o) => ((T)o).DomainEvents as IReadOnlyCollection<IDomainEvent>
        var objParam = Expression.Parameter(typeof(object), "o");
        var typed = Expression.Convert(objParam, type);
        var propAccess = Expression.Property(typed, prop);
        var castToReadOnly = Expression.Convert(propAccess, typeof(IReadOnlyCollection<IDomainEvent>));
        var getLambda = Expression.Lambda<Func<object, IReadOnlyCollection<IDomainEvent>>>(castToReadOnly, objParam).Compile();

        // Build clearer: (object o) => ((T)o).ClearDomainEvents()
        var call = Expression.Call(typed, clear);
        var clrLambda = Expression.Lambda<Action<object>>(call, objParam).Compile();

        var acc = new Accessors(getLambda, clrLambda);
        Cache[type] = acc;
        return acc;
    }
}