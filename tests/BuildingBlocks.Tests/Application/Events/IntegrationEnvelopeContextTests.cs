namespace Axon.BuildingBlocks.Tests.Application.Events;

using Axon.BuildingBlocks.Application.Events.Enveloping;
using FluentAssertions;
using Xunit;

public sealed class IntegrationEnvelopeContextTests
{
    [Fact]
    public void RequestIdGuid_Should_Return_Parsed_RequestIdString_When_Valid_Guid()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var context = new IntegrationEnvelopeContext(
            TraceId: "trace-123",
            RequestId: null,
            TenantId: "tenant-123",
            Metadata: null,
            RequestIdString: guid.ToString());

        // Act
        var result = context.RequestIdGuid;

        // Assert
        result.Should().Be(guid);
    }

    [Fact]
    public void RequestIdGuid_Should_Return_RequestId_When_RequestIdString_Invalid()
    {
        // Arrange
        var fallbackGuid = Guid.NewGuid();
        var context = new IntegrationEnvelopeContext(
            TraceId: "trace-123",
            RequestId: fallbackGuid,
            TenantId: "tenant-123",
            Metadata: null,
            RequestIdString: "not-a-guid");

        // Act
        var result = context.RequestIdGuid;

        // Assert
        result.Should().Be(fallbackGuid);
    }

    [Fact]
    public void RequestIdGuid_Should_Return_Null_When_Both_Invalid()
    {
        // Arrange
        var context = new IntegrationEnvelopeContext(
            TraceId: "trace-123",
            RequestId: null,
            TenantId: "tenant-123",
            Metadata: null,
            RequestIdString: "not-a-guid");

        // Act
        var result = context.RequestIdGuid;

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void RequestIdGuid_Should_Return_RequestId_When_RequestIdString_Null()
    {
        // Arrange
        var fallbackGuid = Guid.NewGuid();
        var context = new IntegrationEnvelopeContext(
            TraceId: "trace-123",
            RequestId: fallbackGuid,
            TenantId: "tenant-123",
            Metadata: null,
            RequestIdString: null);

        // Act
        var result = context.RequestIdGuid;

        // Assert
        result.Should().Be(fallbackGuid);
    }

    [Fact]
    public void RequestIdGuid_Should_Support_W3C_TraceId_Format()
    {
        // Arrange - W3C TraceId format should NOT be parsed as Guid
        var w3cTraceId = "4bf92f3577b34da6a3ce929d0e0e4736";
        var fallbackGuid = Guid.NewGuid();
        var context = new IntegrationEnvelopeContext(
            TraceId: "trace-123",
            RequestId: fallbackGuid,
            TenantId: "tenant-123",
            Metadata: null,
            RequestIdString: w3cTraceId);

        // Act
        var result = context.RequestIdGuid;

        // Assert
        result.Should().Be(fallbackGuid); // Should fall back since W3C format is not a valid Guid
    }

    [Fact]
    public void RequestIdString_Should_Be_Preserved_As_Is()
    {
        // Arrange
        var w3cTraceId = "4bf92f3577b34da6a3ce929d0e0e4736";
        var context = new IntegrationEnvelopeContext(
            TraceId: "trace-123",
            RequestId: null,
            TenantId: "tenant-123",
            Metadata: null,
            RequestIdString: w3cTraceId);

        // Act & Assert
        context.RequestIdString.Should().Be(w3cTraceId);
    }

    [Fact]
    public void Constructor_Should_Allow_Default_Parameters()
    {
        // Act
        var context = new IntegrationEnvelopeContext(
            TraceId: "trace-123",
            RequestId: null,
            TenantId: "tenant-123",
            Metadata: null);

        // Assert
        context.RequestIdString.Should().BeNull();
        context.RequestIdGuid.Should().BeNull();
    }
}