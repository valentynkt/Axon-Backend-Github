using Axon.Modules.Chat.Application.Commands.ProcessMessage;

namespace Axon.Tests.Shared.Builders;

/// <summary>
/// Builder for creating ProcessMessageCommand instances for testing using the Mother Object pattern
/// </summary>
public class ProcessMessageCommandBuilder
{
    private string _message = "Default test message";
    private string? _previousResponseId;

    /// <summary>
    /// Sets the message content
    /// </summary>
    public ProcessMessageCommandBuilder WithMessage(string message)
    {
        _message = message;
        return this;
    }

    /// <summary>
    /// Sets a simple message for basic scenarios
    /// </summary>
    public ProcessMessageCommandBuilder WithSimpleMessage() => WithMessage("Hello, AI!");

    /// <summary>
    /// Sets a complex message for advanced scenarios
    /// </summary>
    public ProcessMessageCommandBuilder WithComplexMessage() => 
        WithMessage("Please analyze this data and provide insights using available tools.");

    /// <summary>
    /// Sets an empty message (for validation testing)
    /// </summary>
    public ProcessMessageCommandBuilder WithEmptyMessage() => WithMessage("");

    /// <summary>
    /// Sets a null message (for validation testing)
    /// </summary>
    public ProcessMessageCommandBuilder WithNullMessage() => WithMessage(null!);

    /// <summary>
    /// Sets a whitespace-only message (for validation testing)
    /// </summary>
    public ProcessMessageCommandBuilder WithWhitespaceMessage() => WithMessage("   ");


    /// <summary>
    /// Sets the previous response ID
    /// </summary>
    public ProcessMessageCommandBuilder WithPreviousResponseId(string responseId)
    {
        _previousResponseId = responseId;
        return this;
    }

    /// <summary>
    /// Sets a valid previous response ID
    /// </summary>
    public ProcessMessageCommandBuilder WithValidPreviousResponse() => 
        WithPreviousResponseId(Guid.NewGuid().ToString());

    /// <summary>
    /// Sets up a complete configuration with previous response
    /// </summary>
    public ProcessMessageCommandBuilder WithFullConfiguration() =>
        WithValidPreviousResponse();

    /// <summary>
    /// Sets up a minimal valid command
    /// </summary>
    public ProcessMessageCommandBuilder AsMinimalValid() => WithSimpleMessage();

    /// <summary>
    /// Sets up a command for error testing
    /// </summary>
    public ProcessMessageCommandBuilder AsInvalid() => WithEmptyMessage();

    /// <summary>
    /// Builds the ProcessMessageCommand
    /// </summary>
    public ProcessMessageCommand Build() => new(
        Message: _message,
        PreviousResponseId: _previousResponseId);

    /// <summary>
    /// Creates a new builder instance (fluent interface)
    /// </summary>
    public static ProcessMessageCommandBuilder New() => new();

    /// <summary>
    /// Creates a builder with a specific message
    /// </summary>
    public static ProcessMessageCommandBuilder ForMessage(string message) => 
        new ProcessMessageCommandBuilder().WithMessage(message);

    /// <summary>
    /// Creates a builder for a minimal valid command
    /// </summary>
    public static ProcessMessageCommandBuilder MinimalValid() => 
        new ProcessMessageCommandBuilder().AsMinimalValid();

    /// <summary>
    /// Creates a builder for a complete command with previous response
    /// </summary>  
    public static ProcessMessageCommandBuilder WithPreviousResponse() => 
        new ProcessMessageCommandBuilder().WithFullConfiguration();
}