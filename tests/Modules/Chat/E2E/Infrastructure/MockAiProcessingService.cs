using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Application.Contracts.AI;
using Axon.Modules.Chat.Application.DTOs.Configurations;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Chat.E2E.Infrastructure;

/// <summary>
/// Mock AI processing service for E2E tests.
/// Provides deterministic, predictable responses with configurable behavior.
/// Thread-safe for concurrent test execution.
/// </summary>
public sealed class MockAiProcessingService : IAiProcessingService
{
    private readonly object _lock = new();
    private MockBehavior _behavior = MockBehavior.Success;
    private string _fixedResponseId = "test_ai_response_id";
    private int _responseCounter;

    /// <summary>
    /// Mock behavior modes for different test scenarios.
    /// </summary>
    public enum MockBehavior
    {
        Success,
        Failure,
        Timeout,
        DuplicateResponseId
    }

    /// <summary>
    /// Configure the mock behavior for the next ProcessMessageAsync call.
    /// Thread-safe.
    /// </summary>
    /// <param name="behavior">The behavior mode to use</param>
    public void ConfigureBehavior(MockBehavior behavior)
    {
        lock (_lock)
        {
            _behavior = behavior;
        }
    }

    /// <summary>
    /// Set a fixed AI response ID for testing idempotency scenarios.
    /// Thread-safe.
    /// </summary>
    /// <param name="responseId">The fixed response ID to use</param>
    public void SetFixedResponseId(string responseId)
    {
        lock (_lock)
        {
            _fixedResponseId = responseId;
        }
    }

    /// <summary>
    /// Reset the mock to default behavior (Success).
    /// Thread-safe.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _behavior = MockBehavior.Success;
            _fixedResponseId = "test_ai_response_id";
            _responseCounter = 0;
        }
    }

    public Task<Result<AiProcessingResult, Error>> ProcessMessageAsync(
        MessageContent userMessage,
        ConversationId conversationId,
        AiResponseId? previousResponseId,
        McpServerConfig[]? mcpConfigs,
        CancellationToken cancellationToken)
    {
        MockBehavior currentBehavior;
        string currentFixedId;
        int currentCounter;

        // Lock only for reading shared state
        lock (_lock)
        {
            currentBehavior = _behavior;
            currentFixedId = _fixedResponseId;
            currentCounter = _responseCounter++;
        }

        return currentBehavior switch
        {
            MockBehavior.Success => ProcessSuccessAsync(userMessage, currentCounter),
            MockBehavior.Failure => ProcessFailureAsync(),
            MockBehavior.Timeout => ProcessTimeoutAsync(),
            MockBehavior.DuplicateResponseId => ProcessWithDuplicateIdAsync(userMessage, currentFixedId),
            _ => ProcessSuccessAsync(userMessage, currentCounter)
        };
    }

    /// <summary>
    /// Simulates successful AI processing with a deterministic response.
    /// </summary>
    private static Task<Result<AiProcessingResult, Error>> ProcessSuccessAsync(MessageContent userMessage, int counter)
    {
        // Create deterministic assistant response based on user message
        var assistantText = $"Mock AI response to: '{userMessage.Value}' (response #{counter})";
        var assistantContent = MessageContent.Parse(assistantText, provider: null);

        // Create unique response ID based on counter
        var responseId = new AiResponseId($"mock_ai_response_{counter}_{Guid.NewGuid():N}");

        var result = new AiProcessingResult(
            AssistantContent: assistantContent,
            ResponseId: responseId,
            ProcessingDuration: TimeSpan.FromMilliseconds(50) // Fast mock response
        );

        return Task.FromResult(Result.Success<AiProcessingResult, Error>(result));
    }

    /// <summary>
    /// Simulates AI service failure.
    /// </summary>
    private static Task<Result<AiProcessingResult, Error>> ProcessFailureAsync()
    {
        var error = Error.Internal(
            "Mock AI service failure for testing",
            "MOCK_AI_FAILURE");

        return Task.FromResult(Result.Failure<AiProcessingResult, Error>(error));
    }

    /// <summary>
    /// Simulates AI service timeout.
    /// </summary>
    private static Task<Result<AiProcessingResult, Error>> ProcessTimeoutAsync()
    {
        var error = Error.Timeout(
            "Mock AI service timeout for testing",
            "MOCK_AI_TIMEOUT",
            timeout: TimeSpan.FromSeconds(30));

        return Task.FromResult(Result.Failure<AiProcessingResult, Error>(error));
    }

    /// <summary>
    /// Simulates duplicate AI response ID for idempotency testing.
    /// </summary>
    private static Task<Result<AiProcessingResult, Error>> ProcessWithDuplicateIdAsync(MessageContent userMessage, string fixedId)
    {
        var assistantText = $"Mock AI response with duplicate ID to: '{userMessage.Value}'";
        var assistantContent = MessageContent.Parse(assistantText, provider: null);

        // Use the fixed response ID to simulate duplicate
        var responseId = new AiResponseId(fixedId);

        var result = new AiProcessingResult(
            AssistantContent: assistantContent,
            ResponseId: responseId,
            ProcessingDuration: TimeSpan.FromMilliseconds(50)
        );

        return Task.FromResult(Result.Success<AiProcessingResult, Error>(result));
    }
}
