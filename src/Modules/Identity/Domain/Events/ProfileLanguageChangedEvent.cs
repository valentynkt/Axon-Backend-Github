namespace Axon.Modules.Identity.Domain.Events;

/// <summary>
/// Domain event raised when a principal's preferred language is changed.
/// </summary>
public sealed record ProfileLanguageChangedEvent(
    AxonId AxonId,
    string NewLanguage,
    DateTimeOffset ChangedAt
) : DomainEvent;