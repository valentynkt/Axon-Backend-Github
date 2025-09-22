using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Options;

namespace Axon.Modules.Identity.Infrastructure.Services;

public interface ICanonicalMessageService
{
    Task<Result<CanonicalChallengeMessage, Error>> GenerateChallengeAsync(
        NetworkEnvironment networkEnvironment,
        string chainId,
        string walletAddress,
        string audience,
        CancellationToken cancellationToken = default);

    Result<bool, Error> ValidateCanonicalMessage(
        string message,
        NetworkEnvironment expectedNetworkEnvironment,
        string expectedChainId,
        string expectedWalletAddress,
        string expectedAudience);

    Result<bool, Error> ValidateTtl(DateTimeOffset issuedAt, DateTimeOffset expiration);
}

public record CanonicalChallengeMessage(
    string NetworkEnvironment,
    string ChainId,
    string Address,
    long IssuedAt,
    long Exp,
    string Nonce,
    string Aud,
    string CanonicalJson);

public sealed class CanonicalMessageService : ICanonicalMessageService
{
    private readonly CanonicalMessageOptions _options;
    private readonly byte[] _hmacKey;

    public CanonicalMessageService(IOptions<CanonicalMessageOptions> options)
    {
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(_options.HmacSecret))
            throw new InvalidOperationException("CanonicalMessageOptions:HmacSecret is required.");

        // Allow either raw text or base64 secret
        _hmacKey = TryBase64(_options.HmacSecret, out var raw)
            ? raw
            : Encoding.UTF8.GetBytes(_options.HmacSecret);
    }

    public Task<Result<CanonicalChallengeMessage, Error>> GenerateChallengeAsync(
        NetworkEnvironment networkEnvironment,
        string chainId,
        string walletAddress,
        string audience,
        CancellationToken cancellationToken = default)
    {
        // 1) Validate inputs with VOs
        var chainIdVo = ChainId.Create(chainId);
        if (chainIdVo.IsFailure)
            return Task.FromResult(Result.Failure<CanonicalChallengeMessage, Error>(chainIdVo.Error));

        var addressVo = Address.Create(walletAddress);
        if (addressVo.IsFailure)
            return Task.FromResult(Result.Failure<CanonicalChallengeMessage, Error>(addressVo.Error));

        if (string.IsNullOrWhiteSpace(audience))
            audience = _options.DefaultAudience;

        // 2) Timing + nonce
        var now = DateTimeOffset.UtcNow;
        var issuedAt = now.ToUnixTimeSeconds();
        var expiration = now.AddSeconds(_options.MaxTtlSeconds).ToUnixTimeSeconds();
        var nonce = GenerateBase64UrlNonce(32);

        // 3) Compute MAC over canonical fields (stateless authenticity)
        var mac = ComputeMac(networkEnvironment.Value, chainIdVo.Value.Value, addressVo.Value.Value, issuedAt, expiration, nonce, audience);

        // 4) Build deterministic JSON with fixed ordering
        var canonicalJson = BuildCanonicalJson(networkEnvironment.Value, chainIdVo.Value.Value, addressVo.Value.Value,
                                               issuedAt, expiration, nonce, audience, mac);

        var result = new CanonicalChallengeMessage(
            NetworkEnvironment: networkEnvironment.Value,
            ChainId: chainIdVo.Value.Value,
            Address: addressVo.Value.Value,
            IssuedAt: issuedAt,
            Exp: expiration,
            Nonce: nonce,
            Aud: audience,
            CanonicalJson: canonicalJson);

        return Task.FromResult(Result.Success<CanonicalChallengeMessage, Error>(result));
    }

    public Result<bool, Error> ValidateCanonicalMessage(
        string message,
        NetworkEnvironment expectedNetworkEnvironment,
        string expectedChainId,
        string expectedWalletAddress,
        string expectedAudience)
    {
        if (string.IsNullOrWhiteSpace(message))
            return Result.Failure<bool, Error>(Error.Validation("Message cannot be empty", "CANONICAL_MESSAGE.MESSAGE_EMPTY"));

        try
        {
            using var doc = JsonDocument.Parse(message);
            var root = doc.RootElement;
            // Required fields + types
            if (!TryGetString(root, "network_environment", out var env) ||
                !TryGetString(root, "chain_id", out var chain) ||
                !TryGetString(root, "address", out var address) ||
                !TryGetInt64(root, "issued_at", out var iat) ||
                !TryGetInt64(root, "exp", out var exp) ||
                !TryGetString(root, "nonce", out var nonce) ||
                !TryGetString(root, "aud", out var aud) ||
                !TryGetString(root, "mac", out var mac))
            {
                return Result.Failure<bool, Error>(Error.Validation("Message missing required fields", "CANONICAL_MESSAGE.MISSING_FIELDS"));
            }

            // Value checks
            if (!string.Equals(env, expectedNetworkEnvironment.Value, StringComparison.Ordinal))
                return Fail($"Network environment mismatch: expected {expectedNetworkEnvironment.Value}, got {env}", "CANONICAL_MESSAGE.NETWORK_ENVIRONMENT_MISMATCH");

            if (!string.Equals(chain, expectedChainId, StringComparison.Ordinal))
                return Fail($"Chain ID mismatch: expected {expectedChainId}, got {chain}", "CANONICAL_MESSAGE.CHAIN_ID_MISMATCH");

            if (!string.Equals(address, expectedWalletAddress, StringComparison.Ordinal))
                return Fail($"Address mismatch: expected {expectedWalletAddress}, got {address}", "CANONICAL_MESSAGE.ADDRESS_MISMATCH");

            if (!string.Equals(aud, expectedAudience, StringComparison.Ordinal))
                return Fail($"Audience mismatch: expected {expectedAudience}, got {aud}", "CANONICAL_MESSAGE.AUDIENCE_MISMATCH");

            // TTL + skew
            var issuedAt = DateTimeOffset.FromUnixTimeSeconds(iat);
            var expiration = DateTimeOffset.FromUnixTimeSeconds(exp);
            var ttl = ValidateTtl(issuedAt, expiration);
            if (ttl.IsFailure) return ttl;

            var now = DateTimeOffset.UtcNow;
            if (now < issuedAt.AddSeconds(-_options.ClockSkewSeconds))
                return Fail("Message not yet valid (issued in future)", "CANONICAL_MESSAGE.NOT_YET_VALID");
            if (now > expiration.AddSeconds(_options.ClockSkewSeconds))
                return Fail("Message has expired", "CANONICAL_MESSAGE.EXPIRED");

            // VO validation (chain + address)
            var chainIdVo = ChainId.Create(chain);
            if (chainIdVo.IsFailure) return Result.Failure<bool, Error>(chainIdVo.Error);
            var addrVo = Address.Create(address);
            if (addrVo.IsFailure) return Result.Failure<bool, Error>(addrVo.Error);

            // MAC verification (stateless anti-tamper)
            var expectedMac = ComputeMac(env, chain, address, iat, exp, nonce, aud);
            if (!FixedTimeEquals(mac, expectedMac))
                return Fail("MAC verification failed", "CANONICAL_MESSAGE.MAC_INVALID");

            return Result.Success<bool, Error>(true);
        }
        catch (JsonException)
        {
            return Result.Failure<bool, Error>(Error.Validation("Invalid JSON format", "CANONICAL_MESSAGE.INVALID_JSON"));
        }
        catch (Exception ex)
        {
            return Result.Failure<bool, Error>(Error.Internal($"Error validating canonical message: {ex.Message}", "CANONICAL_MESSAGE.VALIDATION_ERROR"));
        }
    }

    public Result<bool, Error> ValidateTtl(DateTimeOffset issuedAt, DateTimeOffset expiration)
    {
        var ttlSeconds = (expiration - issuedAt).TotalSeconds;
        if (ttlSeconds > _options.MaxTtlSeconds)
            return Fail($"TTL exceeds maximum: {ttlSeconds}s > {_options.MaxTtlSeconds}s", "CANONICAL_MESSAGE.TTL_EXCEEDED");
        if (ttlSeconds <= 0)
            return Fail("TTL must be positive", "CANONICAL_MESSAGE.TTL_INVALID");
        return Result.Success<bool, Error>(true);
    }

    // ---------- helpers ----------

    private static bool TryBase64(string s, out byte[] bytes)
    {
        try { bytes = Convert.FromBase64String(s); return true; }
        catch { bytes = Array.Empty<byte>(); return false; }
    }

    private static string GenerateBase64UrlNonce(int numBytes)
    {
        var bytes = RandomNumberGenerator.GetBytes(numBytes);
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> data)
    {
        var b64 = Convert.ToBase64String(data);
        return b64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private string ComputeMac(string env, string chain, string address, long issuedAt, long exp, string nonce, string aud)
    {
        // Stable concatenation; no separators ambiguity (use '\n')
        var payload = $"{env}\n{chain}\n{address}\n{issuedAt}\n{exp}\n{nonce}\n{aud}";
        var bytes = Encoding.UTF8.GetBytes(payload);
        var mac = HMACSHA256.HashData(_hmacKey, bytes);
        return Base64UrlEncode(mac);
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        // Compare base64url strings in constant time
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        if (ba.Length != bb.Length) return false;
        var diff = 0;
        for (int i = 0; i < ba.Length; i++) diff |= ba[i] ^ bb[i];
        return diff == 0;
    }

    private static string BuildCanonicalJson(
        string env, string chain, string address, long issuedAt, long exp, string nonce, string aud, string mac)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("network_environment", env);
            writer.WriteString("chain_id", chain);
            writer.WriteString("address", address);
            writer.WriteNumber("issued_at", issuedAt);
            writer.WriteNumber("exp", exp);
            writer.WriteString("nonce", nonce);
            writer.WriteString("aud", aud);
            writer.WriteString("mac", mac);
            writer.WriteEndObject();
            writer.Flush();
        }
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static bool TryGetString(JsonElement root, string name, out string value)
    {
        value = default!;
        if (!root.TryGetProperty(name, out var p)) return false;
        if (p.ValueKind != JsonValueKind.String) return false;
        value = p.GetString()!;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryGetInt64(JsonElement root, string name, out long value)
    {
        value = default;
        if (!root.TryGetProperty(name, out var p)) return false;
        return p.TryGetInt64(out value);
    }

    private static Result<bool, Error> Fail(string msg, string code)
        => Result.Failure<bool, Error>(Error.Validation(msg, code));
}
