// /Modules/Chat/Application/Services/Idempotency/ChatIdempotencyKeyProvider.cs
#nullable enable
using System.Security.Cryptography;
using System.Text;
using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Application.Commands.AppendUserMessage;
using Axon.Modules.Chat.Application.Commands.StartConversation;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Abstractions.Idempotency;

namespace Axon.Modules.Chat.Application.Services.Idempotency;

/// <summary>
/// Generates a single, stable idempotency key for chat messages derived ONLY from message content,
/// regardless of command type (Start vs Append) and regardless of user/tenant.
/// This enables true cross-command de-duplication.
/// </summary>
public sealed class ChatIdempotencyKeyProvider :
    IIdempotencyKeyProvider<StartConversationCommand>,
    IIdempotencyKeyProvider<AppendUserMessageCommand>
{
    public string GenerateKey(StartConversationCommand command, ICurrentUserService _)
        => BuildFromContent(command.Message);

    public string GenerateKey(AppendUserMessageCommand command, ICurrentUserService _)
        => BuildFromContent(command.Content);

    private static string BuildFromContent(MessageContent content)
    {
        // MessageContent.Value is already trimmed/validated via Vogen.
        var hash = Sha256Base64(content.Value);
        // Namespaced prefix so it won't collide with other providers.
        return $"chat:msg:{hash}";
    }

    private static string Sha256Base64(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}