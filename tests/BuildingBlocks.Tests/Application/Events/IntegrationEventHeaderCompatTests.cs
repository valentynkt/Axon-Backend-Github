namespace Axon.BuildingBlocks.Tests.Application.Events;

using Axon.BuildingBlocks.Application.Events.Enveloping;
using FluentAssertions;
using Xunit;

public sealed class IntegrationEventHeaderCompatTests
{
    [Fact]
    public void ReadIdempotencyKey_Should_Return_Value_From_New_Header()
    {
        // Arrange
        var headers = new Dictionary<string, object>
        {
            [IntegrationEventHeaders.IdempotencyKey] = "test-idempotency-key"
        };

        // Act
        var result = IntegrationEventHeaderCompat.ReadIdempotencyKey(headers);

        // Assert
        result.Should().Be("test-idempotency-key");
    }

    [Fact]
    public void ReadDeliveryAttempt_Should_Return_Parsed_Value()
    {
        // Arrange
        var headers = new Dictionary<string, object>
        {
            [IntegrationEventHeaders.DeliveryAttempt] = "3"
        };

        // Act
        var result = IntegrationEventHeaderCompat.ReadDeliveryAttempt(headers);

        // Assert
        result.Should().Be(3);
    }

    [Fact]
    public void ReadDeliveryAttempt_Should_Return_1_For_Missing_Header()
    {
        // Arrange
        var headers = new Dictionary<string, object>();

        // Act
        var result = IntegrationEventHeaderCompat.ReadDeliveryAttempt(headers);

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public void ReadDeliveryAttempt_Should_Return_1_For_Invalid_Value()
    {
        // Arrange
        var headers = new Dictionary<string, object>
        {
            [IntegrationEventHeaders.DeliveryAttempt] = "invalid"
        };

        // Act
        var result = IntegrationEventHeaderCompat.ReadDeliveryAttempt(headers);

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public void ReadTraceId_Should_Return_Value_From_New_Header()
    {
        // Arrange
        var headers = new Dictionary<string, object>
        {
            [IntegrationEventHeaders.TraceId] = "trace-123"
        };

        // Act
        var result = IntegrationEventHeaderCompat.ReadTraceId(headers);

        // Assert
        result.Should().Be("trace-123");
    }

    [Fact]
    public void ReadTraceId_Should_Fallback_To_Legacy_Header()
    {
        // Arrange
        var headers = new Dictionary<string, object>
        {
            [IntegrationEventHeaders.SpanId] = "legacy-span-123"
        };

        // Act
        var result = IntegrationEventHeaderCompat.ReadTraceId(headers);

        // Assert
        result.Should().Be("legacy-span-123");
    }

    [Fact]
    public void ReadRequestIdString_Should_Return_String_Value()
    {
        // Arrange
        var headers = new Dictionary<string, object>
        {
            [IntegrationEventHeaders.RequestId] = "request-123"
        };

        // Act
        var result = IntegrationEventHeaderCompat.ReadRequestIdString(headers);

        // Assert
        result.Should().Be("request-123");
    }

    [Fact]
    public void ReadCorrelationId_Should_Support_Non_Guid_Values()
    {
        // Arrange - W3C TraceId format
        var w3cTraceId = "4bf92f3577b34da6a3ce929d0e0e4736";
        var headers = new Dictionary<string, object>
        {
            [IntegrationEventHeaders.CorrelationId] = w3cTraceId
        };

        // Act
        var result = IntegrationEventHeaderCompat.ReadCorrelationId(headers);

        // Assert
        result.Should().Be(w3cTraceId);
    }

    [Fact]
    public void ReadOutboxEntryId_Should_Parse_Guid_From_New_Header()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var headers = new Dictionary<string, object>
        {
            [IntegrationEventHeaders.OutboxEntryId] = guid.ToString()
        };

        // Act
        var result = IntegrationEventHeaderCompat.ReadOutboxEntryId(headers);

        // Assert
        result.Should().Be(guid);
    }

    [Fact]
    public void ReadOutboxEntryId_Should_Fallback_To_Legacy_Header()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var headers = new Dictionary<string, object>
        {
            [IntegrationEventHeaders.OutboxEntryIdOld] = guid.ToString()
        };

        // Act
        var result = IntegrationEventHeaderCompat.ReadOutboxEntryId(headers);

        // Assert
        result.Should().Be(guid);
    }

    [Fact]
    public void ReadOutboxEntryId_Should_Return_Null_For_Invalid_Guid()
    {
        // Arrange
        var headers = new Dictionary<string, object>
        {
            [IntegrationEventHeaders.OutboxEntryId] = "not-a-guid"
        };

        // Act
        var result = IntegrationEventHeaderCompat.ReadOutboxEntryId(headers);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ReadTenantId_Should_Fallback_Through_Multiple_Headers()
    {
        // Arrange
        var headers = new Dictionary<string, object>
        {
            [IntegrationEventHeaders.UserId] = "user-123" // Should fallback to this
        };

        // Act
        var result = IntegrationEventHeaderCompat.ReadTenantId(headers);

        // Assert
        result.Should().Be("user-123");
    }

    [Fact]
    public void ReadTenantId_Should_Prefer_New_Header_Over_Legacy()
    {
        // Arrange
        var headers = new Dictionary<string, object>
        {
            [IntegrationEventHeaders.TenantId] = "tenant-123",
            [IntegrationEventHeaders.UserId] = "user-456"
        };

        // Act
        var result = IntegrationEventHeaderCompat.ReadTenantId(headers);

        // Assert
        result.Should().Be("tenant-123"); // Should use new header
    }

    [Fact]
    public void ParseRequestIdAsGuid_Should_Parse_Valid_Guid()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var guidString = guid.ToString();

        // Act
        var result = IntegrationEventHeaderCompat.ParseRequestIdAsGuid(guidString);

        // Assert
        result.Should().Be(guid);
    }

    [Fact]
    public void ParseRequestIdAsGuid_Should_Return_Null_For_Invalid_Guid()
    {
        // Act
        var result = IntegrationEventHeaderCompat.ParseRequestIdAsGuid("not-a-guid");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ParseRequestIdAsGuid_Should_Return_Null_For_W3C_TraceId()
    {
        // Arrange - W3C TraceId format (32 hex chars)
        var w3cTraceId = "4bf92f3577b34da6a3ce929d0e0e4736";

        // Act
        var result = IntegrationEventHeaderCompat.ParseRequestIdAsGuid(w3cTraceId);

        // Assert
        result.Should().BeNull(); // Should not parse W3C format as Guid
    }
}