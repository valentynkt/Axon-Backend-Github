using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Infrastructure.Ai;
using Axon.Modules.Chat.Infrastructure.Ai.Abstractions;
using Axon.Modules.Chat.Infrastructure.Ai.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Chat.Infrastructure.Tests.Ai.Services;

/// <summary>
/// Unit tests for HttpRequestBuilder following London School approach
/// Tests the SRP-compliant service extracted from OpenAiClient refactoring
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("Infrastructure")]
[Category("HttpRequestBuilder")]
public sealed class HttpRequestBuilderTests
{
    private HttpRequestBuilder _sut = null!;
    private Mock<IPayloadSerializer> _mockPayloadSerializer = null!;
    private Mock<ILogger<HttpRequestBuilder>> _mockLogger = null!;
    private OpenAiOptions _options = null!;

    [SetUp]
    public void SetUp()
    {
        _mockPayloadSerializer = new Mock<IPayloadSerializer>();
        _mockLogger = new Mock<ILogger<HttpRequestBuilder>>();
        
        _options = new OpenAiOptions
        {
            ApiKey = "test-api-key",
            Model = "gpt-4o",
            TimeoutSeconds = 30,
            MaxTokens = 1000,
            Temperature = 0.7,
            McpEnabled = true
        };

        var optionsWrapper = new OptionsWrapper<OpenAiOptions>(_options);
        _sut = new HttpRequestBuilder(optionsWrapper, _mockPayloadSerializer.Object, _mockLogger.Object);
    }

    [Test]
    public void Constructor_WithValidDependencies_ShouldCreateInstance()
    {
        // Act & Assert
        _sut.ShouldNotBeNull();
    }

