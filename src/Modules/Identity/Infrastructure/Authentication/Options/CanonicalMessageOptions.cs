namespace Axon.Modules.Identity.Infrastructure.Services;

public sealed class CanonicalMessageOptions
{
    // REQUIRED: 32+ bytes of random secret; base64 or raw
    public string HmacSecret { get; set; } = string.Empty;

    // Defaults match your story; override via config if needed
    public int MaxTtlSeconds { get; set; } = 300;   // 5 minutes
    public int ClockSkewSeconds { get; set; } = 60; // ±60s
    public string DefaultAudience { get; set; } = "axon-api";
}