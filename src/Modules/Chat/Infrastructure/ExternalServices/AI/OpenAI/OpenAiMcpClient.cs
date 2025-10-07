using System.Net;
using System.Text;
using System.Text.Json;
using Axon.BuildingBlocks.Core.Primitives.ValueObjects; // MessageContent (Vogen)
using Axon.Modules.Chat.Application.Contracts.AI;
using Axon.Modules.Chat.Application.DTOs.Configurations;
using Axon.Modules.Chat.Application.DTOs.Requests;
using Axon.Modules.Chat.Application.DTOs.Responses;
using Axon.Modules.Chat.Domain.Errors;
using Axon.Modules.Chat.Infrastructure.ExternalServices.AI.OpenAI.Models;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Axon.Modules.Chat.Infrastructure.ExternalServices.AI.OpenAI;

/// <summary>
/// Minimal, production-lean client for OpenAI Responses API with optional MCP tools.
/// - Start new conversation when PreviousResponseId is null
/// - Continue conversation when PreviousResponseId is set
/// </summary>
public sealed class OpenAiMcpClient : IAiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<OpenAiMcpClient> _logger;
    private readonly OpenAiOptions _options;
    private readonly IMcpToolBuilder<McpToolDefinition> _toolBuilder;

    // Shared JSON serializer options for consistent serialization/deserialization
    // Note: We use explicit [JsonPropertyName] attributes on DTOs, so no naming policy needed
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true, // Allow flexible casing on deserialization
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public OpenAiMcpClient(
        HttpClient httpClient,
        ILogger<OpenAiMcpClient> logger,
        IOptions<OpenAiOptions> options,
        IMcpToolBuilder<McpToolDefinition> toolBuilder)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _toolBuilder = toolBuilder ?? throw new ArgumentNullException(nameof(toolBuilder));
    }

    public async Task<Result<AiResponse, Error>> ProcessMessageAsync(
        AiRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Build strongly-typed request payload
            var requestPayload = BuildRequestPayload(request);

            _logger.LogDebug("Sending Responses API request (continuation={Continuation})",
                !string.IsNullOrWhiteSpace(request.PreviousResponseId));

            // Serialize to JSON using shared options
            var jsonContent = JsonSerializer.Serialize(requestPayload, JsonOptions);
            using var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Send request to configured endpoint
            var response = await _http.PostAsync(_options.ResponsesApiPath, content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // Parse structured error response
                var errorDetails = TryParseOpenAiError(body);
                _logger.LogError("OpenAI error {StatusCode}. Code={ErrCode} Message={ErrMsg}. Raw={Raw}",
                    (int)response.StatusCode, errorDetails.Code ?? "-", errorDetails.Message ?? "-", body);

                var error = response.StatusCode switch
                {
                    HttpStatusCode.TooManyRequests => AiErrors.RateLimited,
                    HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => AiErrors.RequestFailed,
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => AiErrors.RequestFailed,
                    _ => AiErrors.ServiceUnavailable
                };

                return Result.Failure<AiResponse, Error>(error);
            }

            // Parse structured response
            var parsed = ParseResponse(body);
            if (parsed.IsFailure)
            {
                _logger.LogWarning("Failed to parse/validate AI response: {ErrorCode}", parsed.Error.Code);
                return parsed;
            }

            _logger.LogInformation("Responses API request succeeded (responseId={ResponseId})", parsed.Value.ResponseId.Value);
            return Result.Success<AiResponse, Error>(parsed.Value);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Responses API call cancelled.");
            throw; // bubble up cancellation
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed during Responses API call.");
            return Result.Failure<AiResponse, Error>(AiErrors.ServiceUnavailable);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Responses API call timed out.");
            return Result.Failure<AiResponse, Error>(AiErrors.ServiceUnavailable);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Network I/O error during Responses API call.");
            return Result.Failure<AiResponse, Error>(AiErrors.ServiceUnavailable);
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogError(ex, "Connection was disposed during Responses API call.");
            return Result.Failure<AiResponse, Error>(AiErrors.ServiceUnavailable);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON serialization/deserialization error during Responses API call.");
            return Result.Failure<AiResponse, Error>(AiErrors.ResponseInvalid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during Responses API call.");
            return Result.Failure<AiResponse, Error>(AiErrors.RequestFailed);
        }
    }

    private OpenAiResponsesRequest BuildRequestPayload(AiRequest request)
    {
        // Use tool builder to create provider-specific tool definitions
        var tools = _toolBuilder.BuildTools(request.McpConfigs);

        // Create strongly-typed request
        return new OpenAiResponsesRequest
        {
            Model = _options.Model,
            Input = request.Message.Value, // Vogen VO - extract underlying string
            Tools = tools,
            PreviousResponseId = string.IsNullOrWhiteSpace(request.PreviousResponseId) ? null : request.PreviousResponseId,
            MaxTokens = _options.MaxTokens
        };
    }

    private static Result<AiResponse, Error> ParseResponse(string body)
    {
        try
        {
            // Deserialize using strongly-typed DTO with consistent options
            var response = JsonSerializer.Deserialize<OpenAiResponsesResponse>(body, JsonOptions);

            if (response == null || string.IsNullOrWhiteSpace(response.Id))
                return Result.Failure<AiResponse, Error>(AiErrors.ResponseInvalid);

            // Extract content: prefer output_text, fallback to structured output
            var contentStr = response.OutputText ?? ExtractTextFromOutput(response.Output);

            if (string.IsNullOrWhiteSpace(contentStr))
                return Result.Failure<AiResponse, Error>(AiErrors.ResponseInvalid);

            // Validate and construct value objects
            if (!MessageContent.TryParse(contentStr, provider: null, out var contentVo))
                return Result.Failure<AiResponse, Error>(AiErrors.ResponseInvalid);

            var responseIdVo = new AiResponseId(response.Id);

            var aiResponse = new AiResponse(
                Content: contentVo,
                ResponseId: responseIdVo,
                ToolExecutions: null);

            return Result.Success<AiResponse, Error>(aiResponse);
        }
        catch (JsonException)
        {
            return Result.Failure<AiResponse, Error>(AiErrors.ResponseInvalid);
        }
        catch (Exception)
        {
            return Result.Failure<AiResponse, Error>(AiErrors.ResponseInvalid);
        }
    }

    private static string? ExtractTextFromOutput(OutputMessage[]? output)
    {
        if (output == null || output.Length == 0)
            return null;

        var sb = new StringBuilder();

        foreach (var message in output)
        {
            if (message.Type != "message" || message.Content == null)
                continue;

            foreach (var block in message.Content)
            {
                if (block.Type == "text" && !string.IsNullOrEmpty(block.Text))
                {
                    if (sb.Length > 0) sb.AppendLine();
                    sb.Append(block.Text);
                }
            }
        }

        return sb.Length == 0 ? null : sb.ToString();
    }

    private static ErrorDetail TryParseOpenAiError(string body)
    {
        try
        {
            var errorResponse = JsonSerializer.Deserialize<OpenAiErrorResponse>(body, JsonOptions);
            return errorResponse?.Error ?? ErrorDetail.Unknown();
        }
        catch
        {
            return ErrorDetail.ParseFailure();
        }
    }
}
