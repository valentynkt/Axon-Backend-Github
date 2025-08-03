using System.Text.Json;
using Axon.Modules.Chat.Infrastructure.Ai.Services;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Chat.Infrastructure.Tests.Ai.Services;

/// <summary>
/// Unit tests for PayloadSerializer following London School approach
/// Tests the SRP-compliant service extracted from OpenAiClient refactoring
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("Infrastructure")]
[Category("PayloadSerializer")]
public sealed class PayloadSerializerTests
{
    private PayloadSerializer _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _sut = new PayloadSerializer();
    }

    [Test]
    public void Constructor_ShouldCreateInstance()
    {
        // Act & Assert
        _sut.ShouldNotBeNull();
    }

    [Test]
    public void GetOptions_ShouldReturnJsonSerializerOptions()
    {
        // Act
        var options = _sut.GetOptions();

        // Assert
        options.ShouldNotBeNull();
        options.PropertyNamingPolicy.ShouldNotBeNull();
    }

    [Test]
    public void GetOptions_ShouldUseSnakeCaseNaming()
    {
        // Act
        var options = _sut.GetOptions();

        // Assert
        options.PropertyNamingPolicy.ShouldNotBeNull();
        // The property naming policy should convert camelCase to snake_case
        var testName = options.PropertyNamingPolicy.ConvertName("TestProperty");
        testName.ShouldBe("test_property");
    }

    [Test]
    public void Serialize_WithSimpleObject_ShouldReturnJsonString()
    {
        // Arrange
        var testObject = new { TestProperty = "test-value", AnotherProperty = 42 };

        // Act
        var result = _sut.Serialize(testObject);

        // Assert
        result.ShouldNotBeNullOrWhiteSpace();
        result.ShouldContain("test_property");
        result.ShouldContain("another_property");
        result.ShouldContain("test-value");
        result.ShouldContain("42");
    }

    [Test]
    public void Serialize_WithComplexObject_ShouldHandleNestedProperties()
    {
        // Arrange
        var testObject = new
        {
            TopLevel = "value",
            NestedObject = new
            {
                PropertyOne = "nested-value",
                PropertyTwo = 123
            },
            ArrayProperty = new[] { "item1", "item2" }
        };

        // Act
        var result = _sut.Serialize(testObject);

        // Assert
        result.ShouldNotBeNullOrWhiteSpace();
        result.ShouldContain("top_level");
        result.ShouldContain("nested_object");
        result.ShouldContain("property_one");
        result.ShouldContain("property_two");
        result.ShouldContain("array_property");
        result.ShouldContain("nested-value");
    }

    [Test]
    public void Serialize_WithNullObject_ShouldReturnNull()
    {
        // Act
        var result = _sut.Serialize(null!);

        // Assert
        result.ShouldBe("null");
    }

    [Test]
    public void Serialize_WithDictionary_ShouldConvertKeysToSnakeCase()
    {
        // Arrange
        var testDictionary = new Dictionary<string, object>
        {
            ["ModelName"] = "gpt-4o",
            ["MaxTokens"] = 1000,
            ["TemperatureValue"] = 0.7
        };

        // Act
        var result = _sut.Serialize(testDictionary);

        // Assert
        result.ShouldNotBeNullOrWhiteSpace();
        result.ShouldContain("model_name");
        result.ShouldContain("max_tokens");
        result.ShouldContain("temperature_value");
    }

    [Test]
    public void Deserialize_WithValidJson_ShouldReturnObject()
    {
        // Arrange
        var json = """{"test_property":"test-value","another_property":42}""";

        // Act
        var result = _sut.Deserialize<TestClass>(json);

        // Assert
        result.ShouldNotBeNull();
        result.TestProperty.ShouldBe("test-value");
        result.AnotherProperty.ShouldBe(42);
    }

    [Test]
    public void Deserialize_WithInvalidJson_ShouldThrowJsonException()
    {
        // Arrange
        var invalidJson = "{invalid json}";

        // Act & Assert
        Should.Throw<JsonException>(() => _sut.Deserialize<TestClass>(invalidJson));
    }

    [Test]
    public void Deserialize_WithNullJson_ShouldThrowArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => _sut.Deserialize<TestClass>(null!));
    }

    [Test]
    public void Deserialize_WithEmptyJson_ShouldThrowJsonException()
    {
        // Act & Assert
        Should.Throw<JsonException>(() => _sut.Deserialize<TestClass>(string.Empty));
    }

    [Test]
    public void Deserialize_WithComplexJson_ShouldHandleNestedObjects()
    {
        // Arrange
        var json = """
        {
            "simple_property": "value",
            "nested_object": {
                "nested_property": "nested-value"
            },
            "array_property": ["item1", "item2"]
        }
        """;

        // Act
        var result = _sut.Deserialize<ComplexTestClass>(json);

        // Assert
        result.ShouldNotBeNull();
        result.SimpleProperty.ShouldBe("value");
        result.NestedObject.ShouldNotBeNull();
        result.NestedObject.NestedProperty.ShouldBe("nested-value");
        result.ArrayProperty.ShouldNotBeNull();
        result.ArrayProperty.Length.ShouldBe(2);
        result.ArrayProperty[0].ShouldBe("item1");
        result.ArrayProperty[1].ShouldBe("item2");
    }

    [Test]
    public void RoundTripSerialization_ShouldPreserveData()
    {
        // Arrange
        var originalObject = new TestClass
        {
            TestProperty = "original-value",
            AnotherProperty = 999
        };

        // Act
        var json = _sut.Serialize(originalObject);
        var deserializedObject = _sut.Deserialize<TestClass>(json);

        // Assert
        deserializedObject.ShouldNotBeNull();
        deserializedObject.TestProperty.ShouldBe(originalObject.TestProperty);
        deserializedObject.AnotherProperty.ShouldBe(originalObject.AnotherProperty);
    }

    [Test]
    public void Serialize_WithDateTimeObject_ShouldUseIsoFormat()
    {
        // Arrange
        var testObject = new
        {
            CreatedAt = new DateTime(2023, 12, 25, 10, 30, 45, DateTimeKind.Utc),
            Name = "test"
        };

        // Act
        var result = _sut.Serialize(testObject);

        // Assert
        result.ShouldNotBeNullOrWhiteSpace();
        result.ShouldContain("created_at");
        result.ShouldContain("2023-12-25T10:30:45");
    }

    // Test classes for deserialization tests
    public class TestClass
    {
        public string TestProperty { get; set; } = string.Empty;
        public int AnotherProperty { get; set; }
    }

    public class ComplexTestClass
    {
        public string SimpleProperty { get; set; } = string.Empty;
        public NestedTestClass NestedObject { get; set; } = new();
        public string[] ArrayProperty { get; set; } = Array.Empty<string>();
    }

    public class NestedTestClass
    {
        public string NestedProperty { get; set; } = string.Empty;
    }
}