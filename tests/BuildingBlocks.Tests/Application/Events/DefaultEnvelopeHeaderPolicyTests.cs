using BuildingBlocks.Application.Events.Enveloping;
using BuildingBlocks.Core.Domain.Events;

namespace BuildingBlocks.Tests.Application.Events;

[TestFixture]
public sealed class DefaultEnvelopeHeaderPolicyTests
{
    private DefaultEnvelopeHeaderPolicy _policy = null!;

    [SetUp]
    public void SetUp()
    {
        _policy = new DefaultEnvelopeHeaderPolicy();
    }

    [Test]
    public void Apply_SetsNewKebabCaseHeaders()
    {
        // Arrange
        var eventTypeName = "UserCreated";
        var schemaVersion = "1.0";
        
        // Act
        var headers = _policy.Apply(
            domainEvent: null,
            eventTypeName: eventTypeName,
            schemaVersion: schemaVersion,
            context: null,
            existingHeaders: null,
            payloadHashProvider: () => "test-hash");

        // Assert - NEW KEBAB-CASE HEADERS
        headers.ShouldContainKey(IntegrationEventHeaders.IntegrationEventId);
        headers.ShouldContainKey(IntegrationEventHeaders.IntegrationEventType);
        headers.ShouldContainKey(IntegrationEventHeaders.IntegrationSchemaVersion);
        headers.ShouldContainKey(IntegrationEventHeaders.ProducedAt);
        headers.ShouldContainKey(IntegrationEventHeaders.IdempotencyKey);

        headers[IntegrationEventHeaders.IntegrationEventType].ShouldBe(eventTypeName);
        headers[IntegrationEventHeaders.IntegrationSchemaVersion].ShouldBe(schemaVersion);
    }

    [Test]
    public void Apply_SetsLegacyHeadersForBackwardCompatibility()
    {
        // Arrange
        var eventTypeName = "UserCreated";
        var schemaVersion = "1.0";
        
        // Act
        var headers = _policy.Apply(
            domainEvent: null,
            eventTypeName: eventTypeName,
            schemaVersion: schemaVersion,
            context: null,
            existingHeaders: null,
            payloadHashProvider: () => "test-hash");

        // Assert - DUAL-WRITE LEGACY HEADERS
        headers.ShouldContainKey(IntegrationEventHeaders.IntegrationEventIdOld);
        headers.ShouldContainKey(IntegrationEventHeaders.IntegrationEventTypeOld);
        headers.ShouldContainKey(IntegrationEventHeaders.IntegrationSchemaVersionOld);
        headers.ShouldContainKey(IntegrationEventHeaders.EventId);
        headers.ShouldContainKey(IntegrationEventHeaders.EventType);
        headers.ShouldContainKey(IntegrationEventHeaders.OccurredAt);

        // Values should match between new and old headers
        headers[IntegrationEventHeaders.IntegrationEventType].ShouldBe(headers[IntegrationEventHeaders.IntegrationEventTypeOld]);
        headers[IntegrationEventHeaders.IntegrationEventType].ShouldBe(headers[IntegrationEventHeaders.EventType]);
        headers[IntegrationEventHeaders.IntegrationEventId].ShouldBe(headers[IntegrationEventHeaders.IntegrationEventIdOld]);
        headers[IntegrationEventHeaders.IntegrationEventId].ShouldBe(headers[IntegrationEventHeaders.EventId]);
    }

    [Test]
    public void Apply_WithTraceId_SetsTraceAndCorrelationHeaders()
    {
        // Arrange
        var traceId = "12345-trace-id";
        var context = new IntegrationEnvelopeContext
        {
            TraceId = traceId
        };
        
        // Act
        var headers = _policy.Apply(
            domainEvent: null,
            eventTypeName: "TestEvent",
            schemaVersion: "1.0",
            context: context,
            existingHeaders: null,
            payloadHashProvider: () => "test-hash");

        // Assert
        headers.ShouldContainKey(IntegrationEventHeaders.TraceId);
        headers.ShouldContainKey(IntegrationEventHeaders.CorrelationId);
        
        headers[IntegrationEventHeaders.TraceId].ShouldBe(traceId);
        headers[IntegrationEventHeaders.CorrelationId].ShouldBe(traceId); // correlation.id = trace.id preferred
    }

    [Test]
    public void Apply_WithRequestIdButNoTraceId_UsesRequestIdAsCorrelation()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var context = new IntegrationEnvelopeContext
        {
            RequestId = requestId
        };
        
        // Act
        var headers = _policy.Apply(
            domainEvent: null,
            eventTypeName: "TestEvent",
            schemaVersion: "1.0",
            context: context,
            existingHeaders: null,
            payloadHashProvider: () => "test-hash");

        // Assert
        headers.ShouldContainKey(IntegrationEventHeaders.RequestId);
        headers.ShouldContainKey(IntegrationEventHeaders.CorrelationId);
        
        headers[IntegrationEventHeaders.RequestId].ShouldBe(requestId.ToString("D"));
        headers[IntegrationEventHeaders.CorrelationId].ShouldBe(requestId.ToString("D"));
    }

    [Test]
    public void Apply_WithOutboxEntryId_SetsBothNewAndOldOutboxHeaders()
    {
        // Arrange
        var outboxId = Guid.NewGuid();
        var context = new IntegrationEnvelopeContext
        {
            OutboxEntryId = outboxId
        };
        
        // Act
        var headers = _policy.Apply(
            domainEvent: null,
            eventTypeName: "TestEvent",
            schemaVersion: "1.0",
            context: context,
            existingHeaders: null,
            payloadHashProvider: () => "test-hash");

        // Assert - Both new and old headers should be present
        headers.ShouldContainKey(IntegrationEventHeaders.OutboxEntryId);
        headers.ShouldContainKey(IntegrationEventHeaders.OutboxEntryIdOld);
        
        headers[IntegrationEventHeaders.OutboxEntryId].ShouldBe(outboxId.ToString("D"));
        headers[IntegrationEventHeaders.OutboxEntryIdOld].ShouldBe(outboxId.ToString("D"));
    }
}