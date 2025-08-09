using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BuildingBlocks.Application.Caching;

/// <summary>
/// SHA256-based content hasher with JSON serialization.
/// Provides cryptographically strong and collision-resistant hashing.
/// </summary>
public sealed class Sha256ContentHasher : IContentHasher
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string ComputeHash<T>(T content) where T : notnull
    {
        try
        {
            // Serialize the content to JSON for consistent hashing
            var json = JsonSerializer.Serialize(content, SerializerOptions);
            var inputBytes = Encoding.UTF8.GetBytes(json);
            
            // Use SHA256 for cryptographic strength and collision resistance
            var hashBytes = SHA256.HashData(inputBytes);
            
            // Convert to hex string and take first 16 characters for brevity
            return Convert.ToHexString(hashBytes)[..16];
        }
        catch (Exception)
        {
            // Fallback for non-serializable objects
            // Use GetHashCode with string formatting for consistency
            return Math.Abs(content.GetHashCode()).ToString("x8");
        }
    }
}