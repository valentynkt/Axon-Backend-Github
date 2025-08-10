using Ardalis.GuardClauses;
using BuildingBlocks.Core.Domain.Entities.Abstractions;

namespace BuildingBlocks.Infrastructure.Persistence.PersistMessageProcessor;

public class PersistMessage : IVersioned
{
    // Private constructor for EF Core
    private PersistMessage()
    {
        DataType = string.Empty;
        Data = string.Empty;
    }

    public PersistMessage(Guid id, string dataType, string data, MessageDeliveryType deliveryType)
    {
        Id = id;
        DataType = Guard.Against.NullOrWhiteSpace(dataType, nameof(dataType));
        Data = Guard.Against.NullOrWhiteSpace(data, nameof(data));
        DeliveryType = deliveryType;
        Created = DateTime.Now;
        MessageStatus = MessageStatus.InProgress;
        RetryCount = 0;
    }

    public Guid Id { get; private set; }
    public string DataType { get; private set; } = string.Empty;
    public string Data { get; private set; } = string.Empty;
    public DateTime Created { get; private set; }
    public int RetryCount { get; private set; }
    public MessageStatus MessageStatus { get; private set; }
    public MessageDeliveryType DeliveryType { get; private set; }
    public uint Version { get; set; }

    public void ChangeState(MessageStatus messageStatus)
    {
        MessageStatus = messageStatus;
    }

    public void IncreaseRetry()
    {
        RetryCount++;
    }
}