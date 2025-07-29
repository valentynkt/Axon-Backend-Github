using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Axon.Api.Contracts.Chat;
using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Application.Commands.ProcessMessage;
using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Domain.Types;
using Axon.Shared.Common;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

// Aliases to resolve type ambiguity
using ApiProcessMessageResponse = Axon.Api.Contracts.Chat.ProcessMessageResponse;
using ApplicationProcessMessageResponse = Axon.Modules.Chat.Application.Commands.ProcessMessage.ProcessMessageResponse;

namespace Axon.Api.Tests.Endpoints.Chat;

public sealed class ProcessMessageEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ProcessMessageEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task ProcessMessage_ShouldReturnOkResult_GivenValidRequestWithoutMcp()
    {
        // Arrange
        var request = new ProcessMessageRequest(
            Message: "Hello, AI!",
            McpServer: null,
            ConversationId: null);

        var mockMediator = new Mock<IMediator>();
        var handlerResponse = new ApplicationProcessMessageResponse(
            Response: "Hello! How can I help you?",
            ConversationId: "conv-123",
            ToolExecutions: null);

        mockMediator
            .Setup(x => x.Send(It.IsAny<ProcessMessageCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ApplicationProcessMessageResponse>.Success(handlerResponse));

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace IMediator with mock
                services.Remove(services.Single(d => d.ServiceType == typeof(IMediator)));
                services.AddSingleton(mockMediator.Object);
            });
        }).CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/process", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadFromJsonAsync<ApiProcessMessageResponse>();
        content.Should().NotBeNull();
        content!.Response.Should().Be("Hello! How can I help you?");
        content.ConversationId.Should().Be("conv-123");
        content.ToolExecutions.Should().BeNull();

        // Verify mediator was called correctly
        mockMediator.Verify(x => x.Send(
            It.Is<ProcessMessageCommand>(cmd =>
                cmd.Message == "Hello, AI!" &&
                cmd.McpServerUrl == null &&
                cmd.McpHeaders == null &&
                cmd.AllowedTools == null &&
                cmd.PreviousResponseId == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessMessage_ShouldReturnOkResult_GivenValidRequestWithMcpConfiguration()
    {
        // Arrange
        var mcpServer = new McpServerRequest(
            ServerUrl: "https://api.example.com/mcp",
            ServerLabel: null,
            Headers: new Dictionary<string, string> { { "Authorization", "Bearer token" } },
            AllowedTools: new[] { "weather", "calendar" });

        var request = new ProcessMessageRequest(
            Message: "Check the weather",
            McpServer: mcpServer,
            ConversationId: "prev-conv-123");

        var mockMediator = new Mock<IMediator>();
        var toolExecutions = new[]
        {
            new ToolExecutionSummary("weather", true, TimeSpan.FromMilliseconds(500))
        };

        var handlerResponse = new ApplicationProcessMessageResponse(
            Response: "The weather is sunny and 72°F.",
            ConversationId: "conv-456",
            ToolExecutions: toolExecutions);

        mockMediator
            .Setup(x => x.Send(It.IsAny<ProcessMessageCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ApplicationProcessMessageResponse>.Success(handlerResponse));

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.Remove(services.Single(d => d.ServiceType == typeof(IMediator)));
                services.AddSingleton(mockMediator.Object);
            });
        }).CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/process", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadFromJsonAsync<ApiProcessMessageResponse>();
        content.Should().NotBeNull();
        content!.Response.Should().Be("The weather is sunny and 72°F.");
        content.ConversationId.Should().Be("conv-456");
        content.ToolExecutions.Should().HaveCount(1);
        content.ToolExecutions![0].ToolName.Should().Be("weather");
        content.ToolExecutions[0].Success.Should().BeTrue();
        content.ToolExecutions[0].DurationMs.Should().Be(500);

        // Verify mediator was called with MCP configuration
        mockMediator.Verify(x => x.Send(
            It.Is<ProcessMessageCommand>(cmd =>
                cmd.Message == "Check the weather" &&
                cmd.McpServerUrl == "https://api.example.com/mcp" &&
                cmd.McpHeaders!["Authorization"] == "Bearer token" &&
                cmd.AllowedTools!.Contains("weather") &&
                cmd.AllowedTools!.Contains("calendar") &&
                cmd.PreviousResponseId == "prev-conv-123"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessMessage_ShouldReturnBadRequest_GivenValidationError()
    {
        // Arrange
        var request = new ProcessMessageRequest(
            Message: "Test message",
            McpServer: null,
            ConversationId: null);

        var mockMediator = new Mock<IMediator>();
        var validationError = Error.Validation("Message cannot be empty");

        mockMediator
            .Setup(x => x.Send(It.IsAny<ProcessMessageCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ApplicationProcessMessageResponse>.Failure(validationError));

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.Remove(services.Single(d => d.ServiceType == typeof(IMediator)));
                services.AddSingleton(mockMediator.Object);
            });
        }).CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/process", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Message cannot be empty");
        content.Should().Contain("Validation Error");
    }

    [Fact]
    public async Task ProcessMessage_ShouldReturnNotFound_GivenNotFoundError()
    {
        // Arrange
        var request = new ProcessMessageRequest(
            Message: "Find conversation",
            McpServer: null,
            ConversationId: "non-existent-conv");

        var mockMediator = new Mock<IMediator>();
        var notFoundError = Error.NotFound("Conversation not found");

        mockMediator
            .Setup(x => x.Send(It.IsAny<ProcessMessageCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ApplicationProcessMessageResponse>.Failure(notFoundError));

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.Remove(services.Single(d => d.ServiceType == typeof(IMediator)));
                services.AddSingleton(mockMediator.Object);
            });
        }).CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/process", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Conversation not found");
        content.Should().Contain("Not Found");
    }

    [Fact]
    public async Task ProcessMessage_ShouldReturnBadGateway_GivenExternalServiceError()
    {
        // Arrange
        var request = new ProcessMessageRequest(
            Message: "Test message",
            McpServer: null,
            ConversationId: null);

        var mockMediator = new Mock<IMediator>();
        var externalServiceError = Error.ExternalService("AI service is unavailable");

        mockMediator
            .Setup(x => x.Send(It.IsAny<ProcessMessageCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ApplicationProcessMessageResponse>.Failure(externalServiceError));

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.Remove(services.Single(d => d.ServiceType == typeof(IMediator)));
                services.AddSingleton(mockMediator.Object);
            });
        }).CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/process", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("AI service is unavailable");
        content.Should().Contain("External Service Error");
    }

    [Fact]
    public async Task ProcessMessage_ShouldReturnInternalServerError_GivenUnknownError()
    {
        // Arrange
        var request = new ProcessMessageRequest(
            Message: "Test message",
            McpServer: null,
            ConversationId: null);

        var mockMediator = new Mock<IMediator>();
        var unknownError = Error.InternalError("Something went wrong");

        mockMediator
            .Setup(x => x.Send(It.IsAny<ProcessMessageCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ApplicationProcessMessageResponse>.Failure(unknownError));

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.Remove(services.Single(d => d.ServiceType == typeof(IMediator)));
                services.AddSingleton(mockMediator.Object);
            });
        }).CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/process", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("An unexpected error occurred");
        content.Should().Contain("Internal Server Error");
    }

    [Fact]
    public async Task ProcessMessage_ShouldGenerateConversationId_GivenNullConversationIdInResponse()
    {
        // Arrange
        var request = new ProcessMessageRequest(
            Message: "Hello",
            McpServer: null,
            ConversationId: null);

        var mockMediator = new Mock<IMediator>();
        var handlerResponse = new ApplicationProcessMessageResponse(
            Response: "Hi there!",
            ConversationId: null,  // Handler returns null
            ToolExecutions: null);

        mockMediator
            .Setup(x => x.Send(It.IsAny<ProcessMessageCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ApplicationProcessMessageResponse>.Success(handlerResponse));

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.Remove(services.Single(d => d.ServiceType == typeof(IMediator)));
                services.AddSingleton(mockMediator.Object);
            });
        }).CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/process", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadFromJsonAsync<ApiProcessMessageResponse>();
        content.Should().NotBeNull();
        content!.ConversationId.Should().NotBeNullOrEmpty();
        
        // Verify it's a valid GUID format
        Guid.TryParse(content.ConversationId, out _).Should().BeTrue();
    }

    [Fact]
    public async Task ProcessMessage_ShouldReturnUnsupportedMediaType_GivenNullRequestBody()
    {
        // Act
        var response = await _client.PostAsync("/api/chat/process", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task ProcessMessage_ShouldHandleEmptyToolExecutions_GivenEmptyArray()
    {
        // Arrange
        var request = new ProcessMessageRequest(
            Message: "Test message",
            McpServer: null,
            ConversationId: null);

        var mockMediator = new Mock<IMediator>();
        var handlerResponse = new ApplicationProcessMessageResponse(
            Response: "Response with no tools",
            ConversationId: "conv-123",
            ToolExecutions: Array.Empty<ToolExecutionSummary>());

        mockMediator
            .Setup(x => x.Send(It.IsAny<ProcessMessageCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ApplicationProcessMessageResponse>.Success(handlerResponse));

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.Remove(services.Single(d => d.ServiceType == typeof(IMediator)));
                services.AddSingleton(mockMediator.Object);
            });
        }).CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/process", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadFromJsonAsync<ApiProcessMessageResponse>();
        content.Should().NotBeNull();
        content!.ToolExecutions.Should().NotBeNull();
        content.ToolExecutions.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessMessage_ShouldHandleCancellation_GivenCancelledRequest()
    {
        // Arrange
        var request = new ProcessMessageRequest(
            Message: "Test message that should be cancelled",
            McpServer: null,
            ConversationId: null);

        var mockMediator = new Mock<IMediator>();
        mockMediator
            .Setup(x => x.Send(It.IsAny<ProcessMessageCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.Remove(services.Single(d => d.ServiceType == typeof(IMediator)));
                services.AddSingleton(mockMediator.Object);
            });
        }).CreateClient();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await FluentActions
            .Invoking(() => client.PostAsJsonAsync("/api/chat/process", request, cts.Token))
            .Should()
            .ThrowAsync<OperationCanceledException>();
    }
}