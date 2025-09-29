using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;

namespace Axon.Modules.Chat.Domain.Services;

/// <summary>
/// Implementation of message sequence validation service.
/// </summary>
public sealed class MessageSequenceValidationService : IMessageSequenceValidationService
{
    public Result<Unit, Error> ValidateMessageSequence(IReadOnlyList<Message> existingMessages, MessageRole newMessageRole)
    {
        if (!existingMessages.Any())
        {
            // First message must be from user
            if (newMessageRole != MessageRole.User)
            {
                return Result.Failure<Unit, Error>(
                    Error.Validation("First message must be from user", "CHAT008"));
            }
            return Result.Success<Unit, Error>(Unit.Value);
        }

        var lastMessage = existingMessages.MaxBy(m => m.Sequence);
        if (lastMessage == null)
        {
            return Result.Success<Unit, Error>(Unit.Value);
        }

        // Check turn-taking rule
        if (lastMessage.Role == newMessageRole)
        {
            return Result.Failure<Unit, Error>(
                Error.Validation($"Cannot have consecutive {newMessageRole} messages", "CHAT009"));
        }

        return Result.Success<Unit, Error>(Unit.Value);
    }

    public int GetNextSequenceNumber(IReadOnlyList<Message> messages)
    {
        if (!messages.Any())
            return 1;

        return messages.Max(m => m.Sequence) + 1;
    }

    public Result<Unit, Error> ValidateSequenceIntegrity(IReadOnlyList<Message> messages)
    {
        if (!messages.Any())
            return Result.Success<Unit, Error>(Unit.Value);

        var orderedMessages = messages.OrderBy(m => m.Sequence).ToList();
        
        // Check for sequence gaps
        for (int i = 0; i < orderedMessages.Count; i++)
        {
            var expectedSequence = i + 1;
            if (orderedMessages[i].Sequence != expectedSequence)
            {
                return Result.Failure<Unit, Error>(
                    Error.Validation($"Sequence gap detected at position {i}", "CHAT012"));
            }
        }

        // Check for duplicates
        var duplicateSequences = messages
            .GroupBy(m => m.Sequence)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateSequences.Any())
        {
            return Result.Failure<Unit, Error>(
                Error.Validation($"Duplicate sequence numbers: {string.Join(", ", duplicateSequences)}", "CHAT013"));
        }

        return Result.Success<Unit, Error>(Unit.Value);
    }

    public bool IsValidTurnTaking(IReadOnlyList<Message> messages)
    {
        if (messages.Count <= 1)
            return true;

        var orderedMessages = messages.OrderBy(m => m.Sequence).ToList();
        
        for (int i = 1; i < orderedMessages.Count; i++)
        {
            if (orderedMessages[i].Role == orderedMessages[i - 1].Role)
            {
                return false;
            }
        }

        return true;
    }
}