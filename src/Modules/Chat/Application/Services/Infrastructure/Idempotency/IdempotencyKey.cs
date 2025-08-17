using System.Security.Cryptography;
using System.Text;

namespace Axon.Modules.Chat.Application.Services.Infrastructure.Idempotency;

/// <summary>
/// Utility for computing idempotency keys
/// </summary>
public static class IdempotencyKey
{
    /// <summary>
    /// Computes a deterministic idempotency key from input parameters
    /// </summary>
    public static string Compute(string content, Guid conversationId, Guid userId)
    {
        var input = $"{content}|{conversationId}|{userId}";
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}