namespace Axon.Modules.Chat.Application.Services.Infrastructure.Idempotency;

/// <summary>
/// Default configuration values for idempotency behavior
/// </summary>
internal static class IdempotencyDefaults
{
    /// <summary>
    /// Default time window for idempotency cache entries
    /// </summary>
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(30);
}