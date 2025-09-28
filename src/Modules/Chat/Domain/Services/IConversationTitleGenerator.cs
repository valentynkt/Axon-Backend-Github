using Axon.Modules.Chat.Domain.Entities;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;

namespace Axon.Modules.Chat.Domain.Services;

/// <summary>
/// Domain service for generating and validating conversation titles.
/// </summary>
public interface IConversationTitleGenerator
{
    /// <summary>
    /// Generates a title based on the first few messages of the conversation.
    /// </summary>
    Result<string, Error> GenerateTitleFromMessages(IReadOnlyList<Message> messages);

    /// <summary>
    /// Validates that a proposed title meets business requirements.
    /// </summary>
    Result<Unit, Error> ValidateTitle(string title);

    /// <summary>
    /// Sanitizes a title to meet business requirements.
    /// </summary>
    string SanitizeTitle(string title);
}