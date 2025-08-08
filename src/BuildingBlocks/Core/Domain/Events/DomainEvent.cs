using BuildingBlocks.Core.Domain.Primitives;

namespace BuildingBlocks.Core.Domain.Events;

/// <summary>
/// Base implementation for domain events
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    protected DomainEvent()
    {
        EventId = Guid.NewGuid();
        OccurredAt = DateTime.UtcNow;
        Version = 1;
    }
    
    protected DomainEvent(Guid eventId, DateTime occurredAt, int version = 1)
    {
        EventId = eventId;
        OccurredAt = occurredAt;
        Version = version;
    }
    
    public Guid EventId { get; }
    public DateTime OccurredAt { get; }
    public int Version { get; }
}

/// <summary>
/// Example domain event: User registered
/// </summary>
public sealed record UserRegisteredEvent : DomainEvent
{
    public UserId UserId { get; }
    public Email Email { get; }
    public DateTime RegisteredAt { get; }
    
    public UserRegisteredEvent(UserId userId, Email email)
    {
        UserId = userId;
        Email = email;
        RegisteredAt = OccurredAt;
    }
    
    private UserRegisteredEvent(
        Guid eventId, 
        DateTime occurredAt, 
        int version,
        UserId userId, 
        Email email, 
        DateTime registeredAt) : base(eventId, occurredAt, version)
    {
        UserId = userId;
        Email = email;
        RegisteredAt = registeredAt;
    }
}

/// <summary>
/// Example domain event: Order placed
/// </summary>
public sealed record OrderPlacedEvent : DomainEvent
{
    public OrderId OrderId { get; }
    public UserId UserId { get; }
    public Money TotalAmount { get; }
    public int ItemCount { get; }
    
    public OrderPlacedEvent(OrderId orderId, UserId userId, Money totalAmount, int itemCount)
    {
        OrderId = orderId;
        UserId = userId;
        TotalAmount = totalAmount;
        ItemCount = itemCount;
    }
    
    private OrderPlacedEvent(
        Guid eventId,
        DateTime occurredAt,
        int version,
        OrderId orderId,
        UserId userId,
        Money totalAmount,
        int itemCount) : base(eventId, occurredAt, version)
    {
        OrderId = orderId;
        UserId = userId;
        TotalAmount = totalAmount;
        ItemCount = itemCount;
    }
}

// Dummy types for compilation. Replace with actual implementations from other modules.
public record UserId(Guid Value) : StrongId<Guid>(Value);
public record OrderId(Guid Value) : StrongId<Guid>(Value);