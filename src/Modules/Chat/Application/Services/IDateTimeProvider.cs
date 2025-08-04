namespace Axon.Modules.Chat.Application.Services;

/// <summary>
/// Service interface for providing current date and time values
/// Enables testable time-dependent operations and consistent UTC time usage
/// </summary>
public interface IDateTimeProvider
{
    /// <summary>
    /// Gets the current date and time in UTC
    /// </summary>
    DateTime UtcNow { get; }

    /// <summary>
    /// Gets the current date and time in local time zone
    /// </summary>
    DateTime Now { get; }

    /// <summary>
    /// Gets the current date (without time component) in UTC
    /// </summary>
    DateOnly UtcToday { get; }

    /// <summary>
    /// Gets the current date (without time component) in local time zone
    /// </summary>
    DateOnly Today { get; }
}