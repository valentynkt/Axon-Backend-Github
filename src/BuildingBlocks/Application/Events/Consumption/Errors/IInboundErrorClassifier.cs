using BuildingBlocks.Application.Events.Enveloping;

namespace BuildingBlocks.Application.Events.Consumption.Errors;

/// <summary>
/// Classifies integration event handler failures to determine retry vs dead-letter handling.
/// Implementation should analyze exception types, envelope context, and event type to make decisions.
/// </summary>
public interface IInboundErrorClassifier
{
    /// <summary>
    /// Classifies a handler failure exception to determine appropriate error handling strategy.
    /// </summary>
    /// <param name="exception">The exception thrown during handler execution</param>
    /// <param name="envelope">The integration event envelope being processed</param>
    /// <param name="eventType">The CLR type of the integration event</param>
    /// <returns>Classification indicating whether the error is transient, permanent, or unclassified</returns>
    InboundErrorKind Classify(Exception exception, IntegrationEventEnvelope envelope, Type eventType);
}