    [Test]
    public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => 
            new HttpRequestBuilder(null!, _mockPayloadSerializer.Object, _mockLogger.Object));
    }

    [Test]
    public void Constructor_WithNullPayloadSerializer_ShouldThrowArgumentNullException()
    {
        // Arrange
        var optionsWrapper = new OptionsWrapper<OpenAiOptions>(_options);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => 
            new HttpRequestBuilder(optionsWrapper, null!, _mockLogger.Object));
    }

    [Test]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange
        var optionsWrapper = new OptionsWrapper<OpenAiOptions>(_options);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => 
            new HttpRequestBuilder(optionsWrapper, _mockPayloadSerializer.Object, null!));
    }

    [Test]
    public void ConfigureHttpClient_WithValidHttpClient_ShouldSetAuthorizationHeader()
    {
        // Arrange
        using var httpClient = new HttpClient();

        // Act
        _sut.ConfigureHttpClient(httpClient);

        // Assert
        httpClient.DefaultRequestHeaders.Authorization.ShouldNotBeNull();
        httpClient.DefaultRequestHeaders.Authorization.Scheme.ShouldBe("Bearer");
        httpClient.DefaultRequestHeaders.Authorization.Parameter.ShouldBe("test-api-key");
    }

    [Test]
    public void ConfigureHttpClient_WithValidHttpClient_ShouldSetAcceptHeader()
    {
        // Arrange
        using var httpClient = new HttpClient();

        // Act
        _sut.ConfigureHttpClient(httpClient);

        // Assert
        httpClient.DefaultRequestHeaders.Accept.ShouldContain(
            header => header.MediaType == "application/json");
    }

    [Test]
    public void ConfigureHttpClient_WithValidHttpClient_ShouldSetTimeout()
    {
        // Arrange
        using var httpClient = new HttpClient();

        // Act
        _sut.ConfigureHttpClient(httpClient);

        // Assert
        httpClient.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Test]
    public void GetApiUrl_ShouldReturnCorrectUrl()
    {
        // Act
        var apiUrl = _sut.GetApiUrl();

        // Assert
        apiUrl.ShouldBe("https://api.openai.com/v1/responses");
    }

    [Test]
    public void BuildRequestContent_WithValidRequest_ShouldCallPayloadSerializer()
    {
        // Arrange
        var request = new AiRequest("test message");
        var expectedJson = "{\"model\":\"gpt-4o\",\"input\":\"test message\"}";
        
        _mockPayloadSerializer
            .Setup(x => x.Serialize(It.IsAny<Dictionary<string, object>>()))
            .Returns(expectedJson);

        // Act
        using var content = _sut.BuildRequestContent(request, null);

        // Assert
        _mockPayloadSerializer.Verify(
            x => x.Serialize(It.IsAny<Dictionary<string, object>>()), 
            Times.Once);
    }

    [Test]
    public void BuildRequestContent_WithValidRequest_ShouldReturnStringContent()
    {
        // Arrange
        var request = new AiRequest("test message");
        var expectedJson = "{\"model\":\"gpt-4o\",\"input\":\"test message\"}";
        
        _mockPayloadSerializer
            .Setup(x => x.Serialize(It.IsAny<Dictionary<string, object>>()))
            .Returns(expectedJson);

        // Act
        using var content = _sut.BuildRequestContent(request, null);

        // Assert
        content.ShouldNotBeNull();
        content.Headers.ContentType?.MediaType.ShouldBe("application/json");
        content.Headers.ContentType?.CharSet.ShouldBe("utf-8");
    }

    [Test]
    public void BuildRequestContent_WithMcpConfigs_ShouldIncludeToolsArray()
    {
        // Arrange
        var mcpConfigs = new List<McpServerConfig>
        {
            new("https://test.example.com", "test-server", null, new[] { "tool1", "tool2" }, false, 30)
        };
        
        var request = new AiRequest("test message", mcpConfigs);
        var expectedJson = "{\"model\":\"gpt-4o\",\"input\":\"test message\",\"tools\":[]}";
        
        _mockPayloadSerializer
            .Setup(x => x.Serialize(It.IsAny<Dictionary<string, object>>()))
            .Returns(expectedJson);

        // Act
        using var content = _sut.BuildRequestContent(request, null);

        // Assert
        _mockPayloadSerializer.Verify(x => x.Serialize(
            It.Is<Dictionary<string, object>>(dict => dict.ContainsKey("tools"))), 
            Times.Once);
    }

    [Test]
    public void BuildRequestContent_WithActivity_ShouldSetActivityTags()
    {
        // Arrange
        using var activity = new Activity("test-activity");
        activity.Start();
        var mcpConfigs = new List<McpServerConfig>
        {
            new("https://test.example.com", "test-server", null, new[] { "tool1", "tool2" })
        };
        
        var request = new AiRequest("test message", mcpConfigs);
        var expectedJson = "{}";
        
        _mockPayloadSerializer
            .Setup(x => x.Serialize(It.IsAny<Dictionary<string, object>>()))
            .Returns(expectedJson);

        // Act
        using var content = _sut.BuildRequestContent(request, activity);
        activity.Stop();

        // Assert
        activity.Tags.ShouldContain(tag => tag.Key == "mcp.enabled" && tag.Value == "True");
        activity.Tags.ShouldContain(tag => tag.Key == "mcp.servers_configured" && tag.Value == "1");
    }

    [Test]
    public void BuildRequestContent_WithReasoningModel_ShouldExcludeTemperature()
    {
        // Arrange
        _options.Model = "o1-preview";
        _options.Temperature = 0.7;
        var optionsWrapper = new OptionsWrapper<OpenAiOptions>(_options);
        _sut = new HttpRequestBuilder(optionsWrapper, _mockPayloadSerializer.Object, _mockLogger.Object);
        
        var request = new AiRequest("test message");
        var capturedPayload = new Dictionary<string, object>();
        
        _mockPayloadSerializer
            .Setup(x => x.Serialize(It.IsAny<Dictionary<string, object>>()))
            .Callback<Dictionary<string, object>>(dict => 
            {
                foreach (var kvp in dict)
                {
                    capturedPayload[kvp.Key] = kvp.Value;
                }
            })
            .Returns("{}");

        // Act
        using var content = _sut.BuildRequestContent(request, null);

        // Assert
        capturedPayload.ShouldNotContainKey("temperature");
        capturedPayload.ShouldContainKey("model");
        capturedPayload["model"].ShouldBe("o1-preview");
    }

    [Test]
    public void BuildRequestContent_WithNormalModel_ShouldIncludeTemperature()
    {
        // Arrange
        var request = new AiRequest("test message");
        var capturedPayload = new Dictionary<string, object>();
        
        _mockPayloadSerializer
            .Setup(x => x.Serialize(It.IsAny<Dictionary<string, object>>()))
            .Callback<Dictionary<string, object>>(dict => 
            {
                foreach (var kvp in dict)
                {
                    capturedPayload[kvp.Key] = kvp.Value;
                }
            })
            .Returns("{}");

        // Act
        using var content = _sut.BuildRequestContent(request, null);

        // Assert
        capturedPayload.ShouldContainKey("temperature");
        capturedPayload["temperature"].ShouldBe(0.7);
    }

    [Test]
    public void BuildRequestContent_WithMaxTokens_ShouldIncludeMaxOutputTokens()
    {
        // Arrange
        var request = new AiRequest("test message");
        var capturedPayload = new Dictionary<string, object>();
        
        _mockPayloadSerializer
            .Setup(x => x.Serialize(It.IsAny<Dictionary<string, object>>()))
            .Callback<Dictionary<string, object>>(dict => 
            {
                foreach (var kvp in dict)
                {
                    capturedPayload[kvp.Key] = kvp.Value;
                }
            })
            .Returns("{}");

        // Act
        using var content = _sut.BuildRequestContent(request, null);

        // Assert
        capturedPayload.ShouldContainKey("max_output_tokens");
        capturedPayload["max_output_tokens"].ShouldBe(1000);
    }

    [Test]
    public void BuildRequestContent_WithZeroMaxTokens_ShouldExcludeMaxOutputTokens()
    {
        // Arrange
        _options.MaxTokens = 0;
        var optionsWrapper = new OptionsWrapper<OpenAiOptions>(_options);
        _sut = new HttpRequestBuilder(optionsWrapper, _mockPayloadSerializer.Object, _mockLogger.Object);
        
        var request = new AiRequest("test message");
        var capturedPayload = new Dictionary<string, object>();
        
        _mockPayloadSerializer
            .Setup(x => x.Serialize(It.IsAny<Dictionary<string, object>>()))
            .Callback<Dictionary<string, object>>(dict => 
            {
                foreach (var kvp in dict)
                {
                    capturedPayload[kvp.Key] = kvp.Value;
                }
            })
            .Returns("{}");

        // Act
        using var content = _sut.BuildRequestContent(request, null);

        // Assert
        capturedPayload.ShouldNotContainKey("max_output_tokens");
    }
}