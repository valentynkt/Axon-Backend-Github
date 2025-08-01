using System.Net;
using System.Text.Json;
using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Domain.Types;
using Axon.Shared.Common;
using Microsoft.Extensions.Logging;
using Moq;

namespace Axon.Tests.Shared.TestDoubles;

/// <summary>
/// Comprehensive test doubles for external dependencies following London School TDD
/// </summary>
public static class ExternalDependencyDoubles
{
    /// <summary>
    /// OpenAI service test double with configurable behavior
    /// </summary>
    public class OpenAiServiceDouble : IAiClient
    {
        private readonly List<(AiRequest Request, AiResponse Response)> _interactions = new();
        private readonly Queue<Result<AiResponse>> _responseQueue = new();
        private Func<AiRequest, Result<AiResponse>>? _responseStrategy;

        public IReadOnlyList<(AiRequest Request, AiResponse Response)> Interactions => _interactions.AsReadOnly();

        /// <summary>
        /// Sets up the double to return specific responses in order
        /// </summary>
        public void SetupResponses(params Result<AiResponse>[] responses)
        {
            _responseQueue.Clear();
            foreach (var response in responses)
            {
                _responseQueue.Enqueue(response);
            }
        }

        /// <summary>
        /// Sets up the double to use a strategy function for responses
        /// </summary>
        public void SetupResponseStrategy(Func<AiRequest, Result<AiResponse>> strategy)
        {
            _responseStrategy = strategy;
        }

        /// <summary>
        /// Sets up the double to always return success with specific content
        /// </summary>
        public void SetupSuccessResponse(string content, string? responseId = null, ToolExecution[]? toolExecutions = null)
        {
            var response = new AiResponse(content, responseId ?? Guid.NewGuid().ToString(), toolExecutions);
            SetupResponses(Result<AiResponse>.Success(response));
        }

        /// <summary>
        /// Sets up the double to always return failure
        /// </summary>
        public void SetupFailureResponse(Error error)
        {
            SetupResponses(Result<AiResponse>.Failure(error));
        }

        public async Task<Result<AiResponse>> ProcessMessageAsync(AiRequest request, CancellationToken cancellationToken = default)
        {
            // Use strategy if configured
            if (_responseStrategy != null)
            {
                var result = _responseStrategy(request);
                if (result.IsSuccess)
                {
                    _interactions.Add((request, result.Value));
                }
                return result;
            }

            // Use queued responses
            if (_responseQueue.Count > 0)
            {
                var result = _responseQueue.Dequeue();
                if (result.IsSuccess)
                {
                    _interactions.Add((request, result.Value));
                }
                return result;
            }

            // Default behavior
            var defaultResponse = new AiResponse($"Default response for: {request.Message}", Guid.NewGuid().ToString(), null);
            _interactions.Add((request, defaultResponse));
            return Result<AiResponse>.Success(defaultResponse);
        }

        /// <summary>
        /// Verifies that the double was called with expected request pattern
        /// </summary>
        public void VerifyCalledWith(Func<AiRequest, bool> requestMatcher, string description = "")
        {
            var matchingInteraction = _interactions.FirstOrDefault(i => requestMatcher(i.Request));
            if (matchingInteraction == default)
            {
                throw new InvalidOperationException($"Expected interaction not found: {description}");
            }
        }

        /// <summary>
        /// Verifies the total number of interactions
        /// </summary>
        public void VerifyInteractionCount(int expectedCount)
        {
            if (_interactions.Count != expectedCount)
            {
                throw new InvalidOperationException($"Expected {expectedCount} interactions, but got {_interactions.Count}");
            }
        }

        /// <summary>
        /// Resets the double for next test
        /// </summary>
        public void Reset()
        {
            _interactions.Clear();
            _responseQueue.Clear();
            _responseStrategy = null;
        }
    }

    /// <summary>
    /// MCP Server resolver test double with configuration scenarios
    /// </summary>
    public class McpServerResolverDouble : IMcpServerResolver
    {
        private readonly List<Result<IReadOnlyCollection<McpServerConfig>>> _calls = new();
        private Result<IReadOnlyCollection<McpServerConfig>>? _configuredResult;

        public IReadOnlyList<Result<IReadOnlyCollection<McpServerConfig>>> Calls => _calls.AsReadOnly();

        /// <summary>
        /// Sets up the double to return specific configuration
        /// </summary>
        public void SetupConfiguration(IReadOnlyCollection<McpServerConfig> configs)
        {
            _configuredResult = Result<IReadOnlyCollection<McpServerConfig>>.Success(configs);
        }

