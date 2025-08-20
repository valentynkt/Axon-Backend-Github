// /BuildingBlocks/Core/Abstractions/Idempotency/IdempotencyOptions.cs
namespace BuildingBlocks.Core.Abstractions.Idempotency;

public sealed class IdempotencyOptions
{
    /// <summary>Default cache window if the request doesn’t override.</summary>
    public TimeSpan DefaultWindow { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Cache key prefix.</summary>
    public string KeyPrefix { get; set; } = "idem";

    /// <summary>Header name to read when using HTTP (optional).</summary>
    public string HeaderName { get; set; } = "Idempotency-Key";

    /// <summary>Include current user id (if available) into the derived key.</summary>
    public bool IncludeUserInKey { get; set; } = true;

    /// <summary>Also incorporate request payload hash into the key.</summary>
    public bool IncludePayloadHash { get; set; } = true;
}