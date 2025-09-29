using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;

namespace Axon.Modules.Chat.Domain.Services;

/// <summary>
/// Domain service for validating message sequences and turn-taking rules.
/// </summary>
public interface IMessageSequenceValidationService
{
    /// <summary>
    /// Validates that a new message with the given role can be added to the sequence.
    /// </summary>
    Result<Unit, Error> ValidateMessageSequence(IReadOnlyList<Message> existingMessages, MessageRole newMessageRole);

    /// <summary>
    /// Gets the next valid sequence number for a new message.
    /// </summary>
    int GetNextSequenceNumber(IReadOnlyList<Message> messages);

    /// <summary>
    /// Validates that the message sequence maintains integrity (no gaps, proper ordering).
    /// </summary>
    Result<Unit, Error> ValidateSequenceIntegrity(IReadOnlyList<Message> messages);

    /// <summary>
    /// Checks if the conversation follows proper turn-taking between user and assistant.
    /// </summary>
    bool IsValidTurnTaking(IReadOnlyList<Message> messages);
}