        /// <summary>
        /// Sets up the double to return empty configuration
        /// </summary>
        public void SetupEmptyConfiguration()
        {
            SetupConfiguration(new List<McpServerConfig>().AsReadOnly());
        }

        /// <summary>
        /// Sets up the double to return failure
        /// </summary>
        public void SetupFailure(Error error)
        {
            _configuredResult = Result<IReadOnlyCollection<McpServerConfig>>.Failure(error);
        }

        /// <summary>
        /// Sets up the double with multiple MCP servers for testing
        /// </summary>
        public void SetupMultipleServers(params (string Url, string Label, string[] Tools)[] servers)
        {
            var configs = servers.Select(s => new McpServerConfig(
                ServerUrl: s.Url,
                ServerLabel: s.Label,
                Headers: null,
                AllowedTools: s.Tools,
                RequireApproval: false)).ToList().AsReadOnly();
            
            SetupConfiguration(configs);
        }

        public Result<IReadOnlyCollection<McpServerConfig>> GetEnabledServerConfigurations()
        {
            var result = _configuredResult ?? Result<IReadOnlyCollection<McpServerConfig>>.Success(
                new List<McpServerConfig>().AsReadOnly());
            
            _calls.Add(result);
            return result;
        }

        /// <summary>
        /// Verifies the double was called the expected number of times
        /// </summary>
        public void VerifyCallCount(int expectedCount)
        {
            if (_calls.Count != expectedCount)
            {
                throw new InvalidOperationException($"Expected {expectedCount} calls, but got {_calls.Count}");
            }
        }

        /// <summary>
        /// Resets the double for next test
        /// </summary>
        public void Reset()
        {
            _calls.Clear();
            _configuredResult = null;
        }
    }

    /// <summary>
    /// HTTP client test double for infrastructure testing
    /// </summary>
    public class HttpClientDouble
    {
        private readonly List<(HttpRequestMessage Request, HttpResponseMessage Response)> _interactions = new();
        private readonly Queue<HttpResponseMessage> _responseQueue = new();
        private Func<HttpRequestMessage, HttpResponseMessage>? _responseStrategy;

        public IReadOnlyList<(HttpRequestMessage Request, HttpResponseMessage Response)> Interactions => _interactions.AsReadOnly();

        /// <summary>
        /// Sets up the double to return specific HTTP responses in order
        /// </summary>
        public void SetupResponses(params HttpResponseMessage[] responses)
        {
            _responseQueue.Clear();
            foreach (var response in responses)
            {
                _responseQueue.Enqueue(response);
            }
        }

        /// <summary>
        /// Sets up the double to use a strategy function for responses
        /// </summary>
        public void SetupResponseStrategy(Func<HttpRequestMessage, HttpResponseMessage> strategy)
        {
            _responseStrategy = strategy;
        }

        /// <summary>
        /// Sets up successful OpenAI response
        /// </summary>
        public void SetupOpenAiSuccessResponse(string outputText, string? responseId = null)
        {
            var responseBody = JsonSerializer.Serialize(new
            {
                id = responseId ?? Guid.NewGuid().ToString(),
                output_text = outputText,
                mcp_calls = Array.Empty<object>()
            });

            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, System.Text.Encoding.UTF8, "application/json")
            };

