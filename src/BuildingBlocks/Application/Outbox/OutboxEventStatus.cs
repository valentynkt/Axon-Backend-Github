namespace BuildingBlocks.Application.Outbox;

/// <summary>Status of an outbox entry at the Application boundary.</summary>
public enum OutboxEventStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3,
    DeadLetter = 4
}