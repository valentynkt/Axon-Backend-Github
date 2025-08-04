using Axon.Modules.Chat.Application.Services;

namespace Axon.Modules.Chat.Infrastructure.Services;

/// <summary>
/// System implementation of IDateTimeProvider that provides real system time
/// Used in production to get actual current date and time values
/// </summary>
public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    /// <summary>
    /// Gets the current date and time in UTC
    /// </summary>
    public DateTime UtcNow => DateTime.UtcNow;

    /// <summary>
    /// Gets the current date and time in local time zone
    /// </summary>
    public DateTime Now => DateTime.Now;

    /// <summary>
    /// Gets the current date (without time component) in UTC
    /// </summary>
    public DateOnly UtcToday => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>
    /// Gets the current date (without time component) in local time zone
    /// </summary>
    public DateOnly Today => DateOnly.FromDateTime(DateTime.Now);
}