using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Axon.Modules.Chat.Application.Commands.AppendUserMessage;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Abstractions.Idempotency;
using BuildingBlocks.Core.Domain.Primitives;

namespace Axon.Modules.Chat.Application.Services.Idempotency;

/// <summary>
/// Provides idempotency key generation for chat commands.
/// Generates deterministic keys based on command content, conversation ID, and user context.
/// </summary>
public sealed partial class ChatIdempotencyKeyProvider : IIdempotencyKeyProvider<AppendUserMessageCommand>
{
    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceNormalizer();

    /// <summary>
    /// Generates an idempotency key for AppendUserMessageCommand.
    /// The key is based on normalized message content, conversation ID, and user ID to ensure
    /// the same logical operation always gets the same key.
    /// </summary>
    /// <param name="command">The command to generate a key for.</param>
    /// <param name="currentUser">The current user context.</param>
    /// <returns>A deterministic idempotency key.</returns>
    public string GenerateKey(AppendUserMessageCommand command, ICurrentUserService currentUser)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId))
        {
            throw new InvalidOperationException("Cannot generate idempotency key for unauthenticated user");
        }

        var userIdResult = UserId.FromString(currentUser.UserId);
        if (userIdResult.IsFailure)
        {
            throw new InvalidOperationException($"Invalid user ID: {userIdResult.Error.Message}");
        }

        // Normalize content: trim and collapse whitespace
        var normalizedContent = NormalizeContent(command.Content.Value);

        // Create deterministic key from normalized content, conversation ID, and user ID
        var input = $"{normalizedContent}|{command.ConversationId.Value}|{userIdResult.Value.Value}";
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        var hashString = Convert.ToHexString(hash).ToLowerInvariant();
        
        return $"chat:append-message:{hashString}";
    }

    private static string NormalizeContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return string.Empty;

        // Trim and collapse multiple whitespace characters to single space
        return WhitespaceNormalizer().Replace(content.Trim(), " ");
    }
}