            SetupResponses(httpResponse);
        }

        /// <summary>
        /// Sets up HTTP error response
        /// </summary>
        public void SetupErrorResponse(HttpStatusCode statusCode, string errorMessage)
        {
            var httpResponse = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(errorMessage)
            };

            SetupResponses(httpResponse);
        }

        /// <summary>
        /// Simulates the HTTP request/response cycle
        /// </summary>
        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
        {
            HttpResponseMessage response;

            if (_responseStrategy != null)
            {
                response = _responseStrategy(request);
            }
            else if (_responseQueue.Count > 0)
            {
                response = _responseQueue.Dequeue();
            }
            else
            {
                response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"id\":\"default\",\"output_text\":\"Default response\"}")
                };
            }

            _interactions.Add((request, response));
            return response;
        }

        /// <summary>
        /// Verifies HTTP request was made with expected properties
        /// </summary>
        public void VerifyRequestMade(Func<HttpRequestMessage, bool> requestMatcher, string description = "")
        {
            var matchingInteraction = _interactions.FirstOrDefault(i => requestMatcher(i.Request));
            if (matchingInteraction == default)
            {
                throw new InvalidOperationException($"Expected HTTP request not found: {description}");
            }
        }

        /// <summary>
        /// Verifies HTTP request payload content
        /// </summary>
        public async Task VerifyRequestPayload(Func<string, bool> payloadMatcher, string description = "")
        {
            foreach (var interaction in _interactions)
            {
                if (interaction.Request.Content != null)
                {
                    var payload = await interaction.Request.Content.ReadAsStringAsync();
                    if (payloadMatcher(payload))
                    {
                        return; // Found matching payload
                    }
                }
            }
            throw new InvalidOperationException($"Expected HTTP request payload not found: {description}");
        }

        /// <summary>
        /// Resets the double for next test
        /// </summary>
        public void Reset()
        {
            _interactions.Clear();
            _responseQueue.Clear();
            _responseStrategy = null;
        }
    }

    /// <summary>
    /// Logger test double that captures log entries for verification
    /// </summary>
    public class LoggerDouble<T> : ILogger<T>
    {
        private readonly List<LogEntry> _logEntries = new();

        public IReadOnlyList<LogEntry> LogEntries => _logEntries.AsReadOnly();

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            _logEntries.Add(new LogEntry(logLevel, eventId, message, exception));
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        /// <summary>
        /// Verifies that a log entry with specific criteria was recorded
        /// </summary>
        public void VerifyLogEntry(LogLevel expectedLevel, string expectedMessagePattern)
        {
            var matchingEntry = _logEntries.FirstOrDefault(e => 
                e.LogLevel == expectedLevel && 
                e.Message.Contains(expectedMessagePattern));

            if (matchingEntry == null)
            {
                throw new InvalidOperationException($"Expected log entry not found: Level={expectedLevel}, Pattern={expectedMessagePattern}");
            }
        }

        /// <summary>
        /// Verifies the total number of log entries
        /// </summary>
        public void VerifyLogEntryCount(int expectedCount)
        {
            if (_logEntries.Count != expectedCount)
            {
                throw new InvalidOperationException($"Expected {expectedCount} log entries, but got {_logEntries.Count}");
            }
        }

        /// <summary>
        /// Resets the double for next test
        /// </summary>
        public void Reset()
        {
            _logEntries.Clear();
        }

        public record LogEntry(LogLevel LogLevel, EventId EventId, string Message, Exception? Exception);
    }

    /// <summary>
    /// Factory for creating configured test doubles
    /// </summary>
    public static class Factory
    {
        /// <summary>
        /// Creates OpenAI service double with successful configuration
        /// </summary>
        public static OpenAiServiceDouble CreateSuccessfulOpenAiDouble(string responseContent = "Mock AI response")
        {
            var openAiDouble = new OpenAiServiceDouble();
            openAiDouble.SetupSuccessResponse(responseContent);
            return openAiDouble;
        }

        /// <summary>
        /// Creates OpenAI service double that fails
        /// </summary>
        public static OpenAiServiceDouble CreateFailingOpenAiDouble(Error error)
        {
            var openAiDouble = new OpenAiServiceDouble();
            openAiDouble.SetupFailureResponse(error);
            return openAiDouble;
        }

        /// <summary>
        /// Creates MCP resolver double with empty configuration
        /// </summary>
        public static McpServerResolverDouble CreateEmptyMcpResolverDouble()
        {
            var resolverDouble = new McpServerResolverDouble();
            resolverDouble.SetupEmptyConfiguration();
            return resolverDouble;
        }

        /// <summary>
        /// Creates MCP resolver double with test servers
        /// </summary>
        public static McpServerResolverDouble CreateMcpResolverWithServers(params (string Url, string Label, string[] Tools)[] servers)
        {
            var resolverDouble = new McpServerResolverDouble();
            resolverDouble.SetupMultipleServers(servers);
            return resolverDouble;
        }

        /// <summary>
        /// Creates HTTP client double with successful OpenAI response
        /// </summary>
        public static HttpClientDouble CreateSuccessfulHttpDouble(string outputText = "Mock response")
        {
            var httpDouble = new HttpClientDouble();
            httpDouble.SetupOpenAiSuccessResponse(outputText);
            return httpDouble;
        }

        /// <summary>
        /// Creates HTTP client double that returns errors
        /// </summary>
        public static HttpClientDouble CreateFailingHttpDouble(HttpStatusCode statusCode, string errorMessage)
        {
            var httpDouble = new HttpClientDouble();
            httpDouble.SetupErrorResponse(statusCode, errorMessage);
            return httpDouble;
        }
    }
}