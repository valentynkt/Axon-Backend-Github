namespace BuildingBlocks.Core.Abstractions.Idempotency;

/// <summary>
/// Default configuration values for idempotency behavior.
/// </summary>
public static class IdempotencyDefaults
{
    /// <summary>
    /// Default time window for caching idempotent command results.
    /// </summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Default prefix used when generating idempotency keys.
    /// </summary>
    public const string KeyPrefix = "idem";

    /// <summary>
    /// Cache key for intermediate states during multi-phase operations.
    /// </summary>
    public const string IntermediateStateSuffix = ":intermediate";
}