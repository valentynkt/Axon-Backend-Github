using System.Text.Json;
using System.Text.Json.Serialization;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Canonical message template v1 for wallet proof with deterministic JSON structure
/// Implements Story 5.5 requirements for hardened wallet authentication
/// </summary>
public sealed record CanonicalMessageV1
{
    // Version identifier for future extensibility
    public const string Version = "v1";

    // Maximum TTL in seconds (5 minutes per requirements)
    public const int MaxTtlSeconds = 300;

    // Clock skew tolerance in seconds
    public const int ClockSkewSeconds = 60;

    [JsonPropertyName("network_environment")]
    [JsonPropertyOrder(1)]
    public string NetworkEnvironment { get; init; }

    [JsonPropertyName("chain_id")]
    [JsonPropertyOrder(2)]
    public string ChainId { get; init; }

    [JsonPropertyName("address")]
    [JsonPropertyOrder(3)]
    public string Address { get; init; }

    [JsonPropertyName("issued_at")]
    [JsonPropertyOrder(4)]
    public long IssuedAt { get; init; }

    [JsonPropertyName("exp")]
    [JsonPropertyOrder(5)]
    public long Exp { get; init; }

    [JsonPropertyName("nbf")]
    [JsonPropertyOrder(6)]
    public long Nbf { get; init; }

    [JsonPropertyName("nonce")]
    [JsonPropertyOrder(7)]
    public string Nonce { get; init; }

    [JsonPropertyName("aud")]
    [JsonPropertyOrder(8)]
    public string Aud { get; init; }

    [JsonPropertyName("version")]
    [JsonPropertyOrder(9)]
    public string MessageVersion { get; init; }

    [JsonPropertyName("mac")]
    [JsonPropertyOrder(10)]
    public string Mac { get; init; }

    private CanonicalMessageV1(
        string networkEnvironment,
        string chainId,
        string address,
        long issuedAt,
        long exp,
        long nbf,
        string nonce,
        string aud,
        string mac)
    {
        NetworkEnvironment = networkEnvironment;
        ChainId = chainId;
        Address = address;
        IssuedAt = issuedAt;
        Exp = exp;
        Nbf = nbf;
        Nonce = nonce;
        Aud = aud;
        MessageVersion = Version;
        Mac = mac;
    }

    /// <summary>
    /// Creates a new canonical message v1 with validation
    /// </summary>
    public static Result<CanonicalMessageV1, Error> Create(
        NetworkEnvironment networkEnvironment,
        ChainId chainId,
        Address address,
        string audience,
        string nonce,
        string mac,
        DateTimeOffset? issuedAt = null)
    {
        if (networkEnvironment == null)
            return Result.Failure<CanonicalMessageV1, Error>(
                Error.Validation("Network environment is required", "CANONICAL_V1.NETWORK_REQUIRED"));

        if (chainId == null)
            return Result.Failure<CanonicalMessageV1, Error>(
                Error.Validation("Chain ID is required", "CANONICAL_V1.CHAIN_REQUIRED"));

        if (address == null)
            return Result.Failure<CanonicalMessageV1, Error>(
                Error.Validation("Address is required", "CANONICAL_V1.ADDRESS_REQUIRED"));

        if (string.IsNullOrWhiteSpace(audience))
            return Result.Failure<CanonicalMessageV1, Error>(
                Error.Validation("Audience is required", "CANONICAL_V1.AUDIENCE_REQUIRED"));

        if (string.IsNullOrWhiteSpace(nonce))
            return Result.Failure<CanonicalMessageV1, Error>(
                Error.Validation("Nonce is required", "CANONICAL_V1.NONCE_REQUIRED"));

        if (string.IsNullOrWhiteSpace(mac))
            return Result.Failure<CanonicalMessageV1, Error>(
                Error.Validation("MAC is required", "CANONICAL_V1.MAC_REQUIRED"));

        var now = issuedAt ?? DateTimeOffset.UtcNow;
        var iat = now.ToUnixTimeSeconds();
        var exp = now.AddSeconds(MaxTtlSeconds).ToUnixTimeSeconds(); // Hard cap at 5 minutes
        var nbf = iat; // Not before equals issued at

        var message = new CanonicalMessageV1(
            networkEnvironment.Value,
            chainId.Value,
            address.Value,
            iat,
            exp,
            nbf,
            nonce,
            audience,
            mac);

        return Result.Success<CanonicalMessageV1, Error>(message);
    }

    /// <summary>
    /// Validates the message timing constraints
    /// </summary>
    public Result<bool, Error> ValidateTiming(DateTimeOffset currentTime)
    {
        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(IssuedAt);
        var expiration = DateTimeOffset.FromUnixTimeSeconds(Exp);
        var notBefore = DateTimeOffset.FromUnixTimeSeconds(Nbf);

        // Check TTL doesn't exceed 5 minutes
        var ttlSeconds = (expiration - issuedAt).TotalSeconds;
        if (ttlSeconds > MaxTtlSeconds)
        {
            return Result.Failure<bool, Error>(
                Error.Validation($"Wallet proof TTL exceeds 5-minute maximum: {ttlSeconds}s", "CANONICAL_V1.TTL_EXCEEDED"));
        }

        if (ttlSeconds <= 0)
        {
            return Result.Failure<bool, Error>(
                Error.Validation("TTL must be positive", "CANONICAL_V1.TTL_INVALID"));
        }

        // Check not before with skew
        if (currentTime < notBefore.AddSeconds(-ClockSkewSeconds))
        {
            return Result.Failure<bool, Error>(
                Error.Validation("Message not yet valid", "CANONICAL_V1.NOT_YET_VALID"));
        }

        // Check expiration with skew
        if (currentTime > expiration.AddSeconds(ClockSkewSeconds))
        {
            return Result.Failure<bool, Error>(
                Error.Validation("Message has expired", "CANONICAL_V1.EXPIRED"));
        }

        return Result.Success<bool, Error>(true);
    }

    // Cached JSON serializer options for performance
    private static readonly JsonSerializerOptions CanonicalJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false,
        PropertyNamingPolicy = null // Use JsonPropertyName attributes
    };

    /// <summary>
    /// Serializes to canonical JSON with deterministic field order
    /// </summary>
    public string ToCanonicalJson()
    {
        return JsonSerializer.Serialize(this, CanonicalJsonOptions);
    }

    /// <summary>
    /// Gets the canonical byte representation for signature verification
    /// </summary>
    public byte[] GetCanonicalBytes()
    {
        var json = ToCanonicalJson();
        return System.Text.Encoding.UTF8.GetBytes(json);
    }

    // Cached JSON deserializer options for strict parsing
    private static readonly JsonSerializerOptions StrictDeserializerOptions = new()
    {
        PropertyNameCaseInsensitive = false // Strict field name matching
    };

    /// <summary>
    /// Parses a canonical message from JSON
    /// </summary>
    public static Result<CanonicalMessageV1, Error> FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Result.Failure<CanonicalMessageV1, Error>(
                Error.Validation("JSON cannot be empty", "CANONICAL_V1.JSON_EMPTY"));
        }

        try
        {
            var message = JsonSerializer.Deserialize<CanonicalMessageV1>(json, StrictDeserializerOptions);
            if (message == null)
            {
                return Result.Failure<CanonicalMessageV1, Error>(
                    Error.Validation("Failed to deserialize message", "CANONICAL_V1.DESERIALIZATION_FAILED"));
            }

            // Validate version
            if (message.MessageVersion != Version)
            {
                return Result.Failure<CanonicalMessageV1, Error>(
                    Error.Validation($"Unsupported message version: {message.MessageVersion}", "CANONICAL_V1.VERSION_UNSUPPORTED"));
            }

            return Result.Success<CanonicalMessageV1, Error>(message);
        }
        catch (JsonException ex)
        {
            return Result.Failure<CanonicalMessageV1, Error>(
                Error.Validation($"Invalid JSON format: {ex.Message}", "CANONICAL_V1.INVALID_JSON"));
        }
    }
}