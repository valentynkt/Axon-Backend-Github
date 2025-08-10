namespace BuildingBlocks.Application.Outbox;

/// <summary>
/// Failure info computed by the Application layer (e.g., using a backoff policy),
/// then applied by the Infrastructure repository.
/// </summary>
public sealed record OutboxFailureInfo(
    Guid EntryId,
    string ErrorMessage,
    DateTime FailedAt,
    DateTime? NextRetryAtUtc = null,
    bool MoveToDeadLetter = false);
