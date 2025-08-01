using Axon.Shared.Common;

namespace Axon.Modules.Chat.Domain.ValueObjects;

/// <summary>
/// Represents the processing status of a message
/// </summary>
public readonly record struct MessageStatus
{
    public string Value { get; }

    private MessageStatus(string value) => Value = value;

    public static readonly MessageStatus Draft = new("draft");
    public static readonly MessageStatus Processing = new("processing");
    public static readonly MessageStatus Completed = new("completed");
    public static readonly MessageStatus Failed = new("failed");
    public static readonly MessageStatus Cancelled = new("cancelled");

    /// <summary>
    /// Creates a MessageStatus from a string value with validation
    /// </summary>
    /// <param name="value">The status string value</param>
    /// <returns>Result containing MessageStatus or validation error</returns>
    public static Result<MessageStatus> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Error.Validation("MessageStatus cannot be null or empty");

        var normalizedValue = value.ToLowerInvariant().Trim();

        return normalizedValue switch
        {
            "draft" => Draft,
            "processing" => Processing,
            "completed" => Completed,
            "failed" => Failed,
            "cancelled" => Cancelled,
            _ => Error.Validation($"Invalid message status: {value}. Valid statuses are: draft, processing, completed, failed, cancelled")
        };
    }

    /// <summary>
    /// Determines if the message is in a final state (completed, failed, or cancelled)
    /// </summary>
    public bool IsFinal => Value == Completed.Value || Value == Failed.Value || Value == Cancelled.Value;

    /// <summary>
    /// Determines if the message can be processed
    /// </summary>
    public bool CanBeProcessed => Value == Draft.Value;

    /// <summary>
    /// Determines if the message is currently being processed
    /// </summary>
    public bool IsProcessing => Value == Processing.Value;

    /// <summary>
    /// Determines if the message processing was successful
    /// </summary>
    public bool IsCompleted => Value == Completed.Value;

    /// <summary>
    /// Determines if the message processing failed
    /// </summary>
    public bool IsFailed => Value == Failed.Value;

    public override string ToString() => Value;

    public static implicit operator string(MessageStatus status) => status.Value;
}