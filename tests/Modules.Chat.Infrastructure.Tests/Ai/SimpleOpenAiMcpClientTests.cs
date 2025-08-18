using System.Net;
using System.Text;
using System.Text.Json;
using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Infrastructure.Ai;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Axon.Modules.Chat.Infrastructure.Tests.Ai;

/// <summary>
/// Unit tests for SimpleOpenAiMcpClient POC implementation
/// </summary>
public sealed class SimpleOpenAiMcpClientTests
{
    private readonly ILogger<SimpleOpenAiMcpClient> _logger;
    private readonly IOptions<OpenAiOptions> _options;
    private readonly OpenAiOptions _optionsValue;
    
    public SimpleOpenAiMcpClientTests()
    {
        _logger = Substitute.For<ILogger<SimpleOpenAiMcpClient>>();
        _optionsValue = new OpenAiOptions
        {
            ApiKey = "test-api-key",
            Model = "gpt-4o",
            MaxTokens = 1000,
            Temperature = 0.7,
            McpEnabled = true,
            TimeoutSeconds = 30
        };
        _options = Options.Create(_optionsValue);
    }
    
    [Fact]
    public async Task ProcessMessageAsync_WithValidRequest_ReturnsSuccess()
    {
        // Arrange
        var mockResponse = CreateMockOpenAiResponse("The answer is 4.");
        var httpClient = CreateMockHttpClient(mockResponse);
        var client = new SimpleOpenAiMcpClient(httpClient, _logger, _options);
        
        var request = new AiRequest(
            Message: "What is 2+2?",
            McpConfigs: null,
            PreviousResponseId: null);
        
        // Act
        var result = await client.ProcessMessageAsync(request, CancellationToken.None);
        
        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Content.Should().Be("The answer is 4.");
        result.Value.ResponseId.Should().NotBeNullOrEmpty();
    }
    
    [Fact]
    public async Task ProcessMessageAsync_WithMcpServers_IncludesToolsInRequest()
    {
        // Arrange
        string? capturedRequestBody = null;
        var mockResponse = CreateMockOpenAiResponse("Test response");
        var httpClient = CreateMockHttpClient(mockResponse, body => capturedRequestBody = body);
        var client = new SimpleOpenAiMcpClient(httpClient, _logger, _options);
        
        var mcpConfigs = new[]
        {
            new McpServerConfig(
                ServerUrl: "https://test.mcp.server/api",
                ServerLabel: "Test Server",
                Headers: new Dictionary<string, string> { ["Authorization"] = "Bearer test" },
                AllowedTools: null,
                RequireApproval: false,
                TimeoutSeconds: 30)
        };
        
        var request = new AiRequest(
            Message: "Test message",
            McpConfigs: mcpConfigs,
            PreviousResponseId: null);
        
        // Act
        await client.ProcessMessageAsync(request, CancellationToken.None);
        
        // Assert
        capturedRequestBody.Should().NotBeNull();
        capturedRequestBody.Should().Contain("\"type\":\"mcp\"");
        capturedRequestBody.Should().Contain("\"server_url\":\"https://test.mcp.server/api\"");
        capturedRequestBody.Should().Contain("\"server_label\":\"Test Server\"");
    }
    
    [Fact]
    public async Task ProcessMessageAsync_WithApiError_ReturnsFailure()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(null, statusCode: HttpStatusCode.BadRequest);
        var client = new SimpleOpenAiMcpClient(httpClient, _logger, _options);
        
        var request = new AiRequest(
            Message: "Test message",
            McpConfigs: null,
            PreviousResponseId: null);
        
        // Act
        var result = await client.ProcessMessageAsync(request, CancellationToken.None);
        
        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("ExternalService");
    }
    
    [Fact]
    public async Task ProcessMessageAsync_WithPreviousResponseId_IncludesInRequest()
    {
        // Arrange
        string? capturedRequestBody = null;
        var mockResponse = CreateMockOpenAiResponse("Continued response");
        var httpClient = CreateMockHttpClient(mockResponse, body => capturedRequestBody = body);
        var client = new SimpleOpenAiMcpClient(httpClient, _logger, _options);
        
        var request = new AiRequest(
            Message: "Continue",
            McpConfigs: null,
            PreviousResponseId: "prev-123");
        
        // Act
        await client.ProcessMessageAsync(request, CancellationToken.None);
        
        // Assert
        capturedRequestBody.Should().NotBeNull();
        capturedRequestBody.Should().Contain("\"previous_response_id\":\"prev-123\"");
    }
    
    private static HttpClient CreateMockHttpClient(
        string? responseContent, 
        HttpStatusCode statusCode = HttpStatusCode.OK,
        Action<string>? captureRequest = null)
    {
        var messageHandler = new MockHttpMessageHandler(responseContent, statusCode, captureRequest);
        return new HttpClient(messageHandler)
        {
            BaseAddress = new Uri("https://api.openai.com/")
        };
    }
    
    private static string CreateMockOpenAiResponse(string textContent)
    {
        var response = new
        {
            id = "resp-123",
            output = new[]
            {
                new
                {
                    type = "message",
                    content = new[]
                    {
                        new { text = textContent }
                    }
                }
            }
        };
        
        return JsonSerializer.Serialize(response);
    }
    
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly string? _responseContent;
        private readonly HttpStatusCode _statusCode;
        private readonly Action<string>? _captureRequest;
        
        public MockHttpMessageHandler(
            string? responseContent, 
            HttpStatusCode statusCode,
            Action<string>? captureRequest = null)
        {
            _responseContent = responseContent;
            _statusCode = statusCode;
            _captureRequest = captureRequest;
        }
        
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, 
            CancellationToken cancellationToken)
        {
            if (_captureRequest != null && request.Content != null)
            {
                var requestBody = await request.Content.ReadAsStringAsync(cancellationToken);
                _captureRequest(requestBody);
            }
            
            var response = new HttpResponseMessage(_statusCode);
            
            if (_responseContent != null)
            {
                response.Content = new StringContent(_responseContent, Encoding.UTF8, "application/json");
            }
            
            return response;
        }
    }
}