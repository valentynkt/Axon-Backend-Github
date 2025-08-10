using System.Text.Json;
using BuildingBlocks.Application.Configuration.Json;
using BuildingBlocks.Core.Domain.Primitives;

namespace BuildingBlocks.Tests.Core.Serialization;

[TestFixture]
[Category("Unit")]
[Category("Core")]
[Category("Serialization")]
public sealed class StrongIdJsonConverterTests
{
    private readonly JsonSerializerOptions _options = JsonDefaults.Options;

    [Test]
    public void Should_SerializeAndDeserialize_GuidStrongId_Successfully()
    {
        // Arrange
        var original = TestGuidStrongId.New();

        // Act
        var json = JsonSerializer.Serialize(original, _options);
        var deserialized = JsonSerializer.Deserialize<TestGuidStrongId>(json, _options);

        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.Value.ShouldBe(original.Value);
        deserialized.ShouldBe(original);
    }

    [Test]
    public void Should_SerializeAndDeserialize_IntStrongId_Successfully()
    {
        // Arrange
        var original = TestIntStrongId.From(42);

        // Act
        var json = JsonSerializer.Serialize(original, _options);
        var deserialized = JsonSerializer.Deserialize<TestIntStrongId>(json, _options);

        // Assert
        deserialized.ShouldNotBeNull();
        deserialized!.Value.ShouldBe(original.Value);
        deserialized.ShouldBe(original);
    }

    [Test]
    public void Should_SerializeAndDeserialize_LongStrongId_Successfully()
    {
        // Arrange
        var original = TestLongStrongId.From(123456789L);

        // Act
        var json = JsonSerializer.Serialize(original, _options);
        var deserialized = JsonSerializer.Deserialize<TestLongStrongId>(json, _options);

        // Assert
        deserialized.ShouldNotBeNull();
        deserialized!.Value.ShouldBe(original.Value);
        deserialized.ShouldBe(original);
    }

    [Test]
    public void Should_SerializeNull_AsNull()
    {
        // Arrange
        TestGuidStrongId? strongId = null;

        // Act
        var json = JsonSerializer.Serialize(strongId, _options);

        // Assert
        json.ShouldBe("null");
    }

    [Test]
    public void Should_DeserializeNull_AsNull()
    {
        // Arrange
        var json = "null";

        // Act
        var deserialized = JsonSerializer.Deserialize<TestGuidStrongId?>(json, _options);

        // Assert
        deserialized.ShouldBeNull();
    }

    [Test]
    public void Should_SerializeAsUnderlyingPrimitive()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var strongId = TestGuidStrongId.From(guid);

        // Act
        var json = JsonSerializer.Serialize(strongId, _options);
        var primitiveJson = JsonSerializer.Serialize(guid, _options);

        // Assert
        json.ShouldBe(primitiveJson);
    }

    [Test]
    public void Should_ThrowJsonException_WhenDeserializingDefaultGuid()
    {
        // Arrange
        var json = JsonSerializer.Serialize(Guid.Empty, _options);

        // Act & Assert
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<TestGuidStrongId>(json, _options))
            .Message.ShouldBe("Cannot deserialize default(Guid) to TestGuidStrongId.");
    }

    [Test]
    public void Should_ThrowJsonException_WhenDeserializingDefaultInt()
    {
        // Arrange
        var json = JsonSerializer.Serialize(0, _options);

        // Act & Assert
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<TestIntStrongId>(json, _options))
            .Message.ShouldBe("Cannot deserialize default(Int32) to TestIntStrongId.");
    }

    [Test]
    public void Should_HandleComplexObjectWithStrongIds()
    {
        // Arrange
        var original = new TestDto
        {
            Id = TestGuidStrongId.New(),
            Count = TestIntStrongId.From(100),
            Name = "Test"
        };

        // Act
        var json = JsonSerializer.Serialize(original, _options);
        var deserialized = JsonSerializer.Deserialize<TestDto>(json, _options);

        // Assert
        deserialized.ShouldNotBeNull();
        deserialized!.Id.ShouldBe(original.Id);
        deserialized.Count.ShouldBe(original.Count);
        deserialized.Name.ShouldBe(original.Name);
    }
}

// Test StrongId implementations
public sealed record TestGuidStrongId : StrongId<Guid>
{
    private TestGuidStrongId(Guid value) : base(value) { }

    public static TestGuidStrongId New() => new(Guid.NewGuid());
    public static TestGuidStrongId From(Guid value) => new(value);
}

public sealed record TestIntStrongId : StrongId<int>
{
    private TestIntStrongId(int value) : base(value) { }

    public static TestIntStrongId From(int value) => new(value);
}

public sealed record TestLongStrongId : StrongId<long>
{
    private TestLongStrongId(long value) : base(value) { }

    public static TestLongStrongId From(long value) => new(value);
}

public sealed record TestDto
{
    public TestGuidStrongId Id { get; init; } = null!;
    public TestIntStrongId Count { get; init; } = null!;
    public string Name { get; init; } = null!;
}