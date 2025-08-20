using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Axon.Modules.Chat.Application.Abstractions.AI;
using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Domain.Errors;

using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Axon.Modules.Chat.Infrastructure.Ai;

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

    // Base address should already be set on the typed HttpClient registration
    private const string ResponsesApiUrl = "https://api.openai.com/v1/responses";

    public OpenAiMcpClient(
        HttpClient httpClient,
        ILogger<OpenAiMcpClient> logger,
        IOptions<OpenAiOptions> options)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        ConfigureHttpClient();
    }

    public async Task<Result<AiResponse>> ProcessMessageAsync(
        AiRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                return AiErrors.RequestFailed;

            var requestPayload = BuildRequestPayload(request);
            // Serialize to JSON
            var jsonContent = JsonSerializer.Serialize(requestPayload);
            using var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _logger.LogDebug("Sending Responses API request (continuation={Continuation})",
                !string.IsNullOrWhiteSpace(request.PreviousResponseId));

            using var response = await _http.PostAsync(ResponsesApiUrl, content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // Try to extract OpenAI error for better diagnostics (still map to our domain errors)
                var (errCode, errMsg) = TryParseOpenAiError(body);
                _logger.LogError("OpenAI error {StatusCode}. Code={ErrCode} Message={ErrMsg}. Raw={Raw}",
                    (int)response.StatusCode, errCode ?? "-", errMsg ?? "-", body);

                return response.StatusCode switch
                {
                    HttpStatusCode.TooManyRequests => AiErrors.ServiceUnavailable,  // simplest mapping
                    HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => AiErrors.RequestFailed,
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => AiErrors.RequestFailed,
                    _ => AiErrors.ServiceUnavailable
                };
            }

            var ai = ParseResponse(body);
            _logger.LogInformation("Responses API request succeeded (responseId={ResponseId})", ai.ResponseId);
            return Result<AiResponse>.Success(ai);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Responses API call cancelled.");
            throw; // bubble up cancellation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Responses API call failed.");
            return AiErrors.RequestFailed;
        }
    }

    private void ConfigureHttpClient()
    {
        _http.DefaultRequestHeaders.Clear();
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Axon-Chat/1.0");
        _http.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds > 0 ? _options.TimeoutSeconds : 60);
    }

    private object BuildRequestPayload(AiRequest request)
    {
        // Build the smallest object that matches the Responses API
        var tools = BuildMcpTools(request.McpConfigs);

        // NOTE: use anonymous type to keep it simple; PostAsJsonAsync will serialize it
        var payload = new
        {
            model = _options.Model,
            input = request.Message,
            tools = tools, // null omitted by serializer
            previous_response_id = string.IsNullOrWhiteSpace(request.PreviousResponseId) ? null : request.PreviousResponseId,
        };

        return payload;
    }

    private static object[]? BuildMcpTools(IReadOnlyCollection<McpServerConfig>? mcpConfigs)
    {
        if (mcpConfigs == null || mcpConfigs.Count == 0)
            return null;

        var tools = new List<object>(mcpConfigs.Count);

        foreach (var cfg in mcpConfigs)
        {
            // Only include what we actually have; keep it minimal
            var tool = new Dictionary<string, object?>
            {
                ["type"] = "mcp",
                ["server_url"] = cfg.ServerUrl,
                ["server_label"] = string.IsNullOrWhiteSpace(cfg.ServerLabel) ? "MCP Server" : cfg.ServerLabel,
                // The Responses API currently accepts approval policies; keep the semantics simple:
                ["require_approval"] = cfg.RequireApproval ? "always" : "never"
            };

            if (cfg.Headers is { Count: > 0 })
                tool["headers"] = cfg.Headers;

            if (cfg.AllowedTools != null && cfg.AllowedTools.Any())
                tool["allowed_tools"] = cfg.AllowedTools;

            tools.Add(tool);
        }

        return tools.ToArray();
    }

    private static AiResponse ParseResponse(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        // id
        var responseId = root.TryGetProperty("id", out var idEl)
            ? idEl.GetString() ?? Guid.NewGuid().ToString("N")
            : Guid.NewGuid().ToString("N");

        // Prefer top-level "output_text" when present (Responses API often includes it)
        if (root.TryGetProperty("output_text", out var ot) && ot.ValueKind == JsonValueKind.String)
        {
            return new AiResponse(
                Content: ot.GetString() ?? string.Empty,
                ResponseId: responseId,
                ToolExecutions: null);
        }

        // Fallback: output[].content[].text (message items)
        var content = ExtractTextFromOutputArray(root) ?? string.Empty;

        return new AiResponse(
            Content: content,
            ResponseId: responseId,
            ToolExecutions: null);
    }

    private static string? ExtractTextFromOutputArray(JsonElement root)
    {
        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
            return null;

        var sb = new StringBuilder();

        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("type", out var type) || type.GetString() != "message")
                continue;

            if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var part in content.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String)
                {
                    var t = textEl.GetString();
                    if (!string.IsNullOrEmpty(t))
                    {
                        if (sb.Length > 0) sb.AppendLine();
                        sb.Append(t);
                    }
                }
            }
        }

        return sb.Length == 0 ? null : sb.ToString();
    }

    private static (string? Code, string? Message) TryParseOpenAiError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.Object)
            {
                string? code = null;
                string? message = null;

                if (err.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String)
                    code = c.GetString();

                if (err.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
                    message = m.GetString();

                return (code, message);
            }

            return (null, null);
        }
        catch
        {
            return (null, null);
        }
    }
}
