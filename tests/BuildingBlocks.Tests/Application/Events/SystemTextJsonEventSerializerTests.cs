namespace Axon.BuildingBlocks.Tests.Application.Events;

using System.Text.Json;
using Axon.BuildingBlocks.Application.Events.Serialization;
using Axon.BuildingBlocks.Core.Domain.Events;
using FluentAssertions;
using Xunit;

public sealed class SystemTextJsonEventSerializerTests
{
    private readonly SystemTextJsonEventSerializer _serializer = new();

    [Fact]
    public void Serialize_And_Deserialize_Should_Preserve_IntegrationEvent()
    {
        // Arrange
        var originalEvent = new TestIntegrationEvent(
            EventId: Guid.NewGuid(),
            OccurredAtUtc: DateTime.UtcNow,
            TestProperty: "Test Value",
            Number: 42);

        // Act
        var serialized = _serializer.Serialize(originalEvent, typeof(TestIntegrationEvent));
        var deserialized = (TestIntegrationEvent?)_serializer.Deserialize(serialized, typeof(TestIntegrationEvent));

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.EventId.Should().Be(originalEvent.EventId);
        deserialized.OccurredAtUtc.Should().BeCloseTo(originalEvent.OccurredAtUtc, TimeSpan.FromSeconds(1));
        deserialized.TestProperty.Should().Be(originalEvent.TestProperty);
        deserialized.Number.Should().Be(originalEvent.Number);
    }

    [Fact]
    public void Serialize_Should_Use_CamelCase_Naming_Policy()
    {
        // Arrange
        var testEvent = new TestIntegrationEvent(
            EventId: Guid.NewGuid(),
            OccurredAtUtc: DateTime.UtcNow,
            TestProperty: "Test Value",
            Number: 42);

        // Act
        var serialized = _serializer.Serialize(testEvent, typeof(TestIntegrationEvent));
        
        // Assert - Check that property names are camelCase
        var json = JsonDocument.Parse(serialized);
        json.RootElement.TryGetProperty("testProperty", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("TestProperty", out _).Should().BeFalse(); // Should not exist in PascalCase
    }

    [Fact]
    public void Serialize_Should_Ignore_Null_Values()
    {
        // Arrange
        var testEvent = new TestIntegrationEventWithNullableProperty(
            EventId: Guid.NewGuid(),
            OccurredAtUtc: DateTime.UtcNow,
            RequiredProperty: "Required",
            OptionalProperty: null);

        // Act
        var serialized = _serializer.Serialize(testEvent, typeof(TestIntegrationEventWithNullableProperty));
        
        // Assert
        var json = JsonDocument.Parse(serialized);
        json.RootElement.TryGetProperty("optionalProperty", out _).Should().BeFalse();
        json.RootElement.TryGetProperty("requiredProperty", out _).Should().BeTrue();
    }

    [Fact]
    public void Generic_Serialize_And_Deserialize_Should_Work()
    {
        // Arrange
        var originalEvent = new TestIntegrationEvent(
            EventId: Guid.NewGuid(),
            OccurredAtUtc: DateTime.UtcNow,
            TestProperty: "Test Value",
            Number: 42);

        // Act
        var serialized = _serializer.Serialize(originalEvent);
        var deserialized = _serializer.Deserialize<TestIntegrationEvent>(serialized);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.EventId.Should().Be(originalEvent.EventId);
        deserialized.TestProperty.Should().Be(originalEvent.TestProperty);
    }

    private sealed record TestIntegrationEvent(
        Guid EventId,
        DateTime OccurredAtUtc,
        string TestProperty,
        int Number) : IIntegrationEvent;

    private sealed record TestIntegrationEventWithNullableProperty(
        Guid EventId,
        DateTime OccurredAtUtc,
        string RequiredProperty,
        string? OptionalProperty) : IIntegrationEvent